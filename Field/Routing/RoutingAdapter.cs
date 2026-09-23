using System;
using Il2CppLast.Map;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Field.Routing
{
    /// <summary>
    /// Every game-specific fact the routing searchers depend on, in one place.
    ///
    /// PORTING: this is the only file in Field/Routing/ expected to change when this
    /// folder is copied to a sibling FFPR mod (beyond the namespace line). The searchers
    /// themselves contain no game constants, no speech, no localization and no logging
    /// calls — see the port checklist in docs/debug.md.
    /// </summary>
    internal static class RoutingAdapter
    {
        // =====================================================================
        // Tile geometry
        // =====================================================================

        /// <summary>World units per map cell. FF5 = 16. Do NOT assume 16 in other games.</summary>
        public const float TileSize = GameConstants.TILE_SIZE;
        public const float TileSizeInverse = GameConstants.TILE_SIZE_INVERSE;

        // =====================================================================
        // No struct offsets.
        //
        // This file used to carry TransportationController.playerTransport at 0x30, read to
        // answer "could the player walk here?" for the vehicle-requirement check. That check
        // was removed (see BreadcrumbRouteChainer), and with it the offset. Everything the
        // routing code needs now comes from public API, so a port to a sibling game has no
        // per-build addresses to re-derive — keep it that way.
        // =====================================================================

        // =====================================================================
        // Map identity
        // =====================================================================

        public static bool IsWorldMap(int mapId) => GameConstants.IsWorldMap(mapId);

        // Loop flags, read from the game once per map by RefreshMapInfo below.
        private static int loopMapId = -1;
        private static bool loopWrapX;
        private static bool loopWrapY;

        /// <summary>
        /// Reads the map's real loop type and caches it. Called from EnsureGrid, which is the
        /// only thing that runs before a flood, so the wrap flags are always populated by the
        /// time TryVisit reads them.
        ///
        /// Also cross-checks the mod's own world-map id table against the game's answer. A
        /// mismatch does not change behaviour — it just means GameConstants.IsWorldMap is
        /// missing an id, which is worth seeing in a log rather than debugging blind.
        /// </summary>
        public static void RefreshMapInfo(FieldController fieldController, int mapId)
        {
            if (loopMapId == mapId) return;

            // Fall back to the old heuristic, so a failed read degrades to previous behaviour
            // rather than silently disabling wrap on a map that does loop.
            bool wrapX = IsWorldMap(mapId);
            bool wrapY = wrapX;

            try
            {
                var mapModel = fieldController?.mapManager?.CurrentMapModel;
                if (mapModel != null)
                {
                    // MapConstants.LoopType: None=0, Horizontal=1, Vertical=2, All=3
                    int loopType = mapModel.GetLoopType();
                    wrapX = loopType == 1 || loopType == 3;
                    wrapY = loopType == 2 || loopType == 3;

                    Log($"[Routing] Map {mapId} loop type {loopType} (wrapX={wrapX}, wrapY={wrapY})");
                }

                if (fieldController != null && fieldController.CheckCurrentWorldMap() != IsWorldMap(mapId))
                {
                    WarnOnce($"worldmap-table-{mapId}",
                        $"Map {mapId}: game says world map = {fieldController.CheckCurrentWorldMap()}, " +
                        $"GameConstants.IsWorldMap says {IsWorldMap(mapId)}");
                }
            }
            catch (Exception ex)
            {
                WarnOnce("map-info", $"Loop type read failed, falling back to the world-map heuristic: {ex.Message}");
            }

            loopMapId = mapId;
            loopWrapX = wrapX;
            loopWrapY = wrapY;
        }

        /// <summary>
        /// World-map edge wrapping. FF5's overworld loops, so a route may legitimately
        /// leave one edge and arrive at the other. Sourced from MapModel.GetLoopType via
        /// RefreshMapInfo; the world-map heuristic is only the fallback now.
        /// </summary>
        public static bool WorldMapWrapsX(int mapId) =>
            loopMapId == mapId ? loopWrapX : IsWorldMap(mapId);

        public static bool WorldMapWrapsY(int mapId) =>
            loopMapId == mapId ? loopWrapY : IsWorldMap(mapId);

        public static bool IsOnFoot() => MoveStateHelper.IsOnFoot();

        // =====================================================================
        // Transport identity
        // =====================================================================

        /// <summary>
        /// The transportation id of the vehicle the player is currently riding.
        /// Mirrors how GetNearbyLandingSpots resolves it, so landing pings and routing
        /// can never disagree about which vehicle is active.
        /// </summary>
        public static bool TryGetCurrentTransportId(TransportationController tc, out int id)
        {
            id = -1;
            if (tc == null) return false;

            try
            {
                var current = tc.CurrentTransportation;
                if (current != null)
                {
                    id = current.Id;
                    return true;
                }
            }
            catch (Exception ex)
            {
                WarnOnce("current-transport", $"CurrentTransportation read failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// True when this transport moves analog rather than on the 4-way movement grid, so
        /// its routes may use true diagonals.
        ///
        /// The airship and the wind drake fly with free directional input — the game even
        /// swaps in a dedicated FieldPlayerKeyAirshipController for them — so routing them
        /// on 4 neighbours both overstates the distance (70 steps where 40 will do) and
        /// produces an L-shape the player would never actually fly. Everything else, walking
        /// included, is grid-locked to north/south/east/west.
        /// </summary>
        public static bool AllowsDiagonalMovement(TransportationController tc, int transportId)
        {
            if (tc == null) return false;

            try
            {
                int type = tc.GetTransportationType(transportId);
                return type == MoveStateHelper.TRANSPORT_PLANE
                    || type == MoveStateHelper.TRANSPORT_LOWFLYING
                    || type == MoveStateHelper.TRANSPORT_SPECIAL_PLANE;
            }
            catch (Exception ex)
            {
                // A wrong answer here only costs route quality, never correctness, so the
                // grid-locked assumption is the safe one to fall back to.
                WarnOnce("diagonal-type", $"Transport type read failed, routing 4-way: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // Logging hook — the searchers never reference MelonLogger directly
        // =====================================================================

        public static void Log(string message) => MelonLogger.Msg(message);
        public static void Warn(string message) => MelonLogger.Warning(message);

        private static readonly System.Collections.Generic.HashSet<string> warnedKeys
            = new System.Collections.Generic.HashSet<string>();

        public static void WarnOnce(string key, string message)
        {
            if (!warnedKeys.Add(key)) return;
            MelonLogger.Warning($"[Routing] {message}");
        }

        /// <summary>
        /// Keyed one-shot at Msg level. Routing runs inside a 2 s beacon loop and a
        /// per-entity filter, so an ungated line here becomes a log flood.
        ///
        /// Keyed rather than a plain bool latch because the interesting lines vary within a
        /// map — the flood line has to fire again when the player swaps ship for airship, or
        /// the log never shows the run you are actually trying to read.
        /// </summary>
        public static void LogOnce(string key, string message)
        {
            if (!warnedKeys.Add(key)) return;
            MelonLogger.Msg(message);
        }

        /// <summary>Clears one-shot log latches. Called on map transition.</summary>
        public static void ResetLogLatches()
        {
            warnedKeys.Clear();
        }
    }
}
