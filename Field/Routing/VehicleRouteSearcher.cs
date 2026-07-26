using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppLast.Map;
using UnityEngine;

namespace FFV_ScreenReader.Field.Routing
{
    /// <summary>
    /// Mod-owned route search for vehicles, replacing the game's MapRouteSearcher when the
    /// player is riding something.
    ///
    /// Two reasons the game's searcher cannot do this job:
    ///   1. It is capped at a 64x64 cell window (MapRouteSearcher.SearchHorizontalLimit /
    ///      SearchVerticalLimit), i.e. about +/-31.5 tiles, so the far side of a world map
    ///      is unreachable by construction.
    ///   2. It searches collision mapping data, which describes walking. A ship crossing
    ///      ocean and an airship crossing mountains are both invisible to that model.
    ///
    /// This searcher works from terrain attributes plus the vehicle's own OkList, which is
    /// exactly the pair the landing-ping feature already proves correct.
    ///
    /// PORTING: no speech, no localization, no logging calls, no god-class references — all
    /// game-specific facts come from RoutingAdapter. See docs/debug.md.
    /// </summary>
    internal static class VehicleRouteSearcher
    {
        // Movement cost is uniform, so a plain BFS is an exact shortest-path search here.
        private const int UNREACHABLE = -1;

        /// <summary>How far from the target to look for a stand-in tile, in cells.</summary>
        private const int MAX_APPROACH_RINGS = 32;

        /// <summary>Attribute values are small ints; anything past this means bad data.</summary>
        private const int MAX_ATTRIBUTE = 4096;

        // =====================================================================
        // Terrain attribute grid — one per map, built once
        // =====================================================================

        private static int gridMapId = -1;
        private static int gridWidth;
        private static int gridHeight;
        private static int[] attrGrid;
        private static int maxAttribute;

        /// <summary>Dropped on map transition; the next request rebuilds.</summary>
        public static void InvalidateAll()
        {
            gridMapId = -1;
            attrGrid = null;
            passTransportId = -1;
            okByAttribute = null;
            landingByAttribute = null;
            InvalidateFlood();
            RoutingAdapter.ResetLogLatches();
        }

        public static bool HasGridFor(int mapId) => attrGrid != null && gridMapId == mapId;

        /// <summary>
        /// Builds the attribute grid for the current map if it is not already cached.
        ///
        /// Called from the map-transition hook so the cost lands during a loading screen the
        /// player is already waiting through, rather than on a keypress. Safe to call again
        /// later — it is a no-op once cached.
        /// </summary>
        public static bool EnsureGrid(FieldController fieldController, IMapAccessor mapHandle, int mapId)
        {
            if (attrGrid != null && gridMapId == mapId)
                return true;

            if (fieldController == null || mapHandle == null)
                return false;

            try
            {
                int w = mapHandle.GetCollisionLayerWidth();
                int h = mapHandle.GetCollisionLayerHeight();

                if (w <= 0 || h <= 0 || w > 10000 || h > 10000)
                {
                    RoutingAdapter.WarnOnce("grid-dims", $"Refusing to build attribute grid, implausible dims {w}x{h}");
                    return false;
                }

                var sw = Stopwatch.StartNew();

                var grid = new int[w * h];
                int maxAttr = 0;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        // Same call the landing-spot check uses, so the two can never
                        // disagree about what a tile is.
                        int attr = fieldController.GetCellAttribute(new Vector2(x, y));
                        if (attr < 0 || attr > MAX_ATTRIBUTE) attr = 0;
                        grid[row + x] = attr;
                        if (attr > maxAttr) maxAttr = attr;
                    }
                }

                sw.Stop();

                attrGrid = grid;
                gridWidth = w;
                gridHeight = h;
                gridMapId = mapId;
                maxAttribute = maxAttr;

                // Transport passability is derived from the grid, so it is stale now.
                passTransportId = -1;
                okByAttribute = null;
                landingByAttribute = null;
                InvalidateFlood();

