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
using BattleQuantityAbilityInfomationController = Il2CppSerial.FF5.UI.KeyInput.BattleQuantityAbilityInfomationController;
using UnityEngine;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Shared helper for battle command patches.
    /// </summary>
    internal static class BattleCommandPatchHelper
    {
        /// <summary>
        /// Helper coroutine to speak text after one frame delay.
        /// </summary>
        internal static IEnumerator DelayedSpeech(string text)
        {
            yield return null; // Wait one frame
            FFV_ScreenReaderMod.SpeakText(text);
        }
    }

    /// <summary>
    /// Patch for SetCommandData - announces when a character's turn becomes active.
    /// </summary>
    [HarmonyPatch(typeof(BattleCommandSelectController), nameof(BattleCommandSelectController.SetCommandData))]
    public static class BattleCommandSelectController_SetCommandData_Patch
    {
        private static int lastCharacterId = -1;

        [HarmonyPostfix]
        public static void Postfix(BattleCommandSelectController __instance, OwnedCharacterData data)
        {
            try
            {
                if (data == null) return;

                // Only announce if it's a different character than last time
                int characterId = data.Id;
                if (characterId == lastCharacterId) return;
                lastCharacterId = characterId;

                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName)) return;

                string announcement = $"{characterName}'s turn";
                MelonLogger.Msg($"[Battle Turn] {announcement}");
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
        private static int lastAnnouncedIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(BattleCommandSelectController __instance, int index)
        {
            try
            {
                if (__instance == null) return;


                // SUPPRESSION: If targeting is active, do not announce commands
                // Use the IsTargetSelectionActive flag set by BattleTargetPatches.ShowWindow
                // This is more reliable than FindObjectOfType which can miss active controllers
                if (BattleTargetPatches.IsTargetSelectionActive) return;

                var itemTargetController = UnityEngine.Object.FindObjectOfType<ItemUseController>();
                if (itemTargetController != null) return;


                // Skip duplicate announcements
                if (index == lastAnnouncedIndex) return;
                lastAnnouncedIndex = index;

                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0) return;

                if (index < 0 || index >= contentList.Count) return;

                var contentController = contentList[index];
                if (contentController == null || contentController.TargetCommand == null) return;

                string mesIdName = contentController.TargetCommand.MesIdName;
                if (string.IsNullOrWhiteSpace(mesIdName)) return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string commandName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(commandName)) return;

                MelonLogger.Msg($"[Battle Command] {commandName}");
                CoroutineManager.StartManaged(BattleCommandPatchHelper.DelayedSpeech(commandName));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandSelectController.SetCursor patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for item and tool selection in battle.
    /// </summary>
    [HarmonyPatch(typeof(BattleItemInfomationController), nameof(BattleItemInfomationController.SelectContent),
        new Type[] { typeof(Il2CppLast.UI.Cursor), typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType) })]
    public static class BattleItemInfomationController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(BattleItemInfomationController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                // SUPPRESSION: Stateless check
                if (UnityEngine.Object.FindObjectOfType<ItemUseController>() != null) return;
                // Note: Items usually don't mix with EnemyTargetController in the same way, but safe to check?
                // Actually, if we are in Item menu, and EnemyTarget is active? Unlikely.
                // But ItemUseController is the main one for Items.

                int index = targetCursor.Index;

                bool isMachine = __instance.isMachineState;
                var contentList = __instance.contentList;
                var machineContentList = __instance.machineContentList;

                Il2CppSystem.Collections.Generic.List<BattleItemInfomationContentController> activeList;
                if (isMachine)
                    activeList = machineContentList;
                else
                    activeList = contentList;

                if (activeList == null || activeList.Count == 0) return;
                if (index < 0 || index >= activeList.Count) return;

                var selectedContent = activeList[index];
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

                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Battle Item/Tool] {announcement}");
                CoroutineManager.StartManaged(BattleCommandPatchHelper.DelayedSpeech(announcement));
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
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(BattleQuantityAbilityInfomationController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                // SUPPRESSION: Stateless check
                if (UnityEngine.Object.FindObjectOfType<BattleTargetSelectController>() != null) return;
                if (UnityEngine.Object.FindObjectOfType<ItemUseController>() != null) return;

                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0) return;

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count) return;

                var selectedContent = contentList[index];
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

                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Battle Ability] {announcement}");
                CoroutineManager.StartManaged(BattleCommandPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleQuantityAbilityInfomationController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
