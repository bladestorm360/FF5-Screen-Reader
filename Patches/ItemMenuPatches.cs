using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI;
using Il2CppLast.Defaine;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using UnityEngine;
using Il2CppLast.Management;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Track item menu state for I key handling.
    /// Delegates IsActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class ItemMenuTracker
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.ITEM_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.ITEM_MENU, value);
        }

        public static ItemListContentData LastSelectedItem { get; set; }

        public static bool ValidateState()
        {
            return IsActive;
        }

        public static void ClearState()
        {
            IsActive = false;
            LastSelectedItem = null;
        }
    }

    /// <summary>
    /// Track ItemUseController state to avoid expensive FindObjectOfType calls.
    /// Used by BattleCommandPatches to suppress announcements during item use targeting.
    /// Delegates IsItemUseActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class ItemUseTracker
    {
        public static bool IsItemUseActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.ITEM_USE);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.ITEM_USE, value);
        }
    }

    /// <summary>
    /// Announce helpers shared by the navigation postfixes and the initial-focus path
    /// (FieldItemReannouncePatches), so both produce exactly the same string. Each returns TRUE
    /// when it spoke and FALSE when the data isn't usable yet, which is what lets the
    /// MenuFocusAnnouncer settle loop retry until the list is built.
    /// </summary>
    public static class ItemMenuState
    {
        // SelectContent carries a WithinRangeType — the scroll view re-invokes it on range
        // recalculation with an unchanged row. Also debounces Auto Detail, whose queued
        // (interrupt:false) description would otherwise play twice back to back.
        private static string _lastItemAnnouncement;
        private static string _lastTargetAnnouncement;
        private static string _lastCommandAnnouncement;

        /// <summary>
        /// Clears the guards so a menu (re)entry always announces, even when the focused row is
        /// the one that was last spoken before leaving.
        /// </summary>
        public static void ClearLastAnnouncements()
        {
            _lastItemAnnouncement = null;
            _lastTargetAnnouncement = null;
            _lastCommandAnnouncement = null;
        }

        /// <summary>
        /// Announces one item command-bar entry (Use / Key Items / Sort / ...).
        /// Mirrors EquipMenuState.AnnounceEquipCommand. Returns true when it spoke.
        /// </summary>
        public static bool AnnounceItemCommand(ItemCommandController controller, int index)
        {
            if (controller == null) return false;

            var contentList = controller.contentList;           // List<ItemCommandContentView> @ 0x40
            if (contentList == null || contentList.Count == 0) return false;
            if (index < 0 || index >= contentList.Count) return false;

            var view = contentList[index];
            if (view == null || view.Data == null) return false;

            string commandName = view.Data.Name;
            if (string.IsNullOrEmpty(commandName)) return false;

            if (commandName == _lastCommandAnnouncement) return false;
            _lastCommandAnnouncement = commandName;

            FFV_ScreenReaderMod.SpeakText(commandName);
            return true;
        }

        /// <summary>Announces one item list row. Returns true when it spoke.</summary>
        public static bool AnnounceItemListData(ItemListContentData itemData, int index, int count)
        {
            if (itemData == null) return false;

            // Track for I key equipment compatibility lookup
            ItemMenuTracker.IsActive = true;
            ItemMenuTracker.LastSelectedItem = itemData;
            JobAbilityTrackerHelper.ClearAllTrackers();

            string itemName = StripIconMarkup(itemData.Name);
            if (string.IsNullOrEmpty(itemName)) return false;

            // Build announcement with item details
            string announcement = itemName;

            // Add quantity if available
            int quantity = itemData.Count;
            if (quantity > 0)
            {
                announcement += $", {quantity}";
            }

            // Auto Detail: append the same description the I key reads on demand.
            // Equip requirements are no longer queued here — they moved to the U key /
            // right stick left, so this line stays short enough to skim while scrolling.
            if (PreferencesManager.AutoDetailEnabled)
            {
                string description = StripIconMarkup(itemData.Description);
                if (!string.IsNullOrEmpty(description))
                {
                    announcement += $", {description}";
                }
            }

            // Append list position last (after quantity/description).
            announcement = MenuPosition.Format(announcement, index, count);

            if (announcement == _lastItemAnnouncement) return false;
            _lastItemAnnouncement = announcement;

            FFV_ScreenReaderMod.SpeakText(announcement);
            return true;
        }

        /// <summary>Announces one item-use target character. Returns true when it spoke.</summary>
        public static bool AnnounceItemUseTarget(ItemTargetSelectContentController content, int index, int count)
        {
            if (content == null || content.CurrentData == null) return false;

            var data = content.CurrentData;
            string characterName = data.Name;
            if (string.IsNullOrEmpty(characterName)) return false;

            string announcement = characterName + CharacterStatusHelper.GetFullStatus(data.Parameter);

            // Append target position last.
            announcement = MenuPosition.Format(announcement, index, count);

            if (announcement == _lastTargetAnnouncement) return false;
            _lastTargetAnnouncement = announcement;

            FFV_ScreenReaderMod.SpeakText(announcement);
            return true;
        }
    }

    /// <summary>
    /// Patches for item and equipment menu navigation in FF5.
    /// Announces item/equipment name, quantity, and description when browsing.
    /// </summary>

    // Patch ItemListController.SelectContent to announce items when navigating
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemListController), "SelectContent", new Type[] {
        typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData>),
        typeof(int),
        typeof(Il2CppLast.UI.Cursor),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class ItemListController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ItemListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData> targets,
            int index,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targets == null) return;

                // Convert IEnumerable to List for indexed access
                var targetList = new Il2CppSystem.Collections.Generic.List<ItemListContentData>(targets);
                if (targetList == null || targetList.Count == 0) return;
                if (index < 0 || index >= targetList.Count) return;

                ItemMenuState.AnnounceItemListData(targetList[index], index, targetList.Count);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ItemListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    // Equipment patches live in EquipMenuPatches.cs (EquipMenuState + the three pane readers).

    // Patch ItemUseController.SelectContent to announce character stats when selecting item targets
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemUseController), "SelectContent", new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.UI.KeyInput.ItemTargetSelectContentController>), typeof(Il2CppLast.UI.Cursor) })]
    public static class ItemUseController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.ItemUseController __instance, Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.UI.KeyInput.ItemTargetSelectContentController> targetContents, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0) return;

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count) return;

                ItemMenuState.AnnounceItemUseTarget(contentList[index], index, contentList.Count);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ItemUseController.SelectContent patch: {ex.Message}");
            }
        }
    }

    // Patch ItemUseController.Show to track when item use targeting becomes active
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemUseController), nameof(Il2CppLast.UI.KeyInput.ItemUseController.Show))]
    public static class ItemUseController_Show_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ItemUseTracker.IsItemUseActive = true;
        }
    }

    // Patch ItemUseController.Close to track when item use targeting ends
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemUseController), nameof(Il2CppLast.UI.KeyInput.ItemUseController.Close))]
    public static class ItemUseController_Close_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ItemUseTracker.IsItemUseActive = false;
            MenuFocusAnnouncer.Cancel();
        }
    }


    /// <summary>
    /// Announces the initially-focused row when an item screen is entered or returned to.
    /// SelectContent only fires on cursor movement, so the row the game starts on was silent.
    ///
    /// Hooks the state-entry *Init methods, which the navigation path never fires — the two are
    /// disjoint, so neither needs to suppress the other. Manual patching because every target is
    /// private.
    ///
    /// Patch ONLY ItemListController and ItemUseController: ItemWindowController (dump.cs 468479)
    /// has its own CommandSelectInit / UseSelectInit / ... state machine that drives these, so
    /// hooking it too would request two reads per entry.
    /// </summary>
    public static class FieldItemReannouncePatches
    {
        // ItemListController (KeyInput, dump.cs 466795): selectCursor 0x60, dataList 0x78
        // ItemUseController (KeyInput, dump.cs 467916): contentList 0x40, selectCursor 0x50
        // Both read typed below; offsets documented for traceability only.

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            // The list states. CommandSelectInit is deliberately NOT here: it is the command bar
            // (Use / Key Items / Sort), not a list, so routing it to the list reader made entering
            // Items announce the first inventory row instead of the focused command. It is hooked
            // on ItemWindowController below, which is the controller that owns commandController.
            foreach (var method in new[] { "UseSelectInit", "ImportantSelectInit",
                                           "OrganizeSelectInit", "SortSelectInit" })
            {
                Patch(harmony, typeof(Il2CppLast.UI.KeyInput.ItemListController), method,
                      nameof(ItemList_Init_Postfix));
            }

            // Command bar entry — the FIRST thing focused when Items opens. Mirrors the equip
            // menu, where EquipmentWindowController.CommandInit reaches through to its own
            // commandController rather than reusing a list reader.
            Patch(harmony, typeof(Il2CppLast.UI.KeyInput.ItemWindowController), "CommandSelectInit",
                  nameof(ItemCommand_Init_Postfix));

            foreach (var method in new[] { "SingleInit", "AllInit" })
            {
                Patch(harmony, typeof(Il2CppLast.UI.KeyInput.ItemUseController), method,
                      nameof(ItemTarget_Init_Postfix));
            }

            // Leaving target selection. Backing out does NOT call Close() — the controller's
            // state machine just returns to its Non state, and Close() only runs when the whole
            // item window closes. Without this, ITEM_USE stayed latched all the way back onto
            // the field and pinned the input context off Field, silently killing the entity
            // scanner, pathfinding, and every field audio cue until the game restarted.
            //
            // Folded-stub check (docs/debug.md): SingleExit is RVA 0xA20600, count 1 — safe.
            // Do NOT reach for the more obvious NonInit or AllExit: both are RVA 0x2715A0, this
            // build's folded empty-method address shared by 2671 methods, so patching either
            // detours thousands of methods and hard-crashes on launch. The All path therefore
            // has no hookable exit and relies on the field-state reset in GameStatePatches.
            Patch(harmony, typeof(Il2CppLast.UI.KeyInput.ItemUseController), "SingleExit",
                  nameof(ItemTarget_Exit_Postfix));
        }

        /// <summary>Clears the item-use flag when single-target selection is left, by any route.</summary>
        public static void ItemTarget_Exit_Postfix()
        {
            ItemUseTracker.IsItemUseActive = false;
        }

        public static void ItemList_Init_Postfix(object __instance)
        {
            var controller = __instance as Il2CppLast.UI.KeyInput.ItemListController;
            if (controller == null) return;

            // A state (re)entry is authoritative: clear the nav guard so the focused row speaks
            // even when it is the row that was last announced before leaving.
            ItemMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("ItemMenu", () => TryAnnounceItemListInitial(controller));
        }

        public static void ItemTarget_Init_Postfix(object __instance)
        {
            var controller = __instance as Il2CppLast.UI.KeyInput.ItemUseController;
            if (controller == null) return;

            ItemMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("ItemTarget", () => TryAnnounceItemTargetInitial(controller));
        }

        public static void ItemCommand_Init_Postfix(object __instance)
        {
            var window = __instance as Il2CppLast.UI.KeyInput.ItemWindowController;
            if (window == null) return;

            ItemMenuState.ClearLastAnnouncements();
            MenuFocusAnnouncer.Request("ItemCommand", () => TryAnnounceItemCommandInitial(window));
        }

        private static bool TryAnnounceItemListInitial(Il2CppLast.UI.KeyInput.ItemListController controller)
        {
            if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
            if (!MenuFocusAnnouncer.IsMenuOpen()) return false; // false during a map/asset load

            var dataList = controller.dataList;
            if (dataList == null) return false;

            var list = new Il2CppSystem.Collections.Generic.List<ItemListContentData>(dataList);
            if (list == null || list.Count == 0) return false;

            var cursor = controller.selectCursor;
            if (cursor == null) return false;

            int index = cursor.Index;
            if (index < 0 || index >= list.Count) return false;

            return ItemMenuState.AnnounceItemListData(list[index], index, list.Count);
        }

        private static bool TryAnnounceItemCommandInitial(Il2CppLast.UI.KeyInput.ItemWindowController window)
        {
            if (!MenuFocusAnnouncer.IsAlive(window)) return false;
            if (!MenuFocusAnnouncer.IsMenuOpen()) return false; // false during a map/asset load

            var commandController = window.commandController;   // ItemCommandController @ 0x38
            if (commandController == null) return false;

            var cursor = commandController.selectCursor;        // Cursor @ 0x50
            if (cursor == null) return false;

            return ItemMenuState.AnnounceItemCommand(commandController, cursor.Index);
        }

        private static bool TryAnnounceItemTargetInitial(Il2CppLast.UI.KeyInput.ItemUseController controller)
        {
            if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
            if (BattleState.IsInBattle) return false;   // battle item targeting has its own reader
            if (!MenuFocusAnnouncer.IsMenuOpen()) return false;

            var contentList = controller.contentList;
            if (contentList == null || contentList.Count == 0) return false;

            var cursor = controller.selectCursor;
            if (cursor == null) return false;

            int index = cursor.Index;
            if (index < 0 || index >= contentList.Count) return false;

            return ItemMenuState.AnnounceItemUseTarget(contentList[index], index, contentList.Count);
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type type, string methodName, string postfixName)
        {
            try
            {
                var target = AccessTools.Method(type, methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[ItemMenu] {type.Name}.{methodName} not found");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(FieldItemReannouncePatches).GetMethod(postfixName)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemMenu] Failed to patch {type.Name}.{methodName}: {ex.Message}");
            }
        }
    }

}
