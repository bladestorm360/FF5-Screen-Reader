using HarmonyLib;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using System.Collections;
using MelonLoader;
using System;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks shop menu state to prevent 'I' key from working outside shop menus
    /// </summary>
    public static class ShopMenuTracker
    {
        public static bool IsShopMenuActive { get; set; }
        public static ShopInfoController ActiveInfoController { get; set; }
        public static string LastItemDescription { get; set; }
        public static string LastItemMpCost { get; set; }

        /// <summary>
        /// Validates that shop menu is actually active and visible.
        /// Clears stale state if controller is no longer active.
        /// </summary>
        public static bool ValidateState()
        {
            if (IsShopMenuActive && ActiveInfoController != null)
            {
                if (ActiveInfoController.gameObject == null ||
                    !ActiveInfoController.gameObject.activeInHierarchy)
                {
                    // Controller is no longer active, clear state
                    IsShopMenuActive = false;
                    ActiveInfoController = null;
                    LastItemDescription = null;
                    LastItemMpCost = null;
                    return false;
                }
            }
            return IsShopMenuActive;
        }
    }

    /// <summary>
    /// Announces shop item details when 'I' key is pressed
    /// </summary>
    public static class ShopDetailsAnnouncer
    {
        public static void AnnounceCurrentItemDetails()
        {
            try
            {
                // Verify shop menu is actually active
                if (!ShopMenuTracker.ValidateState())
                {
                    return; // Silently fail if not active
                }

                // Double-check with activeInHierarchy
                if (ShopMenuTracker.ActiveInfoController == null ||
                    ShopMenuTracker.ActiveInfoController.gameObject == null ||
                    !ShopMenuTracker.ActiveInfoController.gameObject.activeInHierarchy)
                {
                    // Menu is not visible, clear state
                    ShopMenuTracker.IsShopMenuActive = false;
                    ShopMenuTracker.ActiveInfoController = null;
                    return;
                }

                // Build announcement from stored data
                string announcement = ShopMenuTracker.LastItemDescription;

                if (!string.IsNullOrEmpty(ShopMenuTracker.LastItemMpCost))
                {
                    announcement += $". {ShopMenuTracker.LastItemMpCost}";
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    MelonLogger.Msg($"[Shop Details] {announcement}");
                    FFV_ScreenReaderMod.SpeakText(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing shop details: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patches for shop menu navigation.
    ///
    /// Working:
    /// - Shop command menu (Buy/Sell/Back)
    /// - Item lists for buying/selling (item name + price)
    /// - Item descriptions (description + MP cost)
    /// - Quantity selection (quantity + total price)
    /// - 'I' key support for re-reading item descriptions
    ///
    /// Not Yet Implemented:
    /// - Equipment submenu (entire screen is inaccessible)
    ///   Uses different navigation patterns (RB/LB for characters, LEFT/RIGHT for slots)
    ///   Requires visual debugging to identify the correct controller methods
    /// </summary>
    [HarmonyPatch]
    public static class ShopPatches
    {
        /// <summary>
        /// Announces shop command menu options (Buy, Sell, Back).
        /// </summary>
        [HarmonyPatch(typeof(ShopCommandMenuController), nameof(ShopCommandMenuController.SetCursor))]
        [HarmonyPostfix]
        private static void AfterShopCommandSetCursor(ShopCommandMenuController __instance, int index)
        {
            try
            {
                if (__instance?.contentList == null || index < 0 || index >= __instance.contentList.Count)
                    return;

                var content = __instance.contentList[index];
                if (content?.view?.nameText == null)
                    return;

                string commandText = content.view.nameText.text;
                if (string.IsNullOrEmpty(commandText))
                    return;

                CoroutineManager.StartManaged(DelayedAnnounceShopCommand(commandText));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopCommandSetCursor: {ex.Message}");
            }
        }

        private static string lastAnnouncedItem = "";

        /// <summary>
        /// Announces individual items in shop buy/sell lists with name and price.
        /// </summary>
        [HarmonyPatch(typeof(ShopListItemContentController), nameof(ShopListItemContentController.SetFocus))]
        [HarmonyPostfix]
        private static void AfterShopItemSetFocus(ShopListItemContentController __instance, bool isFocus)
        {
            try
            {
                if (!isFocus || __instance == null)
                    return;

                // Mark shop as active when items are being focused
                ShopMenuTracker.IsShopMenuActive = true;

                // Get item name from iconTextView
                string itemName = __instance.iconTextView?.nameText?.text;
                if (string.IsNullOrEmpty(itemName))
                    return;

                // Get price from shopListItemContentView
                string price = __instance.shopListItemContentView?.priceText?.text;
                string announcement = string.IsNullOrEmpty(price) ? itemName : $"{itemName}, {price}";

                // Store for later description announcement
                lastAnnouncedItem = itemName;

                CoroutineManager.StartManaged(DelayedAnnounceShopItem(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopItemSetFocus: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces item descriptions when they update in the info panel.
        /// </summary>
        [HarmonyPatch(typeof(ShopInfoController), nameof(ShopInfoController.SetDescription))]
        [HarmonyPostfix]
        private static void AfterSetDescription(ShopInfoController __instance, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return;

                // Store the controller and description for 'I' key access
                ShopMenuTracker.ActiveInfoController = __instance;
                ShopMenuTracker.LastItemDescription = value;

                // Also get MP cost if available
                string mpCost = __instance.itemInfoController?.shopItemInfoView?.mpText?.text;
                ShopMenuTracker.LastItemMpCost = mpCost;

                // Build the announcement
                string announcement = value;
                if (!string.IsNullOrEmpty(mpCost))
                {
                    announcement = $"{value}. {mpCost}";
                }

                CoroutineManager.StartManaged(DelayedAnnounceDescription(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AfterSetDescription: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces quantity changes in the buy/sell trade window.
        /// </summary>
        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.AddCount))]
        [HarmonyPostfix]
        private static void AfterAddCount(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
        }

        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.TakeCount))]
        [HarmonyPostfix]
        private static void AfterTakeCount(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
        }

        private static void AnnounceTradeWindowQuantity(ShopTradeWindowController controller)
        {
            try
            {
                if (controller?.view == null)
                    return;

                // Get quantity and total price
                string quantity = controller.view.selectCountText?.text;
                string totalPrice = controller.view.totarlPriceText?.text;

                if (!string.IsNullOrEmpty(quantity))
                {
                    string announcement = string.IsNullOrEmpty(totalPrice)
                        ? quantity
                        : $"{quantity}, {totalPrice}";

                    CoroutineManager.StartManaged(DelayedAnnounceQuantity(announcement));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceTradeWindowQuantity: {ex.Message}");
            }
        }

        private static IEnumerator DelayedAnnounceShopCommand(string commandText)
        {
            yield return null; // Wait one frame for UI to update
            MelonLogger.Msg($"[Shop Command] {commandText}");
            FFV_ScreenReaderMod.SpeakText($"{commandText}");
        }

        private static IEnumerator DelayedAnnounceShopItem(string itemText)
        {
            yield return null; // Wait one frame for UI to update
            MelonLogger.Msg($"[Shop Item] {itemText}");
            FFV_ScreenReaderMod.SpeakText($"{itemText}");
        }

        private static IEnumerator DelayedAnnounceDescription(string descriptionText)
        {
            yield return null; // Wait one frame for UI to update
            MelonLogger.Msg($"[Shop Description] {descriptionText}");
            FFV_ScreenReaderMod.SpeakText($"{descriptionText}");
        }

        private static IEnumerator DelayedAnnounceQuantity(string quantityText)
        {
            yield return null; // Wait one frame for UI to update
            MelonLogger.Msg($"[Shop Quantity] {quantityText}");
            FFV_ScreenReaderMod.SpeakText($"{quantityText}");
        }
    }

    // NOTE: OnHide method does not exist in FF5's ShopCommandMenuController
    // Shop state is managed through ValidateState() checks instead
}
