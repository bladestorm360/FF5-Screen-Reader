using System;
using Il2CppLast.Map;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Field.Routing
{
    /// <summary>
    /// Every game-specific fact the routing code depends on, in one place.
    ///
    /// PORTING: this is the only file in Field/Routing/ expected to change when this
    /// folder is copied to a sibling FFPR mod (beyond the namespace line). The breadcrumb
    /// chainer contains no game constants, no speech, no localization and no logging
    /// calls — see the port checklist in docs/debug.md.
    ///
    /// The vehicle route searcher and its map/loop/diagonal helpers were removed on
    /// 2026-10-03 (user: vehicle routing did not work); vehicles use the game's searcher.
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
        // Transport identity
        // =====================================================================

        /// <summary>
        /// The transportation id of the vehicle the player is currently riding (landing pings).
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

        private static readonly System.Collections.Generic.HashSet<string> warnedKeys
            = new System.Collections.Generic.HashSet<string>();

        public static void WarnOnce(string key, string message)
        {
            if (!warnedKeys.Add(key)) return;
            MelonLogger.Warning($"[Routing] {message}");
        }
    }
}
