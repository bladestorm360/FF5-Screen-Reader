using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using UnityEngine;
using MapRouteSearcher = Il2Cpp.MapRouteSearcher;

namespace FFV_ScreenReader.Field.Routing
{
    /// <summary>
    /// Reaches targets beyond MapRouteSearcher's ~31.5-tile window by chaining several of
    /// its searches together, without giving up its traversability model.
    ///
    /// The lever: Search takes an ARBITRARY start cell, not necessarily the player's. So the
    /// mod can search from a virtual cell, stitch the results, and never move anything. That
    /// keeps layers, hidden passages, IgnoreRoute and collision entities — none of which a
    /// terrain-attribute grid models — being handled by the game itself.
    ///
    /// This is A* over breadcrumb nodes, not a greedy chain. A greedy "hop toward the target,
    /// repeat" dead-ends permanently on any concave obstacle bigger than the horizon, and
    /// cannot solve a route that must first head AWAY from the objective to leave a room.
    ///
    /// PORTING: no speech, no localization, no logging calls, no god-class references.
    /// </summary>
    internal static class BreadcrumbRouteChainer
    {
        // --- Window geometry -------------------------------------------------
        // MapRouteSearcher builds a 64x64 cell window (SearchHorizontalLimit /
        // SearchVerticalLimit), so roughly +/-31 cells from the start. Hops use a smaller
        // radius: the exact geometry is inferred, and a hop that lands outside the window
        // just fails silently, so the margin is cheap insurance.
        private const int WINDOW_RADIUS = 31;
        private static readonly int[] HOP_RADII = { 24, 16, 10 };

        /// <summary>
        /// Candidate bearings relative to the straight line to the target, in degrees.
        /// Backward bearings are first-class members from the very first expansion — that
        /// is what lets a route leave a room the "wrong" way. Probe ORDER favours the
        /// target (cheap wins first); membership never does.
        /// </summary>
        private static readonly float[] CANDIDATE_BEARINGS =
            { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };

        // --- Budgets ---------------------------------------------------------
        private const int MAX_HOPS = 24;
        private const int BUDGET_MS = 400;

        /// <summary>Nodes within this many cells of each other are the same node.</summary>
        private const int NODE_QUANTISE = 8;

        /// <summary>
        /// Allow chaining when the target is INSIDE the window and the plain search failed.
        ///
        /// Default off, by explicit instruction: a target inside the window that the plain
        /// search could not reach is normally a genuine local block, and answering "no
        /// route" is right. The case this rejects is a near target whose only route is a
        /// long way around, leaving and re-entering the window — real, but rarer than the
        /// false positives turning this on would cost.
        /// </summary>
        private const bool CHAIN_WHEN_TARGET_WITHIN_WINDOW = false;

        // =====================================================================
        // Entry point
        // =====================================================================

        /// <summary>
        /// Attempts a chained route. Returns null when chaining should not even be tried,
        /// so the caller keeps whatever the plain search already reported.
        /// </summary>
        internal static PathInfo TryChain(
            Vector3 playerWorldPos, Vector3 targetWorldPos,
            IMapAccessor mapHandle, FieldPlayer player)
        {
            if (mapHandle == null || player == null) return null;

            try
            {
                int mapWidth = mapHandle.GetCollisionLayerWidth();
                int mapHeight = mapHandle.GetCollisionLayerHeight();
                if (mapWidth <= 0 || mapHeight <= 0 || mapWidth > 10000 || mapHeight > 10000)
                    return null;

                var ctx = new ChainContext
                {
                    MapHandle = mapHandle,
                    Player = player,
                    MapWidth = mapWidth,
                    MapHeight = mapHeight,
                    CollisionEnabled = player._IsOnCollision_k__BackingField,
                    LayerZ = player.gameObject.layer - 9
                };

                ctx.Start = WorldToCell(playerWorldPos, mapWidth, mapHeight);
                ctx.Goal = WorldToCell(targetWorldPos, mapWidth, mapHeight);

                int dx = Mathf.Abs(ctx.Goal.x - ctx.Start.x);
                int dy = Mathf.Abs(ctx.Goal.y - ctx.Start.y);
                bool beyondWindow = dx > WINDOW_RADIUS || dy > WINDOW_RADIUS;

                // Inside the window the plain search is authoritative and the chainer must
                // stay completely dormant — no probes, no logging, nothing.
                if (!beyondWindow && !CHAIN_WHEN_TARGET_WITHIN_WINDOW)
                    return null;

                return RunHopSearch(ctx, playerWorldPos, targetWorldPos);
            }
            catch (Exception ex)
            {
                RoutingAdapter.WarnOnce("chain", $"Breadcrumb chaining failed: {ex.Message}");
                return null;
            }
        }

