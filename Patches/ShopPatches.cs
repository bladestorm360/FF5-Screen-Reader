using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

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

        /// <summary>The open shop, captured by ShopController.Show for its state machine.</summary>
        public static ShopController ActiveShopController { get; set; }

        // ShopController.State (dump.cs 478566): SelectCommand = 1 is the command bar.
        public const int STATE_SELECT_COMMAND = 1;

        // ShopController.stateMachine @ 0x98 -> StateMachine<State>.current @ 0x10 -> State<T>.Tag @ 0x10.
        // The state enum is a private nested type, so it is read by pointer, not through interop.
        private const int OFFSET_STATE_MACHINE = 0x98;
        private const int OFFSET_CURRENT_STATE = 0x10;
        private const int OFFSET_STATE_TAG = 0x10;

        /// <summary>The open shop's ShopController.State, or -1 when it can't be read.</summary>
        public static unsafe int CurrentShopState()
        {
            try
            {
                var controller = ActiveShopController;
                if (controller == null || controller.Pointer == IntPtr.Zero) return -1;

                IntPtr stateMachine = *(IntPtr*)((byte*)controller.Pointer + OFFSET_STATE_MACHINE);
                if (stateMachine == IntPtr.Zero) return -1;

                IntPtr current = *(IntPtr*)((byte*)stateMachine + OFFSET_CURRENT_STATE);
                if (current == IntPtr.Zero) return -1;

                return *(int*)((byte*)current + OFFSET_STATE_TAG);
            }
            catch
            {
                return -1;
            }
        }

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
        // Same-frame repeat guard for the command bar (see AfterShopCommandSetCursor).
        private static int _lastCommandIndex = -1;
        private static int _lastCommandFrame = -10;

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

                // Only while the command bar is the active panel. SetCursor also runs from
                // ShopInfoController.Reset on open and, through SetCommandFocus, from the
                // InitSelectProduct / InitSelectSellItem / InitSelectEquipment state entries —
                // which spoke "Buy" twice on entry and again on stepping into a list. Entering or
                // backing out to the bar (InitSelectCommand) runs in SelectCommand and still speaks.
                int state = ShopMenuTracker.CurrentShopState();
                if (state >= 0 && state != ShopMenuTracker.STATE_SELECT_COMMAND)
                    return;

                // InitSelectCommand reaches SetCursor twice in the same frame (ShopInfoController.Reset
                // and SetCommandFocus); speak the row once instead of a cut-off repeat.
                int frame = UnityEngine.Time.frameCount;
                if (index == _lastCommandIndex && frame - _lastCommandFrame <= 1)
                    return;
                _lastCommandIndex = index;
                _lastCommandFrame = frame;

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

                var (index, count) = GetListPosition(__instance);

                // Get item name from iconTextView. An empty sell slot has none: say so, and keep the
                // U key's target on the last real item.
                string itemName = __instance.iconTextView?.nameText?.text;
                if (string.IsNullOrEmpty(TextUtils.StripIconMarkup(itemName)))
                {
                    CoroutineManager.StartManaged(SpeechHelper.DelayedSpeech(MenuPosition.Format(T("Empty"), index, count)));
                    return;
                }

                // Retained for the U key / right stick left equip lookup (UsableByAnnouncer).
                ShopMenuTracker.LastItemName = TextUtils.StripIconMarkup(itemName);
                ShopMenuTracker.LastContentId = __instance.ContentId;

                // Get price from shopListItemContentView
                string price = __instance.shopListItemContentView?.priceText?.text;
                string announcement = string.IsNullOrEmpty(price) ? itemName : $"{itemName}, {price}";

                CoroutineManager.StartManaged(DelayedAnnounceShopItem(MenuPosition.Format(announcement, index, count)));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopItemSetFocus: {ex.Message}");
            }
        }

        /// <summary>
        /// Position of a buy/sell row within its list: the row's index in the parent
        /// ShopListMainContentController.productContentList, over the ACTIVE rows only — the list is a
        /// fixed pool and the unused slots stay inactive. (-1, 0) when it can't be resolved, which
        /// MenuPosition.Format turns into no suffix.
        /// </summary>
        private static (int index, int count) GetListPosition(ShopListItemContentController item)
        {
            try
            {
                var rows = item.GetComponentInParent<ShopListMainContentController>()?.productContentList;
                if (rows == null) return (-1, 0);

                int index = -1, count = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null || row.gameObject == null || !row.gameObject.activeInHierarchy) continue;
                    if (row.Pointer == item.Pointer) index = count;
                    count++;
                }
                return (index, count);
            }
            catch
            {
                return (-1, 0);
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
        /// Announces the starting quantity and total when the buy/sell trade window opens.
        /// Postfix takes __instance only: Show has string parameters.
        /// </summary>
        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.Show))]
        [HarmonyPostfix]
        internal static void AfterTradeWindowShow(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
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

                CoroutineManager.StartManaged(DelayedAnnounceQuantity(controller));
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

        /// <summary>"Quantity: 3, Total: 450" — read after one frame, once the view has refreshed.</summary>
        private static IEnumerator DelayedAnnounceQuantity(ShopTradeWindowController controller)
        {
            yield return null;

            try
            {
                if (controller?.view == null) yield break;

                int quantity = controller.selectedCount;       // private int @ 0x3C
                string totalPrice = controller.view.totarlPriceText?.text?.Trim();

                FFV_ScreenReaderMod.SpeakText(string.IsNullOrEmpty(totalPrice)
                    ? string.Format(T("Quantity: {0}"), quantity)
                    : string.Format(T("Quantity: {0}, Total: {1}"), quantity, totalPrice));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing trade quantity: {ex.Message}");
            }
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
        public static void AfterShopShow(ShopController __instance)
        {
            ShopMenuTracker.IsInShopSession = true;
            ShopMenuTracker.ActiveShopController = __instance;
        }

        [HarmonyPatch(typeof(ShopController), nameof(ShopController.Close))]
        [HarmonyPostfix]
        public static void AfterShopClose()
        {
            ShopMenuTracker.IsInShopSession = false;
            ShopMenuTracker.ActiveShopController = null;
        }
    }

}
