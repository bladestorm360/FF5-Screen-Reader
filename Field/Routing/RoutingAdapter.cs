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

        /// <summary>
        /// World-map edge wrapping. FF5's overworld loops, so a route may legitimately
        /// leave one edge and arrive at the other. Verified in-game by pathing across the
        /// seam — if a route takes the long way round instead, these are wrong.
        /// </summary>
        public static bool WorldMapWrapsX(int mapId) => IsWorldMap(mapId);
        public static bool WorldMapWrapsY(int mapId) => IsWorldMap(mapId);

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

        // =====================================================================
        // Logging hook — the searchers never reference MelonLogger directly
        // =====================================================================

        public static void Log(string message) => MelonLogger.Msg(message);
        public static void Warn(string message) => MelonLogger.Warning(message);

        /// <summary>
        /// Fires once per flag. Routing runs inside a 2 s beacon loop and a per-entity
        /// filter, so an ungated log line here becomes a log flood.
        /// </summary>
        public static void LogOnce(ref bool flag, string message)
        {
            if (flag) return;
            flag = true;
            MelonLogger.Msg(message);
        }

        private static readonly System.Collections.Generic.HashSet<string> warnedKeys
            = new System.Collections.Generic.HashSet<string>();

        public static void WarnOnce(string key, string message)
        {
            if (!warnedKeys.Add(key)) return;
            MelonLogger.Warning($"[Routing] {message}");
        }

        /// <summary>Clears one-shot log latches. Called on map transition.</summary>
        public static void ResetLogLatches()
        {
            warnedKeys.Clear();
        }
    }
}
