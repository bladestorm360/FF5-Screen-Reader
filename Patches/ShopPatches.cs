using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks shop menu state to prevent 'I' key from working outside shop menus.
    /// Delegates IsShopMenuActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class ShopMenuTracker
    {
        public static bool IsShopMenuActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.SHOP_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.SHOP_MENU, value);
        }
        public static ShopInfoController ActiveInfoController { get; set; }
        public static string LastItemDescription { get; set; }
        public static string LastItemMpCost { get; set; }

        /// <summary>Focused row's display name + content id, for the U key's equip lookup.</summary>
        public static string LastItemName { get; set; }
        public static int LastContentId { get; set; }
        public static bool EnteredEquipmentFromShop { get; set; }
        public static bool IsInShopSession { get; set; }

        /// <summary>
        /// Validates that shop menu is actually active and visible.
        /// Clears stale state if controller is no longer active.
        /// </summary>
        public static bool ValidateState()
        {
            if (IsShopMenuActive && !UnityHelpers.IsControllerActive(ActiveInfoController))
            {
                IsShopMenuActive = false;
                ActiveInfoController = null;
                LastItemDescription = null;
                LastItemMpCost = null;
                LastItemName = null;
                LastContentId = 0;
                EnteredEquipmentFromShop = false;
                return false;
            }
            return IsShopMenuActive;
        }
    }

    /// <summary>
    /// Announces shop item details when 'I' key is pressed
    /// </summary>
    public static class ShopDetailsAnnouncer
    {
        public static void AnnounceCurrentItemDetails(bool interrupt = true)
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
                        FFV_ScreenReaderMod.SpeakText(announcement, interrupt);
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
    /// - Equipment command bar (Equip/Strongest/Remove Everything) via EquipmentCommandView.SetFocus
    ///   with dual-state management for shop/equipment transitions
    ///
    /// Partially Implemented:
    ///   Full equipment submenu (character slots, item lists) still needs additional patches
    /// </summary>
    [HarmonyPatch]
    public static class ShopPatches
    {
        /// <summary>
        /// Announces shop command menu options (Buy, Sell, Back).
        /// </summary>
        [HarmonyPatch(typeof(ShopCommandMenuController), nameof(ShopCommandMenuController.SetCursor))]
        [HarmonyPostfix]
        internal static void AfterShopCommandSetCursor(ShopCommandMenuController __instance, int index)
        {
            try
            {
                // Restore shop state if returning from equipment submenu
                if (ShopMenuTracker.EnteredEquipmentFromShop)
                {
                    ShopMenuTracker.EnteredEquipmentFromShop = false;
                    ShopMenuTracker.IsShopMenuActive = true;
                }

                var content = SelectContentHelper.TryGetItem(__instance?.contentList, index);
                if (content?.view?.nameText == null)
                    return;

                string commandText = content.view.nameText.text;
                if (string.IsNullOrEmpty(commandText))
                    return;

                // Append list position last (Buy / Sell / Back command bar).
                int commandCount = __instance.contentList != null ? __instance.contentList.Count : 0;
                commandText = MenuPosition.Format(commandText, index, commandCount);

                CoroutineManager.StartManaged(DelayedAnnounceShopCommand(commandText));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopCommandSetCursor: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces individual items in shop buy/sell lists with name and price.
        /// </summary>
        [HarmonyPatch(typeof(ShopListItemContentController), nameof(ShopListItemContentController.SetFocus))]
        [HarmonyPostfix]
        internal static void AfterShopItemSetFocus(ShopListItemContentController __instance, bool isFocus)
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

                // Retained for the U key / right stick left equip lookup (UsableByAnnouncer).
                ShopMenuTracker.LastItemName = TextUtils.StripIconMarkup(itemName);
                ShopMenuTracker.LastContentId = __instance.ContentId;

                // Get price from shopListItemContentView
                string price = __instance.shopListItemContentView?.priceText?.text;
                string announcement = string.IsNullOrEmpty(price) ? itemName : $"{itemName}, {price}";

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
        internal static void AfterSetDescription(ShopInfoController __instance, string value)
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

                // Data stored for I-key access; no auto-announce (Q key toggles natively)
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
        internal static void AfterAddCount(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
        }

        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.TakeCount))]
        [HarmonyPostfix]
        internal static void AfterTakeCount(ShopTradeWindowController __instance)
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

        /// <summary>
        /// Manages shop state transitions when entering equipment command bar.
        /// Announcement is handled by EquipmentCommandView.SetFocus patch instead.
        /// </summary>
        [HarmonyPatch(typeof(EquipmentCommandController), nameof(EquipmentCommandController.SetFocus))]
        [HarmonyPostfix]
        internal static void AfterEquipmentCommandSetFocus(EquipmentCommandController __instance, EquipmentCommandId id, bool isFocus)
        {
            if (!isFocus) return;

            // When entering equipment from shop, clear shop state (dual-state pattern)
            if (ShopMenuTracker.IsShopMenuActive)
            {
                ShopMenuTracker.EnteredEquipmentFromShop = true;
                ShopMenuTracker.IsShopMenuActive = false;
                ShopMenuTracker.ActiveInfoController = null;
            }
        }

        /// <summary>
        /// Announces equipment command bar options (Equip, Strongest, Remove Everything)
        /// per-view during navigation. This fires for each view as the cursor moves,
        /// unlike the controller-level SetFocus which only fires once on entry.
        ///
        /// Main-menu path only. Reached from the SHOP the view-level SetFocus does not fire, so
        /// CursorExclusionHelper deliberately lets the generic cursor reader through instead
        /// (the "shop" bypass keyed on EnteredEquipmentFromShop). Returning early here keeps the
        /// two paths mutually exclusive so one cursor move never produces two announcements.
        /// </summary>
        [HarmonyPatch(typeof(EquipmentCommandView), nameof(EquipmentCommandView.SetFocus))]
        [HarmonyPostfix]
        internal static void AfterEquipmentCommandViewSetFocus(EquipmentCommandView __instance, bool isFocus)
        {
            try
            {
                if (!isFocus) return;
                if (ShopMenuTracker.EnteredEquipmentFromShop) return;
                if (__instance?.Data == null) return;

                string commandName = __instance.Data.Name;
                if (string.IsNullOrEmpty(commandName)) return;

                FFV_ScreenReaderMod.SpeakText(commandName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentCommandView.SetFocus patch: {ex.Message}");
            }
        }

        internal static IEnumerator DelayedAnnounceShopCommand(string commandText)
        {
            yield return null; // Wait one frame for UI to update
            FFV_ScreenReaderMod.SpeakText($"{commandText}");
        }

        internal static IEnumerator DelayedAnnounceShopItem(string itemText)
        {
            yield return null; // Wait one frame for UI to update
            FFV_ScreenReaderMod.SpeakText($"{itemText}");

            // Auto Detail: queue the item description/MP after the name+price (same reader as the details key).
            // The one-frame wait lets ShopInfoController.SetDescription populate the description first.
            if (PreferencesManager.AutoDetailEnabled)
                ShopDetailsAnnouncer.AnnounceCurrentItemDetails(interrupt: false);
        }

        internal static IEnumerator DelayedAnnounceQuantity(string quantityText)
        {
            yield return null; // Wait one frame for UI to update
            FFV_ScreenReaderMod.SpeakText($"{quantityText}");
        }
    }

    /// <summary>
    /// Tracks ShopController.Show/Close for shop session lifetime.
    /// Used by InputManager to keep context as Global (not Field) during shop transitions.
    /// </summary>
    [HarmonyPatch]
    public static class ShopSessionPatches
    {
        [HarmonyPatch(typeof(ShopController), nameof(ShopController.Show))]
        [HarmonyPostfix]
        public static void AfterShopShow()
        {
            ShopMenuTracker.IsInShopSession = true;
        }

        [HarmonyPatch(typeof(ShopController), nameof(ShopController.Close))]
        [HarmonyPostfix]
        public static void AfterShopClose()
        {
            ShopMenuTracker.IsInShopSession = false;
        }
    }

}
