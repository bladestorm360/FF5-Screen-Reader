using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Battle;
using Il2CppLast.UI;
using Il2CppLast.Data; // Added for BattlePlayerData
using Il2CppLast.Data.Master;
using Il2CppLast.Data.User;
using Il2CppLast.Management;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.TextUtils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using BattleQuantityAbilityInfomationController = Il2CppSerial.FF5.UI.KeyInput.BattleQuantityAbilityInfomationController;
using UnityEngine;

namespace FFV_ScreenReader.Patches
{
    // Track current active character for status announcements (H key)
    public static class ActiveBattleCharacterTracker
    {
        public static OwnedCharacterData CurrentActiveCharacter { get; set; }
    }

    /// <summary>
    /// Patch for SetCommandData - announces when a character's turn becomes active.
    /// </summary>
    [HarmonyPatch(typeof(BattleCommandSelectController), nameof(BattleCommandSelectController.SetCommandData))]
    public static class BattleCommandSelectController_SetCommandData_Patch
    {
        // Id of the character whose turn was last announced. Never reset — the next turn always
        // belongs to a different character, and a re-show of the same character's window is
        // exactly the case this suppresses.
        private static int _lastCharacterId = -1;

        [HarmonyPostfix]
        public static void Postfix(BattleCommandSelectController __instance, OwnedCharacterData data)
        {
            try
            {
                if (data == null) return;

                // Track the active character for H key status announcement
                ActiveBattleCharacterTracker.CurrentActiveCharacter = data;

                // Activate battle state if not already in battle
                if (!BattleState.IsInBattle)
                {
                    BattleState.SetActive();
                }

                // The game re-invokes SetCommandData for the SAME character whenever the command
                // window is rebuilt — notably after cancelling out of target selection — so
                // announce the turn only when the character actually changes.
                int characterId = data.Id;
                if (characterId == _lastCharacterId) return;
                _lastCharacterId = characterId;

                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName)) return;

                string announcement = string.Format(T("{0}'s turn"), characterName);
                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandSelectController.SetCommandData patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patches for battle command selection (Attack, Magic, Item, Defend, etc.)
    /// Announces command names when cursor moves through the menu.
    /// </summary>
    [HarmonyPatch(typeof(BattleCommandSelectController), nameof(BattleCommandSelectController.SetCursor))]
    public static class BattleCommandSelectController_SetCursor_Patch
    {
        // SetCursor re-fires with an unchanged index when the command window is re-shown
        // (cancel-back from targeting), so track the last announced position.
        private static int _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(BattleCommandSelectController __instance, int index)
        {
            try
            {
                if (__instance == null) return;

                // SUPPRESSION: If targeting is active, do not announce commands
                // Use flags set by BattleTargetPatches and ItemUseTracker patches
                // This avoids expensive FindObjectOfType calls on every cursor movement
                if (BattleTargetPatches.IsTargetSelectionActive || ItemUseTracker.IsItemUseActive) return;

                if (index == _lastIndex) return;
                _lastIndex = index;

                var contentController = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (contentController == null || contentController.TargetCommand == null) return;

                string mesIdName = contentController.TargetCommand.MesIdName;
                if (string.IsNullOrWhiteSpace(mesIdName)) return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string commandName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(commandName)) return;

                // Append list position last (command index within the battle command list).
                int commandCount = __instance.contentList != null ? __instance.contentList.Count : 0;
                commandName = MenuPosition.Format(commandName, index, commandCount);

                CoroutineManager.StartManaged(DelayedBattleCommandSpeech(commandName));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandSelectController.SetCursor patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Battle-specific delayed speech that re-checks suppression after the frame delay.
        /// This prevents "Attack" from being announced when ShowWindow activates target selection
        /// during the one-frame delay.
        /// </summary>
        private static IEnumerator DelayedBattleCommandSpeech(string text)
        {
            yield return null;
            // Re-check suppression after delay — ShowWindow may have activated target selection
            if (BattleTargetPatches.IsTargetSelectionActive || ItemUseTracker.IsItemUseActive)
                yield break;
            FFV_ScreenReaderMod.SpeakText(text);
        }
    }

