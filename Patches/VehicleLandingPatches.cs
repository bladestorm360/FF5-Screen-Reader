using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Map;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces when player enters a zone where vehicle can land.
    /// Patches MapUIManager.SwitchLandable which is called by the game
    /// when the landing state changes based on terrain under the vehicle.
    /// </summary>
    [HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.SwitchLandable))]
    public static class MapUIManager_SwitchLandable_Patch
    {
        private static bool lastLandableState = false;

        [HarmonyPostfix]
        public static void Postfix(bool landable)
        {
            try
            {
                // Only announce when in a vehicle (not on foot)
                if (MoveStateHelper.IsOnFoot())
                    return;

                // Only announce when entering landable zone (false -> true)
                if (landable && !lastLandableState)
                {
                    Core.FFV_ScreenReaderMod.SpeakText("Can land", interrupt: false);
                }

                lastLandableState = landable;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Landing] Error in SwitchLandable patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state when leaving vehicle or changing maps
        /// </summary>
        public static void ResetState()
        {
            lastLandableState = false;
        }
    }
}
