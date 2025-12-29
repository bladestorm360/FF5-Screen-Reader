using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using UnityEngine;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patch ChangeMoveState to announce when entering/exiting ship control and other movement modes
    /// </summary>
    [HarmonyPatch(typeof(FieldPlayer), nameof(FieldPlayer.ChangeMoveState))]
    public static class FieldPlayer_ChangeMoveState_Patch
    {
        private static int lastMoveState = -1;

        [HarmonyPostfix]
        public static void Postfix(FieldPlayer __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                int currentMoveState = (int)__instance.moveState;

                // Only announce if state actually changed
                if (currentMoveState != lastMoveState)
                {
                    // Update cached state in MoveStateHelper (this will also announce the change)
                    MoveStateHelper.UpdateCachedMoveState(currentMoveState);
                    lastMoveState = currentMoveState;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MoveState] Error in ChangeMoveState patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Proactive state monitoring for world map contexts (where ships/vehicles are available)
    /// Uses coroutine to check state every 0.5 seconds, announcing changes immediately
    /// </summary>
    public static class MoveStateMonitor
    {
        private static object stateMonitorCoroutine = null;
        private static int lastKnownState = -1;
        private const float STATE_CHECK_INTERVAL = 0.5f;

        /// <summary>
        /// Coroutine that monitors move state changes every 0.5 seconds
        /// Only runs when on world map (where ships/vehicles are available)
        /// </summary>
        private static IEnumerator MonitorMoveStateChanges()
        {
            while (true)
            {
                yield return new WaitForSeconds(STATE_CHECK_INTERVAL);

                try
                {
                    // Read current state and detect changes
                    int currentState = MoveStateHelper.GetCurrentMoveState();

                    // Only announce if state actually changed (skip initial -1 state)
                    if (currentState != lastKnownState && lastKnownState != -1)
                    {
                        MoveStateHelper.AnnounceStateChange(lastKnownState, currentState);
                    }

                    lastKnownState = currentState;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MoveState] Error in state monitoring coroutine: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Start state monitoring coroutine when entering world map
        /// </summary>
        public static void StartStateMonitoring()
        {
            if (stateMonitorCoroutine == null)
            {
                lastKnownState = -1; // Reset state tracking
                stateMonitorCoroutine = MelonCoroutines.Start(MonitorMoveStateChanges());
            }
        }

        /// <summary>
        /// Stop state monitoring coroutine when leaving world map
        /// </summary>
        public static void StopStateMonitoring()
        {
            if (stateMonitorCoroutine != null)
            {
                MelonCoroutines.Stop(stateMonitorCoroutine);
                stateMonitorCoroutine = null;
                lastKnownState = -1;
            }
        }
    }
}