        // Removed 2026-07-26: a "Requires Pirate Ship" pre-check that answered which vehicle
        // could cross an obstacle by flooding the terrain grid under each vehicle's OkList.
        //
        // It was deleted for a reason worth keeping written down, because the idea is
        // tempting enough to be reinvented: terrain reachability cannot see EVENT gating. A
        // destination may be closed by an unfinished story event rather than by the terrain,
        // and nothing in the terrain data distinguishes those two cases. A confident
        // "Requires Pirate Ship" when the real blocker is an unfinished event sends the
        // player somewhere useless — strictly worse than saying nothing.
        //
        // Note also that naming the terrain would NOT have rescued it. The check never
        // classified terrain in the first place; it asked each vehicle's OkList directly,
        // which is the game's own ground truth. A terrain name would have to be derived from
        // that same data, so it was only ever narration on top of the same inference.

        // =====================================================================
        // Hop-level A*
        // =====================================================================

        private sealed class ChainContext
        {
            public IMapAccessor MapHandle;
            public FieldPlayer Player;
            public int MapWidth;
            public int MapHeight;
            public bool CollisionEnabled;
            public float LayerZ;
            public Vector2Int Start;
            public Vector2Int Goal;
        }

        private sealed class HopNode
        {
            public Vector2Int Cell;
            public int GCost;                    // real steps from the player
            public int FCost;                    // GCost + straight-line estimate
            public HopNode Parent;
            public List<Vector3> PathFromParent; // world-space, joint cell included
        }

        private static PathInfo RunHopSearch(ChainContext ctx, Vector3 playerWorldPos, Vector3 targetWorldPos)
        {
            var sw = Stopwatch.StartNew();

            // Region bound. Deliberately a bound on WHERE the search may look, not on
            // whether each hop makes progress: a route that must back out of a room regresses
            // for several hops before it turns the corner, and any "give up when not getting
            // closer" rule would kill exactly those maps.
            int spanToGoal = Mathf.Max(
                Mathf.Abs(ctx.Goal.x - ctx.Start.x),
                Mathf.Abs(ctx.Goal.y - ctx.Start.y));
            int regionBound = Mathf.Max(WINDOW_RADIUS, Mathf.RoundToInt(spanToGoal * 1.5f)) + 32;

            var open = new List<HopNode>();
            var closed = new Dictionary<long, int>();   // quantised cell -> best GCost
            var enclosed = new HashSet<long>();         // nodes with no exit in any direction

            var startNode = new HopNode
            {
                Cell = ctx.Start,
                GCost = 0,
                FCost = Heuristic(ctx.Start, ctx.Goal),
                Parent = null,
                PathFromParent = null
            };
            open.Add(startNode);

            int hops = 0;
            bool budgetHit = false;
            HopNode reachedGoal = null;

            while (open.Count > 0)
            {
                if (hops >= MAX_HOPS || sw.ElapsedMilliseconds > BUDGET_MS)
                {
                    budgetHit = true;
                    break;
                }

                // Pop lowest F.
                int bestIdx = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].FCost < open[bestIdx].FCost) bestIdx = i;