    /// <summary>
    /// Patch for item and tool selection in battle.
    /// </summary>
    [HarmonyPatch(typeof(BattleItemInfomationController), nameof(BattleItemInfomationController.SelectContent),
        new Type[] { typeof(Il2CppLast.UI.Cursor), typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType) })]
    public static class BattleItemInfomationController_SelectContent_Patch
    {
        // SelectContent carries a WithinRangeType — the scroll view re-fires it on range
        // recalculation with an unchanged row, so compare the built string.
        private static string _lastAnnouncement;

        [HarmonyPostfix]
        public static void Postfix(BattleItemInfomationController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                // SUPPRESSION: Use cached state instead of FindObjectOfType
                if (ItemUseTracker.IsItemUseActive) return;

                int index = targetCursor.Index;

                bool isMachine = __instance.isMachineState;
                var contentList = __instance.contentList;
                var machineContentList = __instance.machineContentList;

                Il2CppSystem.Collections.Generic.List<BattleItemInfomationContentController> activeList;
                if (isMachine)
                    activeList = machineContentList;
                else
                    activeList = contentList;

                var selectedContent = SelectContentHelper.TryGetItem(activeList, index);
                if (selectedContent == null) return;

                string itemName = null;
                var contentData = selectedContent.Data;

                if (contentData != null)
                {
                    itemName = contentData.Name;
                }
                else
                {
                    var view = selectedContent.view;
                    if (view != null)
                    {
                        if (view.IconTextView != null && view.IconTextView.nameText != null)
                            itemName = view.IconTextView.nameText.text;
                        else if (view.NonItemTextView != null && view.NonItemTextView.nameText != null)
                            itemName = view.NonItemTextView.nameText.text;
                    }
                }

                if (string.IsNullOrWhiteSpace(itemName)) return;
                itemName = StripIconMarkup(itemName);
                if (string.IsNullOrWhiteSpace(itemName)) return;

                string announcement = itemName;

                if (contentData != null)
                {
                    try
                    {
                        int count = contentData.Count;
                        if (count > 0) announcement += $", {count}";
                    }
                    catch {}

                    try
                    {
                        string description = contentData.Description;
                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            description = StripIconMarkup(description);
                            if (!string.IsNullOrWhiteSpace(description)) announcement += $", {description}";
                        }
                    }
                    catch {}
                }

                // Append list position last (after quantity / description).
                announcement = MenuPosition.Format(announcement, index, activeList != null ? activeList.Count : 0);

                if (announcement == _lastAnnouncement) return;
                _lastAnnouncement = announcement;

                CoroutineManager.StartManaged(SpeechHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleItemInfomationController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability/magic selection in battle.
    /// </summary>
    [HarmonyPatch(typeof(BattleQuantityAbilityInfomationController), nameof(BattleQuantityAbilityInfomationController.SelectContent),
        new Type[] { typeof(Il2CppLast.UI.Cursor), typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType) })]
    public static class BattleQuantityAbilityInfomationController_SelectContent_Patch
    {
        // Same scroll-view re-fire as the item list above.
        private static string _lastAnnouncement;

        [HarmonyPostfix]
        public static void Postfix(BattleQuantityAbilityInfomationController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                // SUPPRESSION: Use cached state instead of FindObjectOfType calls
                if (BattleTargetPatches.IsTargetSelectionActive || ItemUseTracker.IsItemUseActive) return;

                int index = targetCursor.Index;
                var selectedContent = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (selectedContent == null) return;

                var abilityData = selectedContent.Data;
                if (abilityData == null) return;

                string mesIdName = abilityData.MesIdName;
                string mesIdDescription = abilityData.MesIdDescription;

                if (string.IsNullOrWhiteSpace(mesIdName)) return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string abilityName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(abilityName)) return;

                abilityName = StripIconMarkup(abilityName);
                if (string.IsNullOrWhiteSpace(abilityName)) return;

                string announcement = abilityName;

                if (!string.IsNullOrWhiteSpace(mesIdDescription))
                {
                    string description = StripIconMarkup(messageManager.GetMessage(mesIdDescription));
                    if (!string.IsNullOrWhiteSpace(description)) announcement += $", {description}";
                }

                // Append list position last (after description).
                announcement = MenuPosition.Format(announcement, index, __instance.contentList != null ? __instance.contentList.Count : 0);

                if (announcement == _lastAnnouncement) return;
                _lastAnnouncement = announcement;

                CoroutineManager.StartManaged(SpeechHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleQuantityAbilityInfomationController.SelectContent patch: {ex.Message}");
            }
        }
    }

}
