using System;
using System.Collections.Generic;
using MelonLoader;
using FFV_ScreenReader.Core;

using UserDataManager = Il2CppLast.Management.UserDataManager;
using MessageManager = Il2CppLast.Management.MessageManager;
using EquipUtility = Il2CppLast.Systems.EquipUtility;
using OwnedItemData = Il2CppLast.Data.User.OwnedItemData;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces which unlocked jobs can equip the currently selected equipment
    /// when 'I' key is pressed in the Items menu.
    /// Only works for equipment (weapons/armor), silent for consumables/key items.
    /// </summary>
    public static class ItemDetailsAnnouncer
    {
        private const int CONTENT_TYPE_WEAPON = 2;
        private const int CONTENT_TYPE_ARMOR = 3;

        /// <summary>
        /// Announces which unlocked jobs can equip the currently selected item.
        /// Only announces for weapons and armor, silent for other items.
        /// </summary>
        public static void AnnounceEquipRequirements()
        {
            try
            {
                var itemData = ItemMenuTracker.LastSelectedItem;
                if (itemData == null)
                    return;

                int itemType = itemData.ItemType;
                int contentId = itemData.contentId;

                // Only process equipment (weapons and armor)
                if (itemType != CONTENT_TYPE_WEAPON && itemType != CONTENT_TYPE_ARMOR)
                    return;

                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                    return;

                // Get OwnedItemData from contentId for EquipUtility check
                var ownedItemData = userDataManager.SearchOwnedItem(contentId);
                if (ownedItemData == null)
                    return;

                // Get released (unlocked) jobs
                var releasedJobs = userDataManager.ReleasedJobs;
                if (releasedJobs == null || releasedJobs.Count == 0)
                    return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                    return;

                // Check each released job
                var canEquipNames = new List<string>();
                foreach (var job in releasedJobs)
                {
                    if (job == null)
                        continue;

                    try
                    {
                        bool canEquip = EquipUtility.CanEquipped(ownedItemData, job.Id);
                        if (canEquip)
                        {
                            string jobName = messageManager.GetMessage(job.MesIdName);
                            if (!string.IsNullOrEmpty(jobName))
                            {
                                canEquipNames.Add(jobName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ItemDetails] Error checking job {job.Id}: {ex.Message}");
                    }
                }

                // Build and announce the result
                string announcement;
                if (canEquipNames.Count == 0)
                {
                    announcement = "No unlocked jobs can equip";
                }
                else
                {
                    announcement = "Can equip: " + string.Join(", ", canEquipNames);
                }

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Error: {ex.Message}");
            }
        }
    }
}
