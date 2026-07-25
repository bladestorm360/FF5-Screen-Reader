using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Message;
using Il2CppLast.Battle;
using Il2CppLast.Battle.Function;
using Il2CppLast.Data.Master;
using Il2CppLast.Systems;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using UnityEngine;
using BattleCommandMessageController_KeyInput = Il2CppLast.UI.KeyInput.BattleCommandMessageController;
using BattleCommandMessageController_Touch = Il2CppLast.UI.Touch.BattleCommandMessageController;
using BattlePlayerData = Il2Cpp.BattlePlayerData;
using Il2CppLast.Data.User;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for message display methods - View layer and scrolling messages in Battle
    /// </summary>

    [HarmonyPatch(typeof(ScrollMessageManager), nameof(ScrollMessageManager.Play))]
    public static class ScrollMessageManager_Play_Patch
    {
        // ScrollMessageManager.Play re-fires with the same text while a scroll message is on
        // screen, so hold the last one spoken. Cleared each turn by SetCommandSelectTarget so a
        // repeated system message ("Preemptive Strike" two battles running) still announces.
        private static string _lastScrollMessage;

        /// <summary>Clears the last scroll message so it can be announced again.</summary>
        public static void ResetLastMessage() => _lastScrollMessage = null;

        [HarmonyPostfix]
        public static void Postfix(ScrollMessageClient.ScrollType type, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                string cleanMessage = message.Trim();
                if (cleanMessage == _lastScrollMessage) return;
                _lastScrollMessage = cleanMessage;

                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ScrollMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch ParameterActFunctionManagment.CreateActFunction to announce actor names with their actions
    /// This is called when a battle action function is being created (before execution)
    /// </summary>
    [HarmonyPatch(typeof(ParameterActFunctionManagment), nameof(ParameterActFunctionManagment.CreateActFunction))]
    public static class ParameterActFunctionManagment_CreateActFunction_Patch
    {
        // Two guards, different jobs:
        //  _lastActDataPtr  — CreateActFunction is invoked more than once for the SAME BattleActData
        //                     (per-target / per-hit function creation). Keyed on the INSTANCE, not the
        //                     text, so two goblins both attacking produce distinct pointers and both
        //                     announce (a string compare would swallow the second).
        //  _lastSpokenActorName — ally dual-wield: a second, different BattleActData for the same ally
        //                     swinging again. Only suppressed for direct attacks.
        private static IntPtr _lastActDataPtr = IntPtr.Zero;
        private static string _lastSpokenActorName;

        /// <summary>Clears both guards so a repeated action announces fresh next turn.</summary>
        public static void ResetLastAction()
        {
            _lastActDataPtr = IntPtr.Zero;
            _lastSpokenActorName = null;
        }

        [HarmonyPostfix]
        public static void Postfix(BattleActData battleActData)
        {
            try
            {
                if (battleActData == null) return;

                // Get the attacker's name
                string actorName = GetActorName(battleActData);

                // Get the action/ability name
                string actionName = GetActionName(battleActData);

                if (!string.IsNullOrEmpty(actorName))
                {
                    if (battleActData.Pointer == _lastActDataPtr) return;
                    _lastActDataPtr = battleActData.Pointer;

                    string announcement;
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        string actionLower = actionName.ToLower();
                        if (actionLower == "attack" || actionLower == "fight")
                        {
                            announcement = string.Format(T("{0} attacks"), actorName);
                        }
                        else if (actionLower == "defend" || actionLower == "guard")
                        {
                            announcement = string.Format(T("{0} defends"), actorName);
                        }
                        else if (actionLower == "item")
                        {
                            announcement = string.Format(T("{0} uses item"), actorName);
                        }
                        else
                        {
                            announcement = $"{actorName}, {actionName}";
                        }
                    }
                    else
                    {
                        announcement = string.Format(T("{0} attacks"), actorName);
                    }

                    // Ally dual-wield suppression: same ally name + direct attack = redundant second swing
                    string lowerAction = actionName?.ToLower();
                    bool isDirectAttack = string.IsNullOrEmpty(actionName)
                        || lowerAction == "attack" || lowerAction == "fight";
                    bool isAlly = battleActData.AttackUnitData?.TryCast<Il2Cpp.BattlePlayerData>() != null;

                    if (isAlly && isDirectAttack && actorName == _lastSpokenActorName)
                        return;

                    _lastSpokenActorName = actorName;

                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in CreateActFunction patch: {ex.Message}");
            }
        }

        private static string GetActorName(BattleActData battleActData)
        {
            try
            {
                var attackUnit = battleActData.AttackUnitData;
                if (attackUnit == null) return null;

                return BattleUnitHelper.GetUnitName(attackUnit);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting actor name: {ex.Message}");
            }

            return null;
        }

        private static string GetActionName(BattleActData battleActData)
        {
            try
            {
                // Try to get the ability name first (spells, skills)
                var abilityList = battleActData.abilityList;
                if (abilityList != null && abilityList.Count > 0)
                {
                    var ability = abilityList[0];
                    if (ability != null)
                    {
                        // Use ContentUtitlity to get the localized ability name directly
                        string abilityName = ContentUtitlity.GetAbilityName(ability);
                        if (!string.IsNullOrEmpty(abilityName))
                        {
                            return abilityName;
                        }
                    }
                }

                // Fall back to command name (Attack, Defend, etc.)
                var command = battleActData.Command;
                if (command != null)
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null)
                    {
                        string commandMesId = command.MesIdName;
                        if (!string.IsNullOrEmpty(commandMesId))
                        {
                            string localizedName = messageManager.GetMessage(commandMesId);
                            if (!string.IsNullOrEmpty(localizedName))
                            {
                                return localizedName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting action name: {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Captures the on-screen multi-hit "×N" multiplier from DamageViewUIManager.CreateHitCount, which
    /// fires just before the matching CreateDamageView. Lets the damage announce optionally prepend it
    /// (e.g. "14x1552 damage") when the Multi-hit Damage setting is "With hit count". The value is
    /// consumed and reset to 1 by BattleBasicFunction_CreateDamageView_Patch.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.DamageViewUIManager), nameof(Il2CppLast.UI.DamageViewUIManager.CreateHitCount))]
    public static class DamageViewUIManager_CreateHitCount_Patch
    {
        // Multi-hit multiplier awaiting the next CreateDamageView. 1 = single hit (default / after consume).
        public static int PendingHitCount = 1;
        // Frame the multiplier was captured on. Used to reject a stale count that was never
        // consumed by a CreateDamageView (e.g. a fully-evaded multi-hit) so it can't leak into
        // an unrelated later attack's damage announcement.
        public static int PendingHitCountFrame = -1;

        [HarmonyPostfix]
        public static void Postfix(int hitCountValue)
        {
            PendingHitCount = hitCountValue;
            PendingHitCountFrame = UnityEngine.Time.frameCount;
        }
    }

    [HarmonyPatch(typeof(Il2CppLast.Battle.Function.BattleBasicFunction), nameof(Il2CppLast.Battle.Function.BattleBasicFunction.CreateDamageView))]
    public static class BattleBasicFunction_CreateDamageView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData data, int value, Il2CppLast.Systems.HitType hitType, bool isRecovery, Il2CppLast.Systems.CalcResult.MissType missType)
        {
            try
            {
                string targetName = BattleUnitHelper.GetUnitName(data) ?? T("Unknown");

                // Consume the multi-hit "×N" multiplier captured by CreateHitCount (fires just before
                // this view, on the same or adjacent frame). Reject a stale count from an earlier
                // action that never produced a damage view, then reset to 1 so a later damage with no
                // fresh hit count defaults to single.
                bool fresh = UnityEngine.Time.frameCount - DamageViewUIManager_CreateHitCount_Patch.PendingHitCountFrame <= 1;
                int hitCount = fresh ? DamageViewUIManager_CreateHitCount_Patch.PendingHitCount : 1;
                DamageViewUIManager_CreateHitCount_Patch.PendingHitCount = 1;

                string message;
                if (hitType == Il2CppLast.Systems.HitType.Miss)
                {
                    // NonView = non-damage ability (Steal, Focus, etc.) — game doesn't show "Miss" visually
                    if (missType == Il2CppLast.Systems.CalcResult.MissType.NonView)
                        return;

                    message = string.Format(T("{0}: Miss"), targetName);
                }
                else if (hitType == Il2CppLast.Systems.HitType.Recovery)
                {
                    message = string.Format(T("{0}: Recovered {1} HP"), targetName, value);
                }
                else if (hitType == Il2CppLast.Systems.HitType.MPRecovery)
                {
                    message = string.Format(T("{0}: Recovered {1} MP"), targetName, value);
                }
                else
                {
                    // HP damage — optionally prepend the multi-hit "{N}x" multiplier to the value
                    // (e.g. "14x1552") when the Multi-hit Damage setting is "With hit count".
                    string valueText = (PreferencesManager.DamageDisplay == 1 && hitCount > 1)
                        ? $"{hitCount}x{value}"
                        : value.ToString();
                    message = string.Format(T("{0}: {1} damage"), targetName, valueText);
                }

                // Announce damage/recovery
                FFV_ScreenReaderMod.SpeakText(message, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleBasicFunction.CreateDamageView patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch SetCommandSelectTarget to reset target tracking when a new character's turn begins.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetCommandSelectTarget))]
    public static class BattleMenuController_SetCommandSelectTarget_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattlePlayerData targetData)
        {
            try
            {
                // New turn: clear every "already said this" guard so the same message,
                // action or status can be announced again this turn.
                BattleTargetPatches.ResetState();
                ScrollMessageManager_Play_Patch.ResetLastMessage();
                ParameterActFunctionManagment_CreateActFunction_Patch.ResetLastAction();
                BattleConditionController_Add_Patch.ResetLastCondition();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetCommandSelectTarget patch: {ex.Message}");
            }
        }
    }

    // Note: Removed redundant BattleUIManager and BattleMenuController patches
    // The ActFunctionProvider.ViewMessage patch now handles actor+action announcements
    // The ScrollMessageManager.Play patch handles system messages like "Preemptive Strike"
    // Each patch owns its own re-fire guard, cleared per turn by SetCommandSelectTarget above

    /// <summary>
    /// Patch BattleStealItemPlug.StealItem to announce when items are stolen
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleStealItemPlug), nameof(Il2CppLast.Battle.BattleStealItemPlug.StealItem))]
    public static class BattleStealItemPlug_StealItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int contentId, int cnt)
        {
            try
            {
                if (contentId <= 0) return;

                // Get item name from contentId using MessageManager directly
                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    MelonLogger.Warning("[Steal] MessageManager not available");
                    return;
                }

                // Get the item name message ID using ContentUtitlity
                string itemMesId = ContentUtitlity.GetMesIdItemName(contentId);
                if (!string.IsNullOrEmpty(itemMesId))
                {
                    string itemName = messageManager.GetMessage(itemMesId);
                    if (!string.IsNullOrEmpty(itemName))
                    {
                        // Remove icon markup from name
                        itemName = Utils.TextUtils.StripIconMarkup(itemName);

                        string announcement;
                        if (cnt > 1)
                        {
                            announcement = string.Format(T("Stole {0} x{1}"), itemName, cnt);
                        }
                        else
                        {
                            announcement = string.Format(T("Stole {0}"), itemName);
                        }

                        FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                        return;
                    }
                }

                // Fallback if we couldn't get the item name
                FFV_ScreenReaderMod.SpeakText(T("Stole item"), interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleStealItemPlug.StealItem patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch BattleConditionController.Add to announce status effects when applied
    /// This includes KO (UnableFight), Poison, Silence, Sleep, and all other conditions
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleConditionController), nameof(Il2CppLast.Battle.BattleConditionController.Add))]
    public static class BattleConditionController_Add_Patch
    {
        // Add() is invoked once per target of a multi-target status spell AND re-invoked while a
        // persistent condition (poison, sleep) stays applied, so hold the last "{target}: {condition}"
        // spoken. Cleared each turn by SetCommandSelectTarget so re-applying the same status to the
        // same unit on a later turn still announces.
        private static string _lastCondition;

        /// <summary>Clears the last condition so it can be announced again.</summary>
        public static void ResetLastCondition() => _lastCondition = null;

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData battleUnitData, int id)
        {
            try
            {
                if (battleUnitData == null)
                {
                    return;
                }

                // Get target name using BattleUnitHelper
                string targetName = FFV_ScreenReader.Utils.BattleUnitHelper.GetUnitName(battleUnitData) ?? T("Unknown");

                // Get condition name from ID - look up from ConfirmedConditionList
                string conditionName = null;
                try
                {
                    var unitDataInfo = battleUnitData.BattleUnitDataInfo;
                    if (unitDataInfo?.Parameter != null)
                    {
                        var confirmedList = unitDataInfo.Parameter.ConfirmedConditionList();
                        if (confirmedList != null && confirmedList.Count > 0)
                        {
                            // Look for condition matching our ID
                            foreach (var condition in confirmedList)
                            {
                                if (condition != null && condition.Id == id)
                                {
                                    string conditionMesId = condition.MesIdName;

                                    // Skip conditions with no message ID (internal/hidden statuses)
                                    if (string.IsNullOrEmpty(conditionMesId) || conditionMesId == "None")
                                    {
                                        return; // Skip this status announcement
                                    }

                                    var messageManager = MessageManager.Instance;
                                    if (messageManager != null)
                                    {
                                        string localizedConditionName = messageManager.GetMessage(conditionMesId);
                                        if (!string.IsNullOrEmpty(localizedConditionName))
                                        {
                                            conditionName = localizedConditionName;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Fallback: Announce raw ID if we couldn't resolve the name
                    if (conditionName == null)
                    {
                        conditionName = $"Status {id}";
                        MelonLogger.Warning($"[Status] Could not resolve condition ID {id}, announcing as raw ID");
                    }
                }
                catch (Exception condEx)
                {
                    MelonLogger.Warning($"Error resolving condition ID {id}: {condEx.Message}");
                    conditionName = $"Status {id}";
                }

                string announcement = $"{targetName}: {conditionName}";

                if (announcement == _lastCondition) return;
                _lastCondition = announcement;

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleConditionController.Add patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces defeat messages via BattleCommandMessageController.
    /// Uses manual Harmony patching since the types are in non-standard IL2CPP namespaces.
    /// </summary>
    public static class BattleCommandMessagePatches
    {
        private static string lastBattleCommandMessage = "";

        /// <summary>
        /// Applies manual Harmony patches for BattleCommandMessageController methods.
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                PatchBattleCommandMessage(harmony);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error applying patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a type by name across all loaded assemblies.
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.FullName == fullName)
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Patch BattleCommandMessageController.SetMessage for system messages like "The party was defeated".
        /// </summary>
        private static void PatchBattleCommandMessage(HarmonyLib.Harmony harmony)
        {
            try
            {
                // KeyInput version - uses SetMessage
                var keyInputType = FindType("Il2CppLast.UI.KeyInput.BattleCommandMessageController");
                if (keyInputType != null)
                {
                    var setMessageMethod = AccessTools.Method(keyInputType, "SetMessage");
                    if (setMessageMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setMessageMethod, postfix: new HarmonyMethod(postfix));
                    }
                    else
                    {
                        MelonLogger.Warning("[Battle Command Message] KeyInput.BattleCommandMessageController.SetMessage method not found");
                    }
                }
                else
                {
                    MelonLogger.Warning("[Battle Command Message] KeyInput.BattleCommandMessageController type not found");
                }

                // Touch version - uses SetCommandMessage and SetSystemMessage
                var touchType = FindType("Il2CppLast.UI.Touch.BattleCommandMessageController");
                if (touchType != null)
                {
                    // Patch SetCommandMessage
                    var setCommandMsgMethod = AccessTools.Method(touchType, "SetCommandMessage");
                    if (setCommandMsgMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setCommandMsgMethod, postfix: new HarmonyMethod(postfix));
                    }

                    // Patch SetSystemMessage
                    var setSystemMsgMethod = AccessTools.Method(touchType, "SetSystemMessage");
                    if (setSystemMsgMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setSystemMsgMethod, postfix: new HarmonyMethod(postfix));
                    }
                }
                else
                {
                    MelonLogger.Warning("[Battle Command Message] Touch.BattleCommandMessageController type not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error patching BattleCommandMessageController: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for BattleCommandMessageController.SetMessage/SetCommandMessage/SetSystemMessage.
        /// Announces battle messages including "The party was defeated".
        /// </summary>
        public static void SetMessage_Postfix(object __0)
        {
            try
            {
                // __0 is the message string (using __0 to avoid IL2CPP string param issues)
                string message = __0?.ToString();
                if (string.IsNullOrEmpty(message)) return;

                // Deduplicate
                if (message == lastBattleCommandMessage) return;
                lastBattleCommandMessage = message;

                // Clean up the message
                string cleanMessage = TextUtils.StripIconMarkup(message);
                cleanMessage = cleanMessage.Replace("\n", " ").Replace("\r", " ").Trim();
                while (cleanMessage.Contains("  "))
                    cleanMessage = cleanMessage.Replace("  ", " ");

                if (string.IsNullOrEmpty(cleanMessage)) return;

                // Use interrupt for defeat message so it's heard immediately
                bool isDefeatMessage = cleanMessage.Contains("defeated", StringComparison.OrdinalIgnoreCase);

                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: isDefeatMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error in SetMessage_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state tracking (call at battle end).
        /// </summary>
        public static void ResetState()
        {
            lastBattleCommandMessage = "";
        }
    }

}
