using System;
using System.Collections.Generic;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

using UserDataManager = Il2CppLast.Management.UserDataManager;
using MessageManager = Il2CppLast.Management.MessageManager;
using EquipUtility = Il2CppLast.Systems.EquipUtility;
using MasterManager = Il2CppLast.Data.Master.MasterManager;
using JobGroup = Il2CppLast.Data.Master.JobGroup;
using Weapon = Il2CppLast.Data.Master.Weapon;
using Armor = Il2CppLast.Data.Master.Armor;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// On-demand readers for the focused item in the Items menu.
    ///
    /// - I key / right stick up  → the item's description (<see cref="AnnounceItemDescription"/>)
    /// - U key / right stick left → which unlocked jobs can equip it
    ///   (<see cref="AnnounceEquipRequirements"/>)
    ///
    /// Equip lookups run entirely off master data rather than OwnedItemData, so the same code
    /// serves shop goods the player does not own yet (see Menus/UsableByAnnouncer).
    /// </summary>
    public static class ItemDetailsAnnouncer
    {
        internal const int CONTENT_TYPE_WEAPON = 2;
        internal const int CONTENT_TYPE_ARMOR = 3;

        /// <summary>
        /// Reads the focused item's description. Bound to the I key / right stick up, matching
        /// FF1 and FF4 so the details key means the same thing in every game.
        /// </summary>
        public static void AnnounceItemDescription(bool interrupt = true)
        {
            try
            {
                var itemData = ItemMenuTracker.LastSelectedItem;
                if (itemData == null)
                    return;

                string description = TextUtils.StripIconMarkup(itemData.Description);
                FFV_ScreenReaderMod.SpeakText(
                    string.IsNullOrWhiteSpace(description) ? T("No description available") : description.Trim(),
                    interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Description error: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces which unlocked jobs can equip the focused item. Bound to the U key /
        /// right stick left. Silent for consumables and key items.
        /// </summary>
        public static void AnnounceEquipRequirements(bool interrupt = true)
        {
            try
            {
                var itemData = ItemMenuTracker.LastSelectedItem;
                if (itemData == null)
                    return;

                AnnounceEquipJobsFor(itemData.ItemType, itemData.ItemId, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces which unlocked jobs can equip a given content type/id. Split out so the
        /// equip menu can reuse it — its panes carry the type/id directly rather than an
        /// ItemListContentData.
        /// </summary>
        public static void AnnounceEquipJobsFor(int itemType, int itemId, bool interrupt = true)
        {
            try
            {
                string announcement = BuildEquipJobsAnnouncement(itemType, itemId);
                if (string.IsNullOrEmpty(announcement))
                    return;

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the focused equipment's description in the equip menu — the I key equivalent of
        /// AnnounceItemDescription for a screen whose two panes carry different data types.
        /// </summary>
        public static void AnnounceEquipDescription(bool interrupt = true)
        {
            try
            {
                string description = TextUtils.StripIconMarkup(EquipMenuTracker.LastDescription);
                FFV_ScreenReaderMod.SpeakText(
                    string.IsNullOrWhiteSpace(description) ? T("No description available") : description.Trim(),
                    interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EquipDetails] Description error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds "Can equip: ..." for a (contentType, typeValue) pair using master data only.
        /// Returns null for non-equipment or unresolvable data so callers stay silent.
        ///
        /// Master-data-only is deliberate: UserDataManager.SearchOwnedItem returns null for
        /// items the player does not own, which would make this unusable in shops.
        /// </summary>
        internal static string BuildEquipJobsAnnouncement(int itemType, int itemId)
        {
            try
            {
                if (itemType != CONTENT_TYPE_WEAPON && itemType != CONTENT_TYPE_ARMOR)
                    return null;

                var masterManager = MasterManager.Instance;
                if (masterManager == null)
                    return null;

                int equipJobGroupId = GetEquipJobGroupId(masterManager, itemType, itemId);
                if (equipJobGroupId <= 0)
                    return null;

                var jobGroup = masterManager.GetData<JobGroup>(equipJobGroupId);
                if (jobGroup == null)
                    return null;

                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                    return null;

                // Only released jobs, so the readout can't spoil jobs the player hasn't unlocked.
                var releasedJobs = userDataManager.ReleasedJobs;
                if (releasedJobs == null || releasedJobs.Count == 0)
                    return null;

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                    return null;

                var canEquipNames = new List<string>();
                foreach (var job in releasedJobs)
                {
                    if (job == null)
                        continue;

                    try
                    {
                        // The game's own predicate — avoids hand-rolling the JobGroup's
                        // Job1Accept..Job22Accept columns and assuming jobId == index + 1.
                        if (!EquipUtility.CanEquipped(jobGroup, job.Id))
                            continue;

                        string jobName = messageManager.GetMessage(job.MesIdName);
                        if (!string.IsNullOrEmpty(jobName))
                            canEquipNames.Add(jobName);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ItemDetails] Error checking job {job.Id}: {ex.Message}");
                    }
                }

                return BuildAnnouncement(canEquipNames);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Equip lookup error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads EquipJobGroupId off the Weapon or Armor master row. Returns 0 when unresolvable.
        /// </summary>
        internal static int GetEquipJobGroupId(MasterManager masterManager, int itemType, int itemId)
        {
            try
            {
                if (itemType == CONTENT_TYPE_WEAPON)
                    return masterManager.GetData<Weapon>(itemId)?.EquipJobGroupId ?? 0;

                if (itemType == CONTENT_TYPE_ARMOR)
                    return masterManager.GetData<Armor>(itemId)?.EquipJobGroupId ?? 0;

                return 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemDetails] Job group lookup failed for type {itemType} id {itemId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Formats the spoken line for a resolved job list.</summary>
        internal static string BuildAnnouncement(List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
                return T("No unlocked jobs can equip");

            return string.Format(T("Can equip: {0}"), string.Join(", ", jobNames));
        }
    }
}
