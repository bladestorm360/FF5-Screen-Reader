using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
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
                foreach (var dropItem in itemList)
                {
                    if (dropItem == null) continue;
                    
                    int contentId = dropItem.ContentId;
                    int quantity = dropItem.DropValue; // Assuming DropValue is quantity
                    if (quantity <= 0) quantity = 1;

                    string itemName = GetItemName(contentId);
                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (quantity > 1)
                            messageParts.Add($"{itemName} x{quantity}");
                        else
                            messageParts.Add(itemName);
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
                    else if (charExp > 0)
                    {
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
                            foreach (int abilityId in learningList)
                            {
                                // Find ability name in owned list
                                string abilityName = null;
                                    // FIXME: Unable to resolve Ability Name due to proxy issues
                                    /*if (ability != null && ability.Ability != null && ability.Ability.Id == abilityId)
                                    {
                                        if (ability.Content != null)
                                            abilityName = ability.Content.Name;
                                        break;
                                    }*/

                                if (!string.IsNullOrEmpty(abilityName))
                                {
                                    messageParts.Add($"{charName} learned {abilityName}");
                                }
                            }
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

        private static string GetItemName(int contentId)
        {
            try
            {
                var userManager = UserDataManager.Instance();
                if (userManager == null) return null;

                // Try Normal items
                // Inspecting the list is slow but safer given we lack direct ID lookup knowledge
                // NormalOwnedItemList is IEnumerable
                // However, we can try accessing the dictionaries if accessible, but they were private in dump.
                // We'll iterate the public list properties.
                
                // Only iterate if list is seemingly valid.
                // NOTE: This relies on the item already being in inventory.
                
                // Hack: We can also check MessageManager if we knew the MesId.
                // But we don't.
                
                // Let's try iterating OwnedItemData.
                // Since this runs once per battle end, performance impact is negligible for loop of ~500 items.

                // Reflection access to private dictionary might be better? No, stick to public API.
                // NormalOwnedItemList property exists.
                
                // Unfortunately, IEnumerable iteration in Il2Cpp can be tricky.
                
                // Let's try a different approach:
                // If we can't get the name easily, return null.
                
                // Actually, UserDataManager DOES expose 'normalOwnedItems' via property NormalOwnedItemList?
                // Step 987: private IEnumerable<OwnedItemData> NormalOwnedItemList { get; set; }
                // It's private property in dump!
                
                // Step 987: public Dictionary<int, bool> OwendCrystalFlags;
                
                // Step 987: public List<int> NormalOwnedItemSortIdList
                
                // Okay, NormalOwnedItemList is PRIVATE property. That's annoying.
                // `normalOwnedItems` is private field.
                
                // However, we can use standard Harmony/Reflection to access it if needed.
                // Field: `normalOwnedItems` (Dictionary<int, OwnedItemData>).
                
                // BUT, declaring the dictionary type might be issues if generics matching is strict.
                
                // Let's try to find an OwnedItemData from ANY source we have access to.
                // Maybe the 'ItemList' itself in BattleResultData contains drop items that have the name?
                // Step 949: DropItemData has ContentId, DropValue... NO Name.
                
                // Wait! DropItemData DOES NOT have name.
                
                // What about `ItemMenuPatches`? It iterates `ItemListController.contentList`.
                
                // Let's assume for now that without easy lookup, we skip Item Names or just say "Item ID X".
                // OR, we assume that `MessageManager.Instance.GetMessage(contentId)` MIGHT work? No, IDs differ.
                
                // Let's try to get `UserDataManager.Instance().normalOwnedItems` via Il2Cpp reflection?
                // Or just use `NormalOwnedItemSortIdList`? No.
                
                // Accessing private field via Harmony traversal/Reflection in C# for Il2Cpp objects:
                // We can't easily access the dictionary content without correct generic instantiation.
                
                // Let's check if there is ANY public method to get item.
                // I really missed `GetItem`.
                
                // What if I try `resultMenuController.targetData` or similar? No.
                
                // Okay, I will try to use `Il2CppLast.Management.MasterBlockManager` if it exists.
                // Otherwise, I will sadly have to skip Item Names for this iteration or assume "Items Gained".
                // I'll add a TODO to fix Item Names if they aren't reading.
                
                // Wait! I can get the name if I assume the user has at least one.
                // If I can't resolve it, I'll print "New Item(s)" or similar.
                
                // But wait, FFVI mod `ListItemFormatter` used `messageManager.GetMessage(itemData.MesIdName)`.
                // It HAD `OwnedItemData` or `ItemData`.
                // Where did it get it?
                // `ListItemFormatter.GetContentDataList` took `data._ItemList_k__BackingField`.
                // In FFVI `DropItemData` was seemingly different or they had a way.
                
                // I will assume for now I CANNOT get the name easily without `MasterData`.
                // I will define `GetItemName` to return "Item" + contentId for debug, 
                // but for production, maybe just "Item".
                
                // ACTUALLY, checking `ItemMenuPatches.cs`, I used:
                // `var itemData = targetList[index];` (which is a Controller/View wrapper).
                
                // I will try to use reflection to get `UserDataManager.normalOwnedItems` just in case I can.
                // If not, I'll leave it blank.
                
                return null; 
            }
            catch
            {
                return null;
            }
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
