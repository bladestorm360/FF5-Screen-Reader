using System;
using MelonLoader;
using UnityEngine;
using Il2CppLast.Map;
using MapFieldController = Il2CppLast.Map.FieldController;

namespace FFV_ScreenReader.Field
{
    /// <summary>
    /// Pathfinding through hidden passages.
    ///
    /// The game routes on MapModel.CurrentMappingData, which FieldController.SetupCurrentMappingData
    /// builds as CollisionMappingData AND HiddenPassageMappingData. Every hidden-passage cell is
    /// therefore closed to MapRouteSearcher, although the player walks it: player movement uses the
    /// collision layer alone. The passages come from each sub-map's hidden_passage asset (40 sub-maps
    /// in FF5), and tools/find_hidden.py lists what sits behind them (22 chests, the piano and
    /// several story triggers among others).
    ///
    /// When the normal on-foot search finds nothing, FieldNavigationHelper runs it once more
    /// through SearchWithPassagesOpen: the route grid is swapped for the collision grid for the
    /// duration of that one synchronous search, then put back. Maps without passages are left
    /// untouched, and vehicle routing never reaches this (no world map has passages).
    /// </summary>
    internal static class HiddenPassageRouting
    {
        private static bool loggedFailure;

        /// <summary>
        /// Runs <paramref name="search"/> with hidden passages open. Returns null when the current
        /// map has no hidden passages (nothing to retry) or the map model is unavailable.
        /// </summary>
        public static Il2CppSystem.Collections.Generic.List<Vector3> SearchWithPassagesOpen(
            IMapAccessor mapHandle, Func<Il2CppSystem.Collections.Generic.List<Vector3>> search)
        {
            MapModel model = GetMapModel(mapHandle);
            if (model == null)
                return null;

            var passages = model.HiddenPassageMappingData;
            var collision = model.CollisionMappingData;
            var current = model.CurrentMappingData;
            if (passages == null || collision == null || current == null)
                return null;

            model.SetCurrentMappingData(collision);
            try
            {
                return search();
            }
            finally
            {
                model.SetCurrentMappingData(current);
            }
        }

        private static MapModel GetMapModel(IMapAccessor mapHandle)
        {
            try
            {
                var controller = mapHandle?.TryCast<MapFieldController>();
                return controller?.mapManager?.CurrentMapModel;
            }
            catch (Exception ex)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    MelonLogger.Warning($"[Routing] Hidden-passage routing unavailable ({ex.GetType().Name}: {ex.Message}); " +
                                        "targets behind secret passages will report no path");
                }
                return null;
            }
        }
    }
}
