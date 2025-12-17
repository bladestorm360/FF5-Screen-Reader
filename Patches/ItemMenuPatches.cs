using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI;
using Il2CppLast.Defaine;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using UnityEngine;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Shared helper for item menu patches.
    /// </summary>
    internal static class ItemMenuPatchHelper
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
    /// Patches for item and equipment menu navigation in FF5.
    /// Announces item/equipment name, quantity, and description when browsing.
    /// </summary>

    // Patch ItemListController.SelectContent to announce items when navigating
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemListController), "SelectContent", new Type[] {
        typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData>),
        typeof(int),
        typeof(Il2CppLast.UI.Cursor),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class ItemListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";
        private static float lastAnnouncementTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ItemListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData> targets,
            int index,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                MelonLogger.Msg($"[Item Menu Patch] Called with index={index}, cursor.Index={targetCursor?.Index}");
                
                if (targets == null)
                {
                    return;
                }

                // Convert IEnumerable to List for indexed access
                var targetList = new Il2CppSystem.Collections.Generic.List<ItemListContentData>(targets);
                if (targetList == null || targetList.Count == 0)
                {
                    return;
                }

                if (index < 0 || index >= targetList.Count)
                {
                    return;
                }

                var itemData = targetList[index];
                if (itemData == null)
                {
                    return;
                }

                string itemName = itemData.Name;
                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Remove icon markup from name
                itemName = StripIconMarkup(itemName);

                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Build announcement with item details
                string announcement = itemName;

                // Add quantity if available
                int count = itemData.Count;
                if (count > 0)
                {
                    announcement += $", {count}";
                }

                // Add description if available
                string description = itemData.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    // Remove icon markup
                    description = StripIconMarkup(description);

                    if (!string.IsNullOrEmpty(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicates or rapid re-announcements
                float currentTime = UnityEngine.Time.time;
                if (announcement == lastAnnouncement && (currentTime - lastAnnouncementTime) < 0.1f)
                {
                    MelonLogger.Msg($"[Item Menu] Skipping duplicate within 100ms");
                    return;
                }
                lastAnnouncement = announcement;
                lastAnnouncementTime = currentTime;

                MelonLogger.Msg($"[Item Menu] {announcement}");
                CoroutineManager.StartManaged(ItemMenuPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ItemListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    // Patch EquipmentSelectWindowController.SetCursor to announce equipment when navigating
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.EquipmentSelectWindowController), "SetCursor", new Type[] {
        typeof(Il2CppLast.UI.Cursor),
        typeof(bool),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class EquipmentSelectWindowController_SetCursor_Patch
    {
        private static string lastAnnouncement = "";
        private static float lastAnnouncementTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.EquipmentSelectWindowController __instance,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null)
                {
                    return;
                }

                var contentList = __instance.ContentDataList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                var equipmentData = contentList[index];
                if (equipmentData == null)
                {
                    return;
                }

                string itemName = equipmentData.Name;
                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Remove icon markup from name
                itemName = StripIconMarkup(itemName);

                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Build announcement with equipment details
                string announcement = itemName;

                // Add mechanical info (ATK +15, DEF +8, etc.)
                string paramMessage = equipmentData.ParameterMessage;
                if (!string.IsNullOrEmpty(paramMessage))
                {
                    // Remove icon markup
                    paramMessage = StripIconMarkup(paramMessage);

                    if (!string.IsNullOrEmpty(paramMessage))
                    {
                        announcement += $", {paramMessage}";
                    }
                }

                // Add description if available
                string description = equipmentData.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    // Remove icon markup
                    description = StripIconMarkup(description);

                    if (!string.IsNullOrEmpty(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicates or rapid re-announcements
                float currentTime = UnityEngine.Time.time;
                if (announcement == lastAnnouncement && (currentTime - lastAnnouncementTime) < 0.1f)
                {
                    MelonLogger.Msg($"[Equipment Menu] Skipping duplicate within 100ms");
                    return;
                }
                lastAnnouncement = announcement;
                lastAnnouncementTime = currentTime;

                MelonLogger.Msg($"[Equipment Menu] {announcement}");
                CoroutineManager.StartManaged(ItemMenuPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentSelectWindowController.SetCursor patch: {ex.Message}");
            }
        }
    }

    // Patch EquipmentInfoWindowController.SelectContent to announce equipment slots when navigating
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.EquipmentInfoWindowController), "SelectContent", new Type[] {
        typeof(Il2CppLast.UI.Cursor)
    })]
    public static class EquipmentInfoWindowController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";
        private static float lastAnnouncementTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.EquipmentInfoWindowController __instance,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null)
                {
                    return;
                }

                int index = targetCursor.Index;

                // Get slot name and equipped item from contentList
                string slotName = null;
                string equippedItem = null;
                if (__instance.contentList != null && index >= 0 && index < __instance.contentList.Count)
                {
                    var contentView = __instance.contentList[index];
                    if (contentView != null)
                    {
                        // Get slot name from partText
                        if (contentView.partText != null)
                        {
                            slotName = contentView.partText.text;
                        }

                        // Get item data from Data property
                        var itemData = contentView.Data;
                        if (itemData != null)
                        {
                            equippedItem = itemData.Name;

                            // Get parameter message (ATK +15, DEF +8, etc.)
                            string paramMessage = itemData.ParameterMessage;
                            if (!string.IsNullOrEmpty(paramMessage))
                            {
                                equippedItem += ", " + paramMessage;
                            }
                        }
                    }
                }

                // Build announcement
                string announcement = "";
                if (!string.IsNullOrEmpty(slotName))
                {
                    announcement = slotName;
                }

                if (!string.IsNullOrEmpty(equippedItem))
                {
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcement += ": " + equippedItem;
                    }
                    else
                    {
                        announcement = equippedItem;
                    }
                }

                if (string.IsNullOrEmpty(announcement))
                {
                    return;
                }

                // Filter icon markup
                announcement = StripIconMarkup(announcement);

                // Skip duplicates or rapid re-announcements
                float currentTime = UnityEngine.Time.time;
                if (announcement == lastAnnouncement && (currentTime - lastAnnouncementTime) < 0.1f)
                {
                    MelonLogger.Msg($"[Equipment Slot] Skipping duplicate within 100ms");
                    return;
                }
                lastAnnouncement = announcement;
                lastAnnouncementTime = currentTime;

                MelonLogger.Msg($"[Equipment Slot] {announcement}");
                CoroutineManager.StartManaged(ItemMenuPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentInfoWindowController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
