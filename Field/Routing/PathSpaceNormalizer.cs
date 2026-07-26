using System.Collections.Generic;
using UnityEngine;

namespace FFV_ScreenReader.Field.Routing
{
    /// <summary>
    /// Normalises whatever MapRouteSearcher.Search hands back into world space.
    ///
    /// Why this exists: Search is *given* cell positions and its Node type stores a field
    /// called CellPos, which suggests it returns cell positions too. But every route is
    /// described through DirectionHelper, which reads +y as NORTH — true in world space and
    /// backwards in cell space, where y grows southward. One of those two readings is wrong,
    /// and the dump alone cannot say which.
    ///
    /// Rather than guess, the first path of the session is measured against two known
    /// reference points: the cell coordinate that was passed in, and the player's world
    /// position. Whichever it matches decides the space, the verdict is cached for the rest
    /// of the session, and cell-space paths are converted so callers only ever see world
    /// space. Both searchers then agree, and north is north either way.
    /// </summary>
    internal static class PathSpaceNormalizer
    {
        internal enum Space { Unknown, World, Cell }

        internal static Space Detected { get; private set; } = Space.Unknown;

        private static bool logged;

        internal static void Reset()
        {
            Detected = Space.Unknown;
            logged = false;
        }

        /// <summary>
        /// Classifies the path once per session, from the search's own inputs alone — the
        /// start cell that was passed in, and the world position that cell corresponds to.
        /// Self-contained on purpose: every search goes through here, so whichever one
        /// happens first in a session settles it, including a chained search on a map where
        /// the plain search never succeeds.
        /// </summary>
        internal static void Detect(Vector3 startCell, List<Vector3> path, int mapWidth, int mapHeight)
        {
            if (Detected != Space.Unknown || path == null || path.Count == 0)
                return;

            Vector3 first = path[0];

            // The path's first point is the start, expressed in whatever space the searcher
            // works in. Compare it against that same start written both ways and take the
            // nearer. Two references rather than one: a lone threshold would misfire at the
            // handful of coordinates where a cell index and a world unit coincide.
            float startWorldX = (startCell.x - mapWidth * 0.5f) * RoutingAdapter.TileSize;
            float startWorldY = (mapHeight * 0.5f - startCell.y) * RoutingAdapter.TileSize;

            float cellDelta = Mathf.Abs(first.x - startCell.x) + Mathf.Abs(first.y - startCell.y);
            float worldDelta = Mathf.Abs(first.x - startWorldX) + Mathf.Abs(first.y - startWorldY);

            if (cellDelta <= worldDelta)
            {
                Detected = Space.Cell;
                LogVerdict($"CELL space (first=({first.x:F1},{first.y:F1}) matches start cell " +
                           $"({startCell.x:F1},{startCell.y:F1})). Converting to world space — " +
                           "north/south in on-foot routes was inverted before this build.");
            }
            else
            {
                Detected = Space.World;
                LogVerdict($"WORLD space (first=({first.x:F1},{first.y:F1}) matches start world " +
                           $"({startWorldX:F1},{startWorldY:F1})). No conversion needed.");
            }
        }

        private static void LogVerdict(string detail)
        {
            if (logged) return;
            logged = true;
            RoutingAdapter.Log($"[Routing] MapRouteSearcher returns {detail}");
        }

        /// <summary>
        /// Returns the path in world space, converting only if the game's searcher was found
        /// to work in cell space. Until the space is known the input is passed through
        /// unchanged, which matches the mod's behaviour before this existed.
        /// </summary>
        internal static List<Vector3> ToWorld(List<Vector3> path, int mapWidth, int mapHeight)
        {
            if (Detected != Space.Cell || path == null)
                return path;

            var converted = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                converted.Add(new Vector3(
                    (path[i].x - mapWidth * 0.5f) * RoutingAdapter.TileSize,
                    (mapHeight * 0.5f - path[i].y) * RoutingAdapter.TileSize,
                    0f));
            }
            return converted;
        }
    }
}
