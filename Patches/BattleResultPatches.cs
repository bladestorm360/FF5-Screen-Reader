using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
using Il2CppLast.Data.Master;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using Il2CppLast.Systems;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for battle result announcements (XP, gil, level ups, drops)
    /// </summary>
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.Show))]
    public static class ResultMenuController_Show_Patch
    {
        internal static string lastAnnouncement = "";
        internal static BattleResultData lastBattleData = null;

        [HarmonyPostfix]
        public static void Postfix(BattleResultData data, bool isReverse)
        {
            try
            {
                if (data == null || isReverse)
                {
                    return;
                }

                ProcessBattleResult(data, "Show");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.Show patch: {ex.Message}");
            }
        }

        internal static void ProcessBattleResult(BattleResultData data, string source)
        {
            // Build announcement message
            var messageParts = new System.Collections.Generic.List<string>();

            // Announce gil gained
            int gil = data.GetGil;
            if (gil > 0)
            {
                messageParts.Add($"{gil:N0} gil");
            }

            // Announce items dropped
            var itemList = data.ItemList;
            if (itemList != null && itemList.Count > 0)
            {
                var messageManager = MessageManager.Instance;
                if (messageManager != null)
                {
                    // Convert drop items to content data with localized names
                    var itemContentList = ListItemFormatter.GetContentDataList(itemList, messageManager);
                    if (itemContentList != null && itemContentList.Count > 0)
                    {
                        foreach (var itemContent in itemContentList)
                        {
                            if (itemContent == null) continue;

                            string itemName = itemContent.Name;
                            if (string.IsNullOrEmpty(itemName)) continue;

                            // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                            itemName = StripIconMarkup(itemName);

                            if (!string.IsNullOrEmpty(itemName))
                            {
                                // Get the quantity from Count property
                                int quantity = itemContent.Count;
                                if (quantity > 1)
                                {
                                    messageParts.Add($"{itemName} x{quantity}");
                                }
                                else
                                {
                                    messageParts.Add(itemName);
                                }
                            }
                        }
                    }
                }
            }

            // Announce character results
            var characterList = data.CharacterList;
            if (characterList != null)
            {
                foreach (var charResult in characterList)
                {
                    if (charResult == null) continue;

                    var afterData = charResult.AfterData;
                    if (afterData == null) continue;

                    string charName = afterData.Name;
                    int charExp = charResult.GetExp;
                    int charAbp = charResult.GetABP;

                    // Level up check
                    if (charResult.IsLevelUp)
                    {
                        int newLevel = afterData.parameter != null ? afterData.parameter.ConfirmedLevel() : 0;
                        messageParts.Add($"{charName} gained {charExp:N0} XP and leveled up to level {newLevel}");
                    }
                    else
                    {
                        // Always announce XP, even if 0
                        messageParts.Add($"{charName} gained {charExp:N0} XP");
                    }

                    // ABP check
                    if (charAbp > 0)
                    {
                        // Don't spam ABP if it's routine, but user asked for job points. 
                        // Maybe simplified: "3 ABP" if shared? Or per character?
                        // Usually ABP is global for the party but assigned per character.
                        // We'll append it to the character status line or separate?
                        // Let's treat it per character to be safe.
                        // Actually, often ABP is just "Gained 3 ABP" globally.
                        // But here it is on charResult. Let's mention it if significant (Job Level Up).
                        if (charResult.IsJobLevelUp)
                        {
                            messageParts.Add($"{charName} gained {charAbp} ABP and Job Level Up!");
                        }
                    }

                    // Abilities learned
                    var learningList = charResult.LearningList;
                    if (learningList != null && learningList.Count > 0)
                    {
                        var messageManager = MessageManager.Instance;
                        if (messageManager != null && afterData.OwnedAbilityList != null)
                        {
                            // FIXME: Ability name lookup disabled due to proxy issues
                            // foreach (int abilityId in learningList) { ... }
                        }
                    }
                }
            }

            if (messageParts.Count == 0) return;

            string announcement = string.Join(", ", messageParts);

            // Skip duplicate
            if (data == lastBattleData && announcement == lastAnnouncement)
            {
                return;
            }

            lastBattleData = data;
            lastAnnouncement = announcement;
            
            MelonLogger.Msg($"[Battle Result] {announcement}");
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }

    }

    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowPointsInit))]
    public static class ResultMenuController_ShowPointsInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                var data = __instance.targetData;
                if (data == null) return;
                
                ResultMenuController_Show_Patch.ProcessBattleResult(data, "ShowPointsInit");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.ShowPointsInit patch: {ex.Message}");
            }
        }
    }
}