                var current = open[bestIdx];
                open.RemoveAt(bestIdx);

                long key = QuantiseKey(current.Cell);
                if (closed.TryGetValue(key, out int seenG) && seenG <= current.GCost)
                    continue;
                closed[key] = current.GCost;

                hops++;

                // Can we finish from here in one plain search?
                var finish = TrySearch(ctx, current.Cell, ctx.Goal);
                if (finish != null)
                {
                    reachedGoal = new HopNode
                    {
                        Cell = ctx.Goal,
                        GCost = current.GCost + Mathf.Max(0, finish.Count - 1),
                        Parent = current,
                        PathFromParent = finish
                    };
                    break;
                }

                // Otherwise expand candidate breadcrumbs in every direction.
                bool anyExit = false;
                foreach (var candidate in GenerateCandidates(current.Cell, ctx.Goal))
                {
                    if (ChebyshevDistance(candidate, ctx.Start) > regionBound)
                        continue;

                    long ckey = QuantiseKey(candidate);
                    if (ckey == key) continue;
                    if (enclosed.Contains(ckey)) continue;

                    var segment = TrySearch(ctx, current.Cell, candidate);
                    if (segment == null) continue;

                    anyExit = true;

                    int g = current.GCost + Mathf.Max(0, segment.Count - 1);
                    if (closed.TryGetValue(ckey, out int knownG) && knownG <= g)
                        continue;

                    open.Add(new HopNode
                    {
                        Cell = candidate,
                        GCost = g,
                        FCost = g + Heuristic(candidate, ctx.Goal),
                        Parent = current,
                        PathFromParent = segment
                    });
                }

                if (!anyExit)
                {
                    // Nothing could leave this node in ANY direction — it is genuinely
                    // walled in. Record it so it is never probed again, and if it is where
                    // the player is standing, there is nothing to search at all.
                    enclosed.Add(key);
                    if (current.Parent == null)
                    {
                        RoutingAdapter.Log("[Routing] Start node is enclosed — no route without a vehicle");
                        return Failed(RouteFailure.NoPathProved);
                    }
                }
            }

            sw.Stop();

            if (reachedGoal != null)
            {
                var stitched = Stitch(reachedGoal, playerWorldPos, ctx);
                RoutingAdapter.Log($"[Routing] Breadcrumb route: {hops} hops, " +
                                   $"{stitched.Count} points in {sw.ElapsedMilliseconds}ms");

                return new PathInfo
                {
                    Success = true,
                    WorldPath = stitched,
                    StepCount = Mathf.Max(0, stitched.Count - 1),
                    Failure = RouteFailure.None
                };
            }

            // No route. Which kind matters for the log, not for what is spoken: the player
            // cannot act on the difference, and a partial route into a dead end is worse
            // than silence, so both stay directionless.
            if (budgetHit)
            {
                RoutingAdapter.Log($"[Routing] Breadcrumb search stopped early after {hops} hops, " +
                                   $"{sw.ElapsedMilliseconds}ms (limit: " +
                                   $"{(hops >= MAX_HOPS ? "hop cap" : "wall clock")}) — reachability unknown");
                return Failed(RouteFailure.StoppedEarly);
            }

