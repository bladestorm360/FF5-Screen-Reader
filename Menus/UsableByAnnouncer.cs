using System;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Patches;
using FFV_ScreenReader.Utils;

using MasterManager = Il2CppLast.Data.Master.MasterManager;
using Content = Il2CppLast.Data.Master.Content;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Announces which unlocked jobs can equip the focused weapon/armor.
    /// Bound to the U key and to right stick left, matching FF1.
    ///
    /// Works in the Items menu (via ItemDetailsAnnouncer, which reads ItemListContentData
    /// directly) and in shops, where the item is not owned and must be resolved through the
    /// Content master table first.
    /// </summary>
    public static class UsableByAnnouncer
    {
        private static bool hasLoggedShopResolveFailure = false;

        public static void AnnounceForCurrentContext()
        {
            try
            {
                // Shop first, mirroring InputManager.HandleItemInfoKey's cascade order, so a
                // stale ItemMenuTracker can't be read while a shop is open.
                if (ShopMenuTracker.ValidateState())
                {
                    AnnounceShopItem();
                    return;
                }

                // Equip before Item, matching HandleItemInfoKey's order.
                if (EquipMenuTracker.ValidateState())
                {
                    ItemDetailsAnnouncer.AnnounceEquipJobsFor(
                        EquipMenuTracker.LastItemType, EquipMenuTracker.LastItemId);
                    return;
                }

                if (ItemMenuTracker.ValidateState())
                {
                    ItemDetailsAnnouncer.AnnounceEquipRequirements();
                    return;
                }

                // Silent elsewhere — no equip data to report.
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[UsableBy] Error: {ex.Message}");
            }
        }

        private static void AnnounceShopItem()
        {
            if (!TryResolveShopContent(out int itemType, out int itemId))
                return;

            string announcement = ItemDetailsAnnouncer.BuildEquipJobsAnnouncement(itemType, itemId);
            if (string.IsNullOrEmpty(announcement))
                return;

            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
        }

        /// <summary>
        /// Resolves the focused shop row to a (Content.TypeId, Content.TypeValue) pair.
        ///
        /// TypeId is the content type (2=weapon, 3=armor); TypeValue is the row id within the
        /// corresponding Weapon/Armor master table — which is what EquipJobGroupId hangs off.
        /// </summary>
        private static bool TryResolveShopContent(out int itemType, out int itemId)
        {
            itemType = -1;
            itemId = 0;

            string target = TextUtils.StripIconMarkup(ShopMenuTracker.LastItemName)?.Trim();
            if (string.IsNullOrEmpty(target))
                return false;

            var masterManager = MasterManager.Instance;
            if (masterManager == null)
                return false;

            var contentDict = masterManager.GetList<Content>();
            if (contentDict == null)
                return false;

            // Fast path: the row's own ContentId. Validated against the displayed name because
            // ContentId is not guaranteed to be a Content primary key — if it turns out to be a
            // type-local id, the name won't match and we fall through instead of reporting the
            // wrong item's job list.
            int contentId = ShopMenuTracker.LastContentId;
            if (contentId > 0 && contentDict.ContainsKey(contentId))
            {
                var candidate = contentDict[contentId];
                if (candidate != null && NameMatches(candidate, target))
                {
                    itemType = candidate.TypeId;
                    itemId = candidate.TypeValue;
                    return true;
                }
            }

            // Fallback: resolve by display name. Only runs on an explicit key press, and only
            // when the fast path missed.
            try
            {
                foreach (var kvp in contentDict)
                {
                    var content = kvp.Value;
                    if (content == null)
                        continue;

                    if (!NameMatches(content, target))
                        continue;

                    itemType = content.TypeId;
                    itemId = content.TypeValue;
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (!hasLoggedShopResolveFailure)
                {
                    MelonLogger.Warning($"[UsableBy] Content scan failed: {ex.Message}");
                    hasLoggedShopResolveFailure = true;
                }
            }

            return false;
        }

        private static bool NameMatches(Content content, string target)
        {
            try
            {
                string mesId = content.MesIdName;
                if (string.IsNullOrEmpty(mesId))
                    return false;

                string localized = LocalizationHelper.GetGameMessage(mesId);
                if (string.IsNullOrEmpty(localized))
                    return false;

                return string.Equals(
                    TextUtils.StripIconMarkup(localized)?.Trim(),
                    target,
                    StringComparison.Ordinal);
            }
            catch
            {
                return false; // Message resolution can fail for sparse master rows
            }
        }
    }
}