                RoutingAdapter.Log($"[Routing] Attribute grid built for map {mapId}: {w}x{h} " +
                                   $"({w * h} cells, max attribute {maxAttr}) in {sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                RoutingAdapter.WarnOnce("grid-build", $"Attribute grid build failed: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // Per-transport passability
        //
        // Attributes are small ints, so one CheckOkList call per DISTINCT attribute
        // builds a lookup table — a few hundred interop calls instead of one per cell.
        // =====================================================================

        private static int passTransportId = -1;
        private static bool[] okByAttribute;
        private static bool[] landingByAttribute;

        private static bool EnsurePassability(TransportationController tc, int transportId)
        {
            if (okByAttribute != null && passTransportId == transportId)
                return true;

            if (tc == null || attrGrid == null)
                return false;

            try
            {
                int n = maxAttribute + 1;
                var ok = new bool[n];
                var landing = new bool[n];

                // Attribute 0 is never passable: CheckOkList indexes okList[attribute - 1]
                // and short-circuits to false for 0. Start at 1 to mirror that exactly.
                for (int attr = 1; attr < n; attr++)
                {
                    ok[attr] = tc.CheckOkList(transportId, attr);
                    landing[attr] = tc.CheckLandingList(transportId, attr);
                }

                okByAttribute = ok;
                landingByAttribute = landing;
                passTransportId = transportId;
                InvalidateFlood();
                return true;
            }
            catch (Exception ex)
            {
                RoutingAdapter.WarnOnce("passability", $"Passability table build failed: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // Cell <-> world conversion
        //
        // Matches the formula already used by FieldNavigationHelper.FindPathTo, so the
        // two searchers agree on what a cell is. Note the y inversion: cell y grows
        // southward, world y grows northward.
        // =====================================================================

        public static int WorldToCellX(float worldX) =>
            Mathf.FloorToInt(gridWidth * 0.5f + worldX * RoutingAdapter.TileSizeInverse);

        public static int WorldToCellY(float worldY) =>
            Mathf.FloorToInt(gridHeight * 0.5f - worldY * RoutingAdapter.TileSizeInverse);

        private static float CellToWorldX(int cellX) =>
            (cellX - gridWidth * 0.5f) * RoutingAdapter.TileSize;

        private static float CellToWorldY(int cellY) =>
            (gridHeight * 0.5f - cellY) * RoutingAdapter.TileSize;

        private static bool InBounds(int x, int y) =>
            x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;

        // =====================================================================
        // Flood — memoised on (map, transport, start cell)
        //
        // One BFS answers every question the callers ask: reachability for the entity
        // filter (O(1) per entity), a path to any target (walk the parent array), and the
        // nearest reachable stand-in tile. Recomputed only when the player changes cell.
        // =====================================================================

        private static int floodMapId = -1;
        private static int floodTransportId = -1;
        private static int floodStartIndex = -1;
        private static int[] dist;
        private static int[] parent;
        private static int[] bfsQueue;

        private static void InvalidateFlood()
        {
            floodMapId = -1;
            floodTransportId = -1;
            floodStartIndex = -1;
        }

        private static bool wrapX, wrapY;

        private static bool EnsureFlood(int mapId, int transportId, int startX, int startY)
        {
            if (!InBounds(startX, startY)) return false;

            int startIndex = startY * gridWidth + startX;

            if (dist != null
                && floodMapId == mapId
                && floodTransportId == transportId
                && floodStartIndex == startIndex)
                return true;

            int cellCount = gridWidth * gridHeight;

            if (dist == null || dist.Length != cellCount)
            {
                dist = new int[cellCount];
                parent = new int[cellCount];
                bfsQueue = new int[cellCount];
            }

            wrapX = RoutingAdapter.WorldMapWrapsX(mapId);
            wrapY = RoutingAdapter.WorldMapWrapsY(mapId);

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < cellCount; i++)
            {
                dist[i] = UNREACHABLE;
                parent[i] = -1;
            }

            // The start tile is where the player already is, so it is passable by
            // definition even if its attribute says otherwise (docked ship, scripted
            // placement). Seeding it unconditionally avoids a "no route from here" that
            // contradicts the player's own position.
            int head = 0, tail = 0;
            dist[startIndex] = 0;
            bfsQueue[tail++] = startIndex;

            while (head < tail)
            {
                int cur = bfsQueue[head++];
                int cx = cur % gridWidth;
                int cy = cur / gridWidth;
                int nextDist = dist[cur] + 1;

                TryVisit(cx, cy, 0, -1, cur, nextDist, ref tail);  // north (cell y decreases)
                TryVisit(cx, cy, 0, 1, cur, nextDist, ref tail);   // south
                TryVisit(cx, cy, 1, 0, cur, nextDist, ref tail);   // east
                TryVisit(cx, cy, -1, 0, cur, nextDist, ref tail);  // west
            }

            sw.Stop();

            floodMapId = mapId;
            floodTransportId = transportId;
            floodStartIndex = startIndex;

            RoutingAdapter.LogOnce(ref loggedFirstFlood,
                $"[Routing] Flood: {tail} of {cellCount} cells reachable for transport {transportId} " +
                $"in {sw.ElapsedMilliseconds}ms (wrapX={wrapX}, wrapY={wrapY})");

            return true;
        }

        private static bool loggedFirstFlood;

        private static void TryVisit(int cx, int cy, int dx, int dy, int from, int nextDist, ref int tail)
        {
            int nx = cx + dx;
            int ny = cy + dy;

            if (wrapX)
            {
                if (nx < 0) nx += gridWidth;
                else if (nx >= gridWidth) nx -= gridWidth;
            }
            else if (nx < 0 || nx >= gridWidth) return;

            if (wrapY)
            {
                if (ny < 0) ny += gridHeight;
                else if (ny >= gridHeight) ny -= gridHeight;
            }
            else if (ny < 0 || ny >= gridHeight) return;

            int idx = ny * gridWidth + nx;
            if (dist[idx] != UNREACHABLE) return;
            if (!okByAttribute[attrGrid[idx]]) return;

            dist[idx] = nextDist;
            parent[idx] = from;
            bfsQueue[tail++] = idx;
        }

        // =====================================================================
        // Public queries
        // =====================================================================

        /// <summary>
        /// Routes to the target, or to the nearest tile this transport can occupy when the
        /// target's own tile is off-limits — an airship cannot sit on a town tile, but it
        /// can land beside it, and "beside it" is the useful answer.
        /// </summary>
        public static PathInfo FindPath(
            FieldController fieldController, IMapAccessor mapHandle, TransportationController tc,
            int mapId, int transportId, Vector3 playerWorldPos, Vector3 targetWorldPos)
        {
            var result = new PathInfo { Success = false };

            if (!EnsureGrid(fieldController, mapHandle, mapId))
            {
                result.ErrorMessage = "Attribute grid unavailable";
                return result;
            }

            if (!EnsurePassability(tc, transportId))
            {
                result.ErrorMessage = "Passability table unavailable";
                return result;
            }

            int sx = WorldToCellX(playerWorldPos.x);
            int sy = WorldToCellY(playerWorldPos.y);
            if (!EnsureFlood(mapId, transportId, sx, sy))
            {
                result.ErrorMessage = "Flood failed";
                return result;
            }

            int tx = WorldToCellX(targetWorldPos.x);
            int ty = WorldToCellY(targetWorldPos.y);
            if (!InBounds(tx, ty))
            {
                result.ErrorMessage = "Target outside map";
                return result;
            }

            int goalIndex = ty * gridWidth + tx;
            bool approximate = false;

            if (dist[goalIndex] == UNREACHABLE)
            {
                goalIndex = FindNearestReachable(tx, ty);
                if (goalIndex < 0)
                {
                    result.Failure = RouteFailure.NoPathProved;
                    return result;
                }
                approximate = true;
            }

            result.WorldPath = ReconstructPath(goalIndex);
            result.Success = true;
            result.StepCount = Mathf.Max(0, result.WorldPath.Count - 1);
            result.IsApproximate = approximate;

            if (approximate)
            {
                int gx = goalIndex % gridWidth;
                int gy = goalIndex / gridWidth;

                // Offset expressed in world terms so the caller can phrase it with the same
                // compass helper every other announcement uses.
                result.ApproachOffset = new Vector2(
                    targetWorldPos.x - CellToWorldX(gx),
                    targetWorldPos.y - CellToWorldY(gy));

                result.IsLandingSpot = landingByAttribute != null
                    && landingByAttribute[attrGrid[goalIndex]];
            }

            return result;
        }

        /// <summary>
        /// Ring-expands outward from the target for the reachable cell closest to it.
        /// Rings are searched nearest-first, so the first ring containing any reachable
        /// cell wins; within a ring the shortest travel distance breaks the tie.
        /// </summary>
        private static int FindNearestReachable(int tx, int ty)
        {
            for (int ring = 1; ring <= MAX_APPROACH_RINGS; ring++)
            {
                int best = -1;
                int bestDist = int.MaxValue;

                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        // Perimeter only — inner cells were covered by earlier rings.
                        if (Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;

                        int nx = tx + dx;
                        int ny = ty + dy;

                        if (wrapX)
                        {
                            if (nx < 0) nx += gridWidth;
                            else if (nx >= gridWidth) nx -= gridWidth;
                        }
                        if (wrapY)
                        {
                            if (ny < 0) ny += gridHeight;
                            else if (ny >= gridHeight) ny -= gridHeight;
                        }

                        if (!InBounds(nx, ny)) continue;

                        int idx = ny * gridWidth + nx;
                        if (dist[idx] == UNREACHABLE) continue;

                        if (dist[idx] < bestDist)
                        {
                            bestDist = dist[idx];
                            best = idx;
                        }
                    }
                }

                if (best >= 0) return best;
            }

            return -1;
        }

        /// <summary>
        /// Walks the parent chain back to the player and returns the path in WORLD space,
        /// oriented so +y is north. DirectionHelper reads +y as north, so emitting cell
        /// space here would invert every north/south in the spoken route.
        /// </summary>
        private static List<Vector3> ReconstructPath(int goalIndex)
        {
            var reversed = new List<Vector3>();

            int cur = goalIndex;
            int guard = 0;
            int cellCount = gridWidth * gridHeight;

            while (cur >= 0 && guard++ <= cellCount)
            {
                int cx = cur % gridWidth;
                int cy = cur / gridWidth;
                reversed.Add(new Vector3(CellToWorldX(cx), CellToWorldY(cy), 0f));

                if (cur == floodStartIndex) break;
                cur = parent[cur];
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