            RoutingAdapter.Log($"[Routing] Breadcrumb search exhausted after {hops} hops, " +
                               $"{sw.ElapsedMilliseconds}ms — no route exists within the candidate model");
            return Failed(RouteFailure.NoPathProved);
        }

        private static PathInfo Failed(RouteFailure reason) =>
            new PathInfo { Success = false, Failure = reason };

        // =====================================================================
        // Candidates, search, stitching
        // =====================================================================

        /// <summary>
        /// Breadcrumb candidates around a node: every bearing at every radius. Ordered
        /// target-first so a straightforward route costs the fewest probes, but the backward
        /// bearings are present from the first expansion, which is what makes retreat
        /// routes solvable at all.
        /// </summary>
        private static IEnumerable<Vector2Int> GenerateCandidates(Vector2Int from, Vector2Int goal)
        {
            float baseAngle = Mathf.Atan2(goal.y - from.y, goal.x - from.x) * Mathf.Rad2Deg;

            foreach (int radius in HOP_RADII)
            {
                foreach (float bearing in CANDIDATE_BEARINGS)
                {
                    float rad = (baseAngle + bearing) * Mathf.Deg2Rad;
                    yield return new Vector2Int(
                        from.x + Mathf.RoundToInt(Mathf.Cos(rad) * radius),
                        from.y + Mathf.RoundToInt(Mathf.Sin(rad) * radius));
                }
            }
        }

        /// <summary>
        /// One plain MapRouteSearcher search between two cells, normalised to world space.
        /// Returns null when there is no path. Tries the destination layer ladder the
        /// existing pathfinder uses, so stairs and layer changes behave the same way.
        /// </summary>
        private static List<Vector3> TrySearch(ChainContext ctx, Vector2Int from, Vector2Int to)
        {
            if (to.x < 0 || to.y < 0 || to.x >= ctx.MapWidth || to.y >= ctx.MapHeight)
                return null;

            var start = new Vector3(from.x, from.y, ctx.LayerZ);

            for (int destZ = 2; destZ >= 0; destZ--)
            {
                var dest = new Vector3(to.x, to.y, destZ);

                Il2CppSystem.Collections.Generic.List<Vector3> points;
                try
                {
                    points = MapRouteSearcher.Search(ctx.MapHandle, start, dest, ctx.CollisionEnabled);
                }
                catch
                {
                    continue;
                }

                if (points == null || points.Count == 0)
                    continue;

                var managed = new List<Vector3>(points.Count);
                for (int i = 0; i < points.Count; i++)
                    managed.Add(points[i]);

                // Also calibrate here: on a map where the plain search never succeeds, the
                // first search of the session is one of these, and an uncalibrated path
                // would be described with north and south the wrong way round.
                PathSpaceNormalizer.Detect(start, managed, ctx.MapWidth, ctx.MapHeight);

                return PathSpaceNormalizer.ToWorld(managed, ctx.MapWidth, ctx.MapHeight);
            }

            return null;
        }

        /// <summary>
        /// Walks the hop chain back to the player and concatenates the segments, dropping
        /// the duplicated joint cell at each seam. Describing the stitched path as a whole
        /// is what makes hops invisible in speech — a straight run across a seam becomes
        /// "north 89" rather than "north 24, north 30, north 35".
        /// </summary>
        private static List<Vector3> Stitch(HopNode goalNode, Vector3 playerWorldPos, ChainContext ctx)
        {
            var segments = new List<List<Vector3>>();

            for (var node = goalNode; node?.PathFromParent != null; node = node.Parent)
                segments.Add(node.PathFromParent);

            segments.Reverse();

            var stitched = new List<Vector3>();
            foreach (var segment in segments)
            {
                int startIndex = stitched.Count == 0 ? 0 : 1; // skip the repeated joint cell
                for (int i = startIndex; i < segment.Count; i++)
                    stitched.Add(segment[i]);
            }

            if (stitched.Count == 0)
                stitched.Add(playerWorldPos);

            return stitched;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static Vector2Int WorldToCell(Vector3 world, int mapWidth, int mapHeight) =>
            new Vector2Int(
                Mathf.FloorToInt(mapWidth * 0.5f + world.x * RoutingAdapter.TileSizeInverse),
                Mathf.FloorToInt(mapHeight * 0.5f - world.y * RoutingAdapter.TileSizeInverse));

        private static int Heuristic(Vector2Int a, Vector2Int b) => ChebyshevDistance(a, b);

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static long QuantiseKey(Vector2Int cell)
        {
            long qx = cell.x / NODE_QUANTISE;
            long qy = cell.y / NODE_QUANTISE;
            return (qx << 32) ^ (qy & 0xFFFFFFFFL);
        }
    }
}
