using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using FFV_ScreenReader.Utils;
using FFV_ScreenReader.Core;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches FieldPlayer.GetOn/GetOff and FieldController.ChangeTransportation
    /// for immediate vehicle boarding/disembarking announcements.
    /// Also hooks MainGame.set_FieldReady for entity scan on field ready
    /// and map transition detection (replaces ChangeState hook).
    ///
    /// FF5 Vehicles:
    /// - Ship (TRANSPORT_SHIP)
    /// - Airship (TRANSPORT_PLANE)
    /// - Submarine (TRANSPORT_SUBMARINE)
    /// - Chocobo (TRANSPORT_YELLOW_CHOCOBO, TRANSPORT_BLACK_CHOCOBO)
    /// - Wind Drake/Hiryuu (TRANSPORT_LOWFLYING or TRANSPORT_SPECIAL_PLANE)
    /// </summary>
    public static class MovementSpeechPatches
    {
        private static bool isPatched = false;

        // TransportationType enum values (from MapConstants.TransportationType)
        private const int TRANSPORT_NONE = 0;
        private const int TRANSPORT_PLAYER = 1;
        private const int TRANSPORT_SHIP = 2;
        private const int TRANSPORT_PLANE = 3;
        private const int TRANSPORT_SYMBOL = 4;
        private const int TRANSPORT_CONTENT = 5;
        private const int TRANSPORT_SUBMARINE = 6;
        private const int TRANSPORT_LOWFLYING = 7;
        private const int TRANSPORT_SPECIAL_PLANE = 8;
        private const int TRANSPORT_YELLOW_CHOCOBO = 9;
        private const int TRANSPORT_BLACK_CHOCOBO = 10;
        private const int TRANSPORT_BOKO = 11;
        private const int TRANSPORT_MAGICAL_ARMOR = 12;

        // Track previous transportation for change detection
        private static int lastTransportationId = TRANSPORT_PLAYER;
        private static int lastAnnouncedTransportId = -1;

        // Track intermediate state for scripted vehicle transitions
        private static bool wasInIntermediateState = false;
        private static int preIntermediateTransportId = TRANSPORT_PLAYER;

        /// <summary>
        /// Callback to trigger entity scan when field is ready.
        /// Set by FFV_ScreenReaderMod during initialization.
        /// </summary>
        public static Action OnFieldReady;

        /// <summary>
        /// Apply manual Harmony patches for vehicle hooks and field ready.
        /// Called from FFV_ScreenReaderMod initialization.
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            if (isPatched)
                return;

            try
            {
                TryPatchGetOn(harmony);
                TryPatchGetOff(harmony);
                TryPatchChangeTransportation(harmony);
                TryPatchFieldReady(harmony);
                isPatched = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MoveState] Error applying patches: {ex.Message}");
            }
        }

        private static void TryPatchFieldReady(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type mainGameType = typeof(MainGame);
                MethodInfo targetMethod = null;

                foreach (var method in mainGameType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "set_FieldReady")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                        {
                            targetMethod = method;
                            break;
                        }
                    }
                }

                if (targetMethod != null)
                {
                    var postfix = typeof(MovementSpeechPatches).GetMethod(nameof(FieldReady_Postfix),
                        BindingFlags.Public | BindingFlags.Static);

                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[FieldReady] Could not find MainGame.set_FieldReady method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FieldReady] Error patching set_FieldReady: {ex.Message}");
            }
        }

        public static void FieldReady_Postfix(bool value)
        {
            try
            {
                if (value && !GameStatePatches.IsInEventState)
                {
                    GameStatePatches.CheckMapTransition();
                    OnFieldReady?.Invoke();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[FieldReady] Error in set_FieldReady postfix: {ex.Message}");
            }
        }

        private static void TryPatchChangeTransportation(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type fieldControllerType = typeof(FieldController);
                MethodInfo targetMethod = null;

                foreach (var method in fieldControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "ChangeTransportation")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(int))
                        {
                            targetMethod = method;
                            break;
                        }
                    }
                }

                if (targetMethod != null)
                {
                    var postfix = typeof(MovementSpeechPatches).GetMethod(nameof(ChangeTransportation_Postfix),
                        BindingFlags.Public | BindingFlags.Static);

                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[MoveState] Could not find ChangeTransportation method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MoveState] Error patching ChangeTransportation: {ex.Message}");
            }
        }

        public static void ChangeTransportation_Postfix(FieldController __instance, int transportationId)
        {
            try
            {
                if (transportationId == lastAnnouncedTransportId)
                    return;

                // Resolve actual TransportationType from the dictionary index
                int resolvedType = ResolveTransportationType(__instance, transportationId);

                int previousId = lastTransportationId;
                lastTransportationId = transportationId;

                // Handle intermediate states (TRANSPORT_NONE, TRANSPORT_SYMBOL, TRANSPORT_CONTENT, -1)
                // These occur during scripted vehicle transitions like pirate ship disembark
                // FF5 uses -1 as intermediate state: Ship(2) -> -1 -> Player(1)
                if (IsIntermediateTransportation(resolvedType))
                {
                    if (!wasInIntermediateState && !IsIntermediateTransportation(previousId))
                    {
                        wasInIntermediateState = true;
                        preIntermediateTransportId = previousId;
                    }
                    Core.FFV_ScreenReaderMod.SuppressWallTonesForTransition();
                    return;
                }

                // Coming out of intermediate state - check BOTH flag AND if previousId was intermediate
                if (wasInIntermediateState || IsIntermediateTransportation(previousId))
                {
                    int actualPreviousState = wasInIntermediateState ? preIntermediateTransportId : TRANSPORT_PLAYER;
                    wasInIntermediateState = false;

                    // Check if we transitioned from vehicle to on-foot through intermediate state
                    if (IsVehicleTransportation(actualPreviousState) && resolvedType == TRANSPORT_PLAYER)
                    {
                        lastAnnouncedTransportId = transportationId;
                        MoveStateHelper.SetOnFoot();
                        Core.FFV_ScreenReaderMod.SpeakText("On foot", interrupt: false);
                        return;
                    }

                    // Skip further processing if we came from an intermediate state without vehicle context
                    if (IsIntermediateTransportation(previousId))
                        return;
                }

                bool wasOnVehicle = IsVehicleTransportation(previousId);
                bool isOnVehicle = IsVehicleTransportation(resolvedType);
                bool isNowOnFoot = (resolvedType == TRANSPORT_PLAYER);

                string announcement = null;

                if (!wasOnVehicle && isOnVehicle)
                {
                    string vehicleName = GetTransportationName(resolvedType);
                    if (!string.IsNullOrEmpty(vehicleName))
                    {
                        announcement = $"On {vehicleName}";
                        MoveStateHelper.SetVehicleState(resolvedType);
                    }
                }
                else if (wasOnVehicle && isNowOnFoot)
                {
                    announcement = "On foot";
                    MoveStateHelper.SetOnFoot();
                }

                if (announcement != null)
                {
                    lastAnnouncedTransportId = transportationId;
                    Core.FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MoveState] Error in ChangeTransportation patch: {ex.Message}");
            }
        }

        private static int ResolveTransportationType(FieldController fieldController, int transportationId)
        {
            try
            {
                var transportController = fieldController?.transportation;
                if (transportController != null)
                {
                    return transportController.GetTransportationType(transportationId);
                }
            }
            catch { }
            return transportationId; // fallback: assume it IS the type
        }

        private static bool IsIntermediateTransportation(int transportationId)
        {
            return transportationId < 0 ||
                   transportationId == TRANSPORT_NONE ||
                   transportationId == TRANSPORT_SYMBOL ||
                   transportationId == TRANSPORT_CONTENT;
        }

        private static bool IsVehicleTransportation(int transportationId)
        {
            return transportationId != TRANSPORT_NONE &&
                   transportationId != TRANSPORT_PLAYER &&
                   transportationId != TRANSPORT_SYMBOL &&
                   transportationId != TRANSPORT_CONTENT;
        }

        private static void TryPatchGetOn(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type fieldPlayerType = typeof(FieldPlayer);
                MethodInfo targetMethod = null;

                foreach (var method in fieldPlayerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "GetOn")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(int))
                        {
                            targetMethod = method;
                            break;
                        }
                    }
                }

                if (targetMethod != null)
                {
                    var postfix = typeof(MovementSpeechPatches).GetMethod(nameof(GetOn_Postfix),
                        BindingFlags.Public | BindingFlags.Static);

                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[MoveState] Could not find GetOn method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MoveState] Error patching GetOn: {ex.Message}");
            }
        }

        private static void TryPatchGetOff(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type fieldPlayerType = typeof(FieldPlayer);
                MethodInfo targetMethod = null;

                foreach (var method in fieldPlayerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "GetOff")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(int))
                        {
                            targetMethod = method;
                            break;
                        }
                    }
                }

                if (targetMethod != null)
                {
                    var postfix = typeof(MovementSpeechPatches).GetMethod(nameof(GetOff_Postfix),
                        BindingFlags.Public | BindingFlags.Static);

                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[MoveState] Could not find GetOff method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MoveState] Error patching GetOff: {ex.Message}");
            }
        }

        public static void GetOn_Postfix(int typeId)
        {
            try
            {
                string vehicleName = GetTransportationName(typeId);
                if (!string.IsNullOrEmpty(vehicleName))
                {
                    MoveStateHelper.SetVehicleState(typeId);
                    lastAnnouncedTransportId = typeId;
                    lastTransportationId = typeId;
                    wasInIntermediateState = false;
                    Core.FFV_ScreenReaderMod.SuppressWallTonesForTransition();
                    Core.FFV_ScreenReaderMod.SpeakText($"On {vehicleName}", interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MoveState] Error in GetOn patch: {ex.Message}");
            }
        }

        public static void GetOff_Postfix(int typeId)
        {
            try
            {
                wasInIntermediateState = false;
                MoveStateHelper.SetOnFoot();
                lastAnnouncedTransportId = TRANSPORT_PLAYER;
                lastTransportationId = TRANSPORT_PLAYER;
                Core.FFV_ScreenReaderMod.SuppressWallTonesForTransition();
                Core.FFV_ScreenReaderMod.SpeakText("On foot", interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MoveState] Error in GetOff patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Get human-readable name for FF5 TransportationType.
        /// </summary>
        private static string GetTransportationName(int typeId)
        {
            switch (typeId)
            {
                case TRANSPORT_SHIP:
                    return "ship";
                case TRANSPORT_PLANE:
                    return "airship";
                case TRANSPORT_SUBMARINE:
                    return "submarine";
                case TRANSPORT_SPECIAL_PLANE:
                    return "airship";
                case TRANSPORT_LOWFLYING:
                    return "wind drake";
                case TRANSPORT_YELLOW_CHOCOBO:
                    return "chocobo";
                case TRANSPORT_BLACK_CHOCOBO:
                    return "black chocobo";
                case TRANSPORT_BOKO:
                    return "chocobo";
                case TRANSPORT_MAGICAL_ARMOR:
                    return "magical armor";
                default:
                    return null;
            }
        }

        public static void ResetState()
        {
            lastTransportationId = TRANSPORT_PLAYER;
            lastAnnouncedTransportId = -1;
            wasInIntermediateState = false;
            preIntermediateTransportId = TRANSPORT_PLAYER;
            MoveStateHelper.ResetState();
            FieldPlayer_ChangeMoveState_Patch.ResetState();
        }

        public static void SyncToOnFoot()
        {
            lastTransportationId = TRANSPORT_PLAYER;
            lastAnnouncedTransportId = TRANSPORT_PLAYER;
        }
    }

    /// <summary>
    /// Backup patch for ChangeMoveState - fires when moveState changes.
    /// Catches state transitions that bypass GetOn/GetOff/ChangeTransportation.
    /// This is the most reliable hook for scripted vehicle events like pirate ship disembark.
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

                // Suppress move state announcements during events
                if (GameStatePatches.IsInEventState)
                    return;

                int currentMoveState = (int)__instance.moveState;

                if (currentMoveState != lastMoveState)
                {
                    int previousState = lastMoveState;
                    lastMoveState = currentMoveState;

                    if (previousState == -1)
                        return;

                    MoveStateHelper.AnnounceStateChange(previousState, currentMoveState);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MoveState] Error in ChangeMoveState patch: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Resets the state tracking. Called on map transitions.
        /// </summary>
        public static void ResetState()
        {
            lastMoveState = -1;
        }
    }
}
