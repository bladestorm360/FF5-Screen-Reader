using System;
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
using UnityEngine;
using BattleCommandMessageController_KeyInput = Il2CppLast.UI.KeyInput.BattleCommandMessageController;
using BattleCommandMessageController_Touch = Il2CppLast.UI.Touch.BattleCommandMessageController;
using BattlePlayerData = Il2Cpp.BattlePlayerData;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Global message deduplication to prevent the same message being spoken by multiple patches
    /// </summary>
    public static class GlobalBattleMessageTracker
    {
        private static string lastMessage = "";
        private static float lastMessageTime = 0f;
        private const float MESSAGE_THROTTLE_SECONDS = 1.5f;

        /// <summary>
        /// Try to announce a message, returning false if it was recently announced
        /// </summary>
        public static bool TryAnnounce(string message, string source)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string cleanMessage = message.Trim();
            float currentTime = UnityEngine.Time.time;

            // Skip if same message within throttle window
            if (cleanMessage == lastMessage && (currentTime - lastMessageTime) < MESSAGE_THROTTLE_SECONDS)
            {
                MelonLogger.Msg($"[{source}] Skipped duplicate: {cleanMessage}");
                return false;
            }

            lastMessage = cleanMessage;
            lastMessageTime = currentTime;

            MelonLogger.Msg($"[{source}] {cleanMessage}");
            FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            return true;
        }

        /// <summary>
        /// Reset tracking (e.g., when battle ends)
        /// </summary>
        public static void Reset()
        {
            lastMessage = "";
            lastMessageTime = 0f;
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
                GlobalBattleMessageTracker.TryAnnounce(message, "ScrollMessage");
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
                if (battleActData == null)
                {
                    MelonLogger.Msg("[CreateActFunction] battleActData is null");
                    return;
                }

                MelonLogger.Msg($"[CreateActFunction] Processing battle action");

                // Get the attacker's name
                string actorName = GetActorName(battleActData);

                // Get the action/ability name
                string actionName = GetActionName(battleActData);

                MelonLogger.Msg($"[CreateActFunction] Actor: {actorName ?? "null"}, Action: {actionName ?? "null"}");

                if (!string.IsNullOrEmpty(actorName))
                {
                    string announcement;
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        // Format action name to be more natural
                        // "Attack" -> "attacks", "Fire" -> "casts Fire", etc.
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
                            // For spells/abilities: "Bartz casts Fire" or "Stray Cat uses Scratch"
                            announcement = $"{actorName}, {actionName}";
                        }
                    }
                    else
                    {
                        announcement = $"{actorName} attacks";
                    }
                    GlobalBattleMessageTracker.TryAnnounce(announcement, "BattleAction");
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
                if (attackUnit == null)
                {
                    MelonLogger.Msg("[CreateActFunction] AttackUnitData is null");
                    return null;
                }

                // Check if attacker is a player character
                var playerData = attackUnit.TryCast<BattlePlayerData>();
                if (playerData != null && playerData.ownedCharacterData != null)
                {
                    string name = playerData.ownedCharacterData.Name;
                    MelonLogger.Msg($"[CreateActFunction] Found player name: {name}");
                    return name;
                }

                // Check if attacker is an enemy
                var enemyData = attackUnit.TryCast<BattleEnemyData>();
                if (enemyData != null)
                {
                    string mesIdName = enemyData.GetMesIdName();
                    MelonLogger.Msg($"[CreateActFunction] Enemy mesIdName: {mesIdName}");
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                    {
                        string localizedName = messageManager.GetMessage(mesIdName);
                        if (!string.IsNullOrEmpty(localizedName))
                        {
                            MelonLogger.Msg($"[CreateActFunction] Found enemy name: {localizedName}");
                            return localizedName;
                        }
                    }
                }
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
                            MelonLogger.Msg($"[CreateActFunction] Found ability name: {abilityName}");
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
                                MelonLogger.Msg($"[CreateActFunction] Found command name: {localizedName}");
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
        public static void Postfix(Il2CppLast.Battle.BattleUnitData data, int value, Il2CppLast.Systems.HitType hitType, bool isRecovery)
        {
            try
            {
                string targetName = "Unknown";

                // Check if this is a BattlePlayerData (player character)
                var playerData = data.TryCast<Il2Cpp.BattlePlayerData>();
                if (playerData != null)
                {
                    try
                    {
                        var ownedCharData = playerData.ownedCharacterData;
                        if (ownedCharData != null)
                        {
                            targetName = ownedCharData.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error getting player name: {ex.Message}");
                    }
                }

                // Check if this is a BattleEnemyData (enemy)
                var enemyData = data.TryCast<Il2CppLast.Battle.BattleEnemyData>();
                if (enemyData != null)
                {
                    try
                    {
                        string mesIdName = enemyData.GetMesIdName();
                        var messageManager = Il2CppLast.Management.MessageManager.Instance;
                        if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                        {
                            string localizedName = messageManager.GetMessage(mesIdName);
                            if (!string.IsNullOrEmpty(localizedName))
                            {
                                targetName = localizedName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error getting enemy name: {ex.Message}");
                    }
                }

                string message;
                if (hitType == Il2CppLast.Systems.HitType.Miss || value == 0)
                {
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

                // Use global tracker but don't dedupe damage - each hit is unique
                MelonLogger.Msg($"[Damage] {message}");
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
}
