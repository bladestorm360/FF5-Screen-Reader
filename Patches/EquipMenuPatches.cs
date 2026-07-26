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
        // Scroll-view / cursor-settle re-fires on an unchanged row, one guard per pane. Each guard
        // is only ever consulted within its own pane: the slot and item-list announcers clear each
        // other's guard on focus change, so moving between panes always re-announces.
        //
        // Keyed on INDEX, never on the built string. Both panes routinely render identical text
        // for different rows — two unequipped slots both read "Empty, Attack +3", and a duplicate
        // of the same weapon appears twice in the item list — and a text guard made arrowing
        // between them silent. Position is what changed, so position is what the guard compares.
        private static int _lastCommandIndex = -1;
        private static int _lastSlotIndex = -1;
        private static int _lastSelectRowIndex = -1;

        /// <summary>
        /// Clears every guard, so entering the equipment window from the command bar always
        /// announces even when the focused row is the one last spoken before leaving.
        /// </summary>
        public static void ClearLastAnnouncements()
        {
            _lastCommandIndex = -1;
            _lastSlotIndex = -1;
            _lastSelectRowIndex = -1;
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

            if (index == _lastCommandIndex) return false;
            _lastCommandIndex = index;

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

            // Focus is on the slot pane, so whatever row the item list last spoke is stale.
            // This cross-pane invalidation is what lets both panes drop their *Init hooks:
            // each guard now only ever suppresses a re-fire on an unchanged row *within* its
            // own pane, which is all it was ever for. Done before the dedup check so it still
            // happens when this call is itself a re-fire.
            _lastSelectRowIndex = -1;

            if (index == _lastSlotIndex) return false;
            _lastSlotIndex = index;

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

            // Mirror of the invalidation in AnnounceEquipSlot: focus is on the item list, so the
            // slot pane's remembered row is stale and cancelling back to it must re-announce.
            _lastSlotIndex = -1;

            if (index == _lastSelectRowIndex) return false;
            _lastSelectRowIndex = index;

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
    /// Announces the initially-focused row of the equipment COMMAND bar on entry, which has no
    /// navigation patch of its own to do it.
    ///
    /// The hook hangs off the single EquipmentWindowController state machine, so it covers both
    /// entry points — the field menu AND the shop, which share this controller
    /// (Initalize(bool isShop, MainMenuController) / (bool isShop, ShopController)).
    ///
    /// Adding hooks here is a recurring double-fire source. Before hooking a pane, check whether
    /// its navigation patch already fires during initialisation; if it does, the *Init hook is
    /// redundant and will read the row twice. Confirmed redundant and NOT hooked: InfoInit and
    /// SelectInit (see ApplyPatches), EquipmentInfoWindowController.InitializeSelectContent,
    /// EquipmentCommandController.ResetCursor.
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

            // InfoInit and SelectInit are deliberately NOT patched. Both of those panes already
            // have a navigation announcer that ALSO fires while the pane initialises —
            // EquipmentInfoWindowController.SelectContent and
            // EquipmentSelectWindowController.SetCursor both run on entry, before the *Init hook.
            // Hooking *Init as well read the focused row twice: the navigation patch spoke and
            // set its guard, then the *Init postfix's ClearLastAnnouncements() wiped that guard
            // and the deferred read spoke the identical line one frame later. The clear is what
            // unmasked the duplicate, so no guard could have absorbed it.
            //
            // Entry and re-entry still announce, because AnnounceEquipSlot/AnnounceEquipSelect
            // now invalidate each other's guard on focus change (see EquipMenuState). Those two
            // panes are always entered from one another or from the command bar, whose CommandInit
            // still clears everything.
            //
            // The command bar keeps its hook: EquipmentCommandController has no navigation patch
            // at all, so nothing else would announce its focused entry.

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

        private static bool TryAnnounceCommand(EquipmentWindowController window)
        {
            if (!IsUsable(window)) return false;

            var commandController = window.commandController;
            if (commandController == null) return false;

            var cursor = commandController.selectCursor;
            if (cursor == null) return false;

            return EquipMenuState.AnnounceEquipCommand(commandController, cursor.Index);
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
