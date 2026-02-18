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
using UnityEngine;
using BattleCommandMessageController_KeyInput = Il2CppLast.UI.KeyInput.BattleCommandMessageController;
using BattleCommandMessageController_Touch = Il2CppLast.UI.Touch.BattleCommandMessageController;
using BattlePlayerData = Il2Cpp.BattlePlayerData;
using Il2CppLast.Data.User;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Global message deduplication to prevent the same message being spoken by multiple patches
    /// </summary>
    public static class GlobalBattleMessageTracker
    {
        /// <summary>
        /// Try to announce a message, returning false if it was recently announced
        /// </summary>
        public static bool TryAnnounce(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string cleanMessage = message.Trim();

            if (!FFV_ScreenReader.Utils.AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.BATTLE_MESSAGE, cleanMessage))
            {
                return false;
            }

            FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            return true;
        }

        /// <summary>
        /// Reset tracking (e.g., when battle ends)
        /// </summary>
        public static void Reset()
        {
            FFV_ScreenReader.Utils.AnnouncementDeduplicator.Reset(AnnouncementContexts.BATTLE_MESSAGE);
        }
    }

    /// <summary>
    /// Patches for message display methods - View layer and scrolling messages in Battle
    /// </summary>

    [HarmonyPatch(typeof(ScrollMessageManager), nameof(ScrollMessageManager.Play))]
    public static class ScrollMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ScrollMessageClient.ScrollType type, string message)
        {
            try
            {
                GlobalBattleMessageTracker.TryAnnounce(message);
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
                    // Object-based dedup: each BattleActData is a unique instance,
                    // so two goblins both attacking produce distinct objects and are
                    // both announced (string dedup would suppress the second).
                    if (!AnnouncementDeduplicator.ShouldAnnounce(
                            AnnouncementContexts.BATTLE_ACTION, battleActData))
                        return;

                    string announcement;
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        string actionLower = actionName.ToLower();
                        if (actionLower == "attack" || actionLower == "fight")
                        {
                            announcement = $"{actorName} attacks";
                        }
                        else if (actionLower == "defend" || actionLower == "guard")
                        {
                            announcement = $"{actorName} defends";
                        }
                        else if (actionLower == "item")
                        {
                            announcement = $"{actorName} uses item";
                        }
                        else
                        {
                            announcement = $"{actorName}, {actionName}";
                        }
                    }
                    else
                    {
                        announcement = $"{actorName} attacks";
                    }
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

    [HarmonyPatch(typeof(Il2CppLast.Battle.Function.BattleBasicFunction), nameof(Il2CppLast.Battle.Function.BattleBasicFunction.CreateDamageView))]
    public static class BattleBasicFunction_CreateDamageView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData data, int value, Il2CppLast.Systems.HitType hitType, bool isRecovery, Il2CppLast.Systems.CalcResult.MissType missType)
        {
            try
            {
                string targetName = BattleUnitHelper.GetUnitName(data) ?? "Unknown";

                string message;
                if (hitType == Il2CppLast.Systems.HitType.Miss)
                {
                    // NonView = non-damage ability (Steal, Focus, etc.) — game doesn't show "Miss" visually
                    if (missType == Il2CppLast.Systems.CalcResult.MissType.NonView)
                        return;

                    message = $"{targetName}: Miss";
                }
                else if (hitType == Il2CppLast.Systems.HitType.Recovery)
                {
                    message = $"{targetName}: Recovered {value} HP";
                }
                else if (hitType == Il2CppLast.Systems.HitType.MPRecovery)
                {
                    message = $"{targetName}: Recovered {value} MP";
                }
                else
                {
                    message = $"{targetName}: {value} damage";
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
                // Reset target tracking for new turn
                BattleTargetPatches.ResetState();
                // Also reset global message tracker so turn announcements can repeat
                GlobalBattleMessageTracker.Reset();
                // Reset object-based action dedup so repeat actions are announced fresh
                AnnouncementDeduplicator.Reset(AnnouncementContexts.BATTLE_ACTION);
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
    // All use GlobalBattleMessageTracker for deduplication

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
                            announcement = $"Stole {itemName} x{cnt}";
                        }
                        else
                        {
                            announcement = $"Stole {itemName}";
                        }

                        FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                        return;
                    }
                }

                // Fallback if we couldn't get the item name
                FFV_ScreenReaderMod.SpeakText($"Stole item", interrupt: false);
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
                string targetName = FFV_ScreenReader.Utils.BattleUnitHelper.GetUnitName(battleUnitData) ?? "Unknown";

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

                // Skip duplicates
                if (!FFV_ScreenReader.Utils.AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.BATTLE_MESSAGE_CONDITION, announcement))
                {
                    return;
                }

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
