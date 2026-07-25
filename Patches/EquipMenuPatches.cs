using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.TextUtils;

using EquipmentWindowController = Il2CppLast.UI.KeyInput.EquipmentWindowController;
using EquipmentCommandController = Il2CppLast.UI.KeyInput.EquipmentCommandController;
using EquipmentInfoWindowController = Il2CppLast.UI.KeyInput.EquipmentInfoWindowController;
using EquipmentSelectWindowController = Il2CppLast.UI.KeyInput.EquipmentSelectWindowController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announce helpers for the three equipment panes, shared by the navigation postfixes and the
    /// initial-focus path (FieldEquipReannouncePatches) so both produce the same string. Each
    /// returns TRUE when it spoke and FALSE when the data isn't usable yet, which is what lets the
    /// MenuFocusAnnouncer settle loop retry until the pane is built.
    /// </summary>
    public static class EquipMenuState
    {
        // Scroll-view / cursor-settle re-fires on an unchanged row.
        private static string _lastCommand;
        private static string _lastSlot;
        private static string _lastSelectRow;

        /// <summary>
        /// Clears the guards so a pane (re)entry always announces, even when the focused row is
        /// the one that was last spoken before leaving.
        /// </summary>
        public static void ClearLastAnnouncements()
        {
            _lastCommand = null;
            _lastSlot = null;
            _lastSelectRow = null;
        }

        /// <summary>Announces one equipment command-bar entry. Returns true when it spoke.</summary>
        public static bool AnnounceEquipCommand(EquipmentCommandController controller, int index)
        {
            if (controller == null) return false;

            var contents = controller.contents;                 // List<EquipmentCommandView> @ 0x30
            if (contents == null || contents.Count == 0) return false;
            if (index < 0 || index >= contents.Count) return false;

            var view = contents[index];
            if (view == null || view.Data == null) return false;

            string commandName = view.Data.Name;
            if (string.IsNullOrEmpty(commandName)) return false;

            if (commandName == _lastCommand) return false;
            _lastCommand = commandName;

            FFV_ScreenReaderMod.SpeakText(commandName);
            return true;
        }

        /// <summary>Announces one equipment slot ("Right hand: Broadsword, ATK +12").</summary>
        public static bool AnnounceEquipSlot(EquipmentInfoWindowController controller, int index)
        {
            if (controller == null) return false;

            var contentList = controller.contentList;           // List<EquipmentInfoContentView> @ 0x70
            if (contentList == null || contentList.Count == 0) return false;
            if (index < 0 || index >= contentList.Count) return false;

            string slotName = null;
            string equippedItem = null;

            var contentView = contentList[index];
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

            if (string.IsNullOrEmpty(announcement)) return false;

            // Filter icon markup
            announcement = StripIconMarkup(announcement);

            // Append slot position last.
            announcement = MenuPosition.Format(announcement, index, contentList.Count);

            if (announcement == _lastSlot) return false;
            _lastSlot = announcement;

            FFV_ScreenReaderMod.SpeakText(announcement);
            return true;
        }

        /// <summary>Announces one row of the equipment item-select list.</summary>
        public static bool AnnounceEquipSelect(EquipmentSelectWindowController controller, int index)
        {
            if (controller == null) return false;

            var contentList = controller.ContentDataList;       // List<ItemListContentData> @ 0x50
            if (contentList == null || contentList.Count == 0) return false;
            if (index < 0 || index >= contentList.Count) return false;

            var equipmentData = contentList[index];
            if (equipmentData == null) return false;

            // Remove icon markup from name
            string itemName = StripIconMarkup(equipmentData.Name);
            if (string.IsNullOrEmpty(itemName)) return false;

            // Build announcement with equipment details
            string announcement = itemName;

            // Add mechanical info (ATK +15, DEF +8, etc.)
            string paramMessage = StripIconMarkup(equipmentData.ParameterMessage);
            if (!string.IsNullOrEmpty(paramMessage))
            {
                announcement += $", {paramMessage}";
            }

            // Add description if available
            string description = StripIconMarkup(equipmentData.Description);
            if (!string.IsNullOrEmpty(description))
            {
                announcement += $", {description}";
            }

            // Append list position last (after mechanical info/description).
            announcement = MenuPosition.Format(announcement, index, contentList.Count);

            if (announcement == _lastSelectRow) return false;
            _lastSelectRow = announcement;

            FFV_ScreenReaderMod.SpeakText(announcement);
            return true;
        }
    }

    // Patch EquipmentSelectWindowController.SetCursor to announce equipment when navigating
    [HarmonyPatch(typeof(EquipmentSelectWindowController), "SetCursor", new Type[] {
        typeof(Il2CppLast.UI.Cursor),
        typeof(bool),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class EquipmentSelectWindowController_SetCursor_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EquipmentSelectWindowController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null) return;
                EquipMenuState.AnnounceEquipSelect(__instance, targetCursor.Index);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentSelectWindowController.SetCursor patch: {ex.Message}");
            }
        }
    }

    // Patch EquipmentInfoWindowController.SelectContent to announce equipment slots when navigating
    [HarmonyPatch(typeof(EquipmentInfoWindowController), "SelectContent", new Type[] {
        typeof(Il2CppLast.UI.Cursor)
    })]
    public static class EquipmentInfoWindowController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EquipmentInfoWindowController __instance, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null) return;
                EquipMenuState.AnnounceEquipSlot(__instance, targetCursor.Index);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentInfoWindowController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces the initially-focused row when an equipment pane is entered or returned to.
    /// SetCursor / SelectContent only fire on cursor movement, so the row the game starts on was
    /// silent.
    ///
    /// All three panes hang off the single EquipmentWindowController state machine, so one hook
    /// per pane covers both entry points — the field menu AND the shop, which share this
    /// controller (Initalize(bool isShop, MainMenuController) / (bool isShop, ShopController)).
    /// Do not additionally hook EquipmentInfoWindowController.InitializeSelectContent or
    /// EquipmentCommandController.ResetCursor: redundant, and a double-fire source.
    /// </summary>
    public static class FieldEquipReannouncePatches
    {
        // EquipmentWindowController (KeyInput, dump.cs 464853): commandController 0x38,
        //   infoWindowController 0x40, selectWindowController 0x48
        // EquipmentCommandController (463133): contents 0x30, selectCursor 0x38
        // EquipmentInfoWindowController (463507): selectCursor 0x60, contentList 0x70
        // EquipmentSelectWindowController (464122): contentDataList 0x50, selectCursor 0x60
        // All read typed below; offsets documented for traceability only.

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, "CommandInit", nameof(Command_Init_Postfix));
            Patch(harmony, "InfoInit", nameof(Info_Init_Postfix));
            Patch(harmony, "SelectInit", nameof(Select_Init_Postfix));

            // NoneInit is deliberately NOT patched: its body is empty, and IL2CPP folds every
            // empty method in the game onto ONE shared native address (0x2711A0 / 2561440 here,
            // backing 4398 methods). Patching it detours all of them at once and hard-crashes on
            // launch with no managed exception. Verify with script.json before hooking any *Init:
            // if two entries share an "Address", it is a folded stub. Cancelling a pending read is
            // only a nicety anyway — the generation latch and the IsUsable gate already stop a
            // stale settle loop from speaking.
        }

        public static void Command_Init_Postfix(object __instance)
        {
            var controller = __instance as EquipmentWindowController;
            if (controller == null) return;

            EquipMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("EquipCommand", () => TryAnnounceCommand(controller));
        }

        public static void Info_Init_Postfix(object __instance)
        {
            var controller = __instance as EquipmentWindowController;
            if (controller == null) return;

            EquipMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("EquipSlot", () => TryAnnounceSlot(controller));
        }

        public static void Select_Init_Postfix(object __instance)
        {
            var controller = __instance as EquipmentWindowController;
            if (controller == null) return;

            EquipMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("EquipSelect", () => TryAnnounceSelect(controller));
        }

        private static bool TryAnnounceCommand(EquipmentWindowController window)
        {
            if (!IsUsable(window)) return false;

            var commandController = window.commandController;
            if (commandController == null) return false;

            var cursor = commandController.selectCursor;
            if (cursor == null) return false;

            return EquipMenuState.AnnounceEquipCommand(commandController, cursor.Index);
        }

        private static bool TryAnnounceSlot(EquipmentWindowController window)
        {
            if (!IsUsable(window)) return false;

            var infoController = window.infoWindowController;
            if (infoController == null) return false;

            var cursor = infoController.selectCursor;
            if (cursor == null) return false;

            return EquipMenuState.AnnounceEquipSlot(infoController, cursor.Index);
        }

        private static bool TryAnnounceSelect(EquipmentWindowController window)
        {
            if (!IsUsable(window)) return false;

            var selectController = window.selectWindowController;
            if (selectController == null) return false;

            var cursor = selectController.selectCursor;
            if (cursor == null) return false;

            return EquipMenuState.AnnounceEquipSelect(selectController, cursor.Index);
        }

        /// <summary>
        /// The equipment window is reachable from the field menu (a MenuManager menu) and from a
        /// shop (which is not), so accept either — but never the scene-construction flurry where
        /// neither is true.
        /// </summary>
        private static bool IsUsable(EquipmentWindowController window)
        {
            if (!MenuFocusAnnouncer.IsAlive(window)) return false;
            return MenuFocusAnnouncer.IsMenuOpen() || ShopMenuTracker.IsInShopSession;
        }

        private static void Patch(HarmonyLib.Harmony harmony, string methodName, string postfixName)
        {
            try
            {
                var target = AccessTools.Method(typeof(EquipmentWindowController), methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[EquipMenu] EquipmentWindowController.{methodName} not found");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(FieldEquipReannouncePatches).GetMethod(postfixName)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EquipMenu] Failed to patch EquipmentWindowController.{methodName}: {ex.Message}");
            }
        }
    }
}
