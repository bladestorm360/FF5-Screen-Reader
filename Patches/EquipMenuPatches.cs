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
    /// <summary>
    /// Holds whichever piece of equipment the equip menu currently has focused, so the I and U
    /// details keys work there the way they do in the items menu.
    ///
    /// Stores extracted values rather than a typed object because the two panes hand over
    /// different types: the item-select list yields ItemListContentData, the slot pane yields
    /// OwnedItemData. Both expose a name, a description and a content type/id, which is all the
    /// details keys need.
    ///
    /// Before this existed, pressing I inside the equip menu fell through to ItemMenuTracker —
    /// whose ValidateState() is a bare `return IsActive` with no liveness check — and read the
    /// last row focused in the ITEMS menu. Backing out of Items fires MainMenuController.InitNone,
    /// which clears nothing, so the stale flag survived. Hence SetFocus also clears the item and
    /// job/ability trackers, exactly as the job/ability announcers do.
    /// </summary>
    public static class EquipMenuTracker
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.EQUIP_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.EQUIP_MENU, value);
        }

        public static string LastName { get; private set; }
        public static string LastDescription { get; private set; }
        public static int LastItemType { get; private set; }
        public static int LastItemId { get; private set; }

        public static bool ValidateState() => IsActive;

        // One-shot per pane. BuildEquipJobsAnnouncement gates on content type 2 (weapon) /
        // 3 (armor). The select pane supplies ItemListContentData.ItemType, the slot pane
        // supplies OwnedItemData.TypeId — very likely the same space, but unconfirmed. If the U
        // key is silent on real equipment, this line says which value arrived.
        private static bool _loggedSelect;
        private static bool _loggedSlot;

        internal static void LogTypeOnce(string pane, int itemType, int itemId)
        {
            if (pane == "select") { if (_loggedSelect) return; _loggedSelect = true; }
            else { if (_loggedSlot) return; _loggedSlot = true; }

            MelonLogger.Msg($"[EquipDetails] {pane} pane: itemType={itemType} itemId={itemId} "
                + "(expect 2=weapon / 3=armor for the U key to resolve jobs)");
        }

        /// <summary>Records the focused piece and takes ownership of the details keys.</summary>
        public static void SetFocus(string name, string description, int itemType, int itemId)
        {
            IsActive = true;
            LastName = name;
            LastDescription = description;
            LastItemType = itemType;
            LastItemId = itemId;

            // Equip has focus now, so no other menu's remembered row is current.
            ItemMenuTracker.ClearState();
            JobAbilityTrackerHelper.ClearAllTrackers();
        }

        public static void ClearState()
        {
            IsActive = false;
            LastName = null;
            LastDescription = null;
            LastItemType = 0;
            LastItemId = 0;
        }
    }

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

        // Frame of the last LB/RB character switch: a slot read within QueueSlotFrames of it queues
        // behind the character name instead of cutting it off. A frame stamp rather than a flag, so
        // a switch whose follow-up read was superseded can't leave a later, unrelated read queued.
        private static int _queueSlotFrame = -1000;
        private const int QueueSlotFrames = 10;

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

                    // Track for the I/U details keys. OwnedItemData is not an
                    // ItemListContentData, so the pieces are extracted rather than the object
                    // being handed over. Note the game's own typo: the description property on
                    // OwnedItemData is spelled "Deiscription" (dump.cs:364383), which is why
                    // this pane never spoke one — ".Description" simply does not exist here.
                    string slotDescription = itemData.Deiscription;
                    EquipMenuTracker.SetFocus(itemData.Name, slotDescription,
                                              itemData.TypeId, itemData.ItemId);
                    EquipMenuTracker.LogTypeOnce("slot", itemData.TypeId, itemData.ItemId);

                    // Auto Detail gates the stat line and the description together, so F7 off
                    // leaves a bare name for fast scrolling. Both stay available on the I key.
                    if (PreferencesManager.AutoDetailEnabled)
                    {
                        // Get parameter message (ATK +15, DEF +8, etc.)
                        string paramMessage = itemData.ParameterMessage;
                        if (!string.IsNullOrEmpty(paramMessage))
                        {
                            equippedItem += ", " + paramMessage;
                        }

                        if (!string.IsNullOrEmpty(slotDescription))
                        {
                            equippedItem += ", " + slotDescription;
                        }
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

            bool queue = UnityEngine.Time.frameCount - _queueSlotFrame <= QueueSlotFrames;
            _queueSlotFrame = -1000;
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: !queue);
            return true;
        }

        /// <summary>
        /// LB/RB switch: speak the new character ("Name, Job"), then, if the slot pane has focus,
        /// re-read the focused slot for that character. UpdateView has already refilled the view by
        /// the time SetNextPlayer/SetPrevPlayer return. The switch also works from the command bar
        /// (CommandUpdate), where only the name is read: the slot guard is set only while the slot
        /// pane has focus (CommandInit and the item list both clear it). The guard is cleared because
        /// the cursor usually stays on the same row; if the game's own SelectContent fires first it
        /// speaks the slot and the deferred read is swallowed by the guard, so it is never read twice.
        /// </summary>
        public static void AnnounceCharacterSwitch(EquipmentInfoWindowController controller)
        {
            var view = controller?.view;                         // EquipmentInfoWindowView @ 0x40
            if (view == null) return;

            string name = view.nameText?.text?.Trim();           // @ 0x18
            string job = view.jobNameText?.text?.Trim();         // @ 0x20
            if (!string.IsNullOrEmpty(name))
                FFV_ScreenReaderMod.SpeakText(string.IsNullOrEmpty(job) ? name : $"{name}, {job}");

            if (_lastSlotIndex < 0) return;

            _lastSlotIndex = -1;
            _queueSlotFrame = UnityEngine.Time.frameCount;
            MenuFocusAnnouncer.Request("EquipSwitch", () =>
            {
                if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
                var cursor = controller.selectCursor;            // @ 0x60
                if (cursor == null) return false;
                AnnounceEquipSlot(controller, cursor.Index);
                return true;
            });
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

            string description = StripIconMarkup(equipmentData.Description);

            // Track for the I/U details keys. This pane's rows ARE ItemListContentData — the
            // same type the items menu uses — so ItemType/ItemId feed BuildEquipJobsAnnouncement
            // directly.
            EquipMenuTracker.SetFocus(itemName, description,
                                      equipmentData.ItemType, equipmentData.ItemId);
            EquipMenuTracker.LogTypeOnce("select", equipmentData.ItemType, equipmentData.ItemId);

            // Auto Detail gates the stat line and the description together, matching the slot
            // pane. Previously both were spoken unconditionally and F7 did nothing here.
            if (PreferencesManager.AutoDetailEnabled)
            {
                // Add mechanical info (ATK +15, DEF +8, etc.)
                string paramMessage = StripIconMarkup(equipmentData.ParameterMessage);
                if (!string.IsNullOrEmpty(paramMessage))
                {
                    announcement += $", {paramMessage}";
                }

                if (!string.IsNullOrEmpty(description))
                {
                    announcement += $", {description}";
                }
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
    /// LB/RB character switch in the slot pane. SetNextPlayer/SetPrevPlayer (private, unique RVAs
    /// 0x4C67C0 / 0x4C69C0) run once per switch from the page-turn coroutines. Do NOT hook
    /// UpdateSwitchCharacter instead: CommandUpdate/InfoUpdate call it every frame.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentInfoWindowController), "SetNextPlayer")]
    public static class EquipmentInfoWindowController_SetNextPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EquipmentInfoWindowController __instance)
        {
            try { EquipMenuState.AnnounceCharacterSwitch(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"Error in EquipmentInfoWindowController.SetNextPlayer patch: {ex.Message}"); }
        }
    }

    [HarmonyPatch(typeof(EquipmentInfoWindowController), "SetPrevPlayer")]
    public static class EquipmentInfoWindowController_SetPrevPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EquipmentInfoWindowController __instance)
        {
            try { EquipMenuState.AnnounceCharacterSwitch(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"Error in EquipmentInfoWindowController.SetPrevPlayer patch: {ex.Message}"); }
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
