using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

// FF5 Save/Load UI types
// Touch version: SaveListController has contentList at 0x40, SelectContent(SaveSlotData data).
//   Only instantiated in touch_savewindow_ui — not used by the PC title or field lists.
// KeyInput version (title Load AND field Save/Load on PC): SaveListController has contentList
//   at 0x68, SelectContent(Cursor targetCursor, ...)
using TouchSaveListController = Il2CppLast.UI.Touch.SaveListController;
using KeyInputSaveListController = Il2CppLast.UI.KeyInput.SaveListController;
using KeyInputLoadWindowController = Il2CppLast.UI.KeyInput.LoadWindowController;
using KeyInputSaveWindowController = Il2CppLast.UI.KeyInput.SaveWindowController;
using KeyInputLoadGameWindowController = Il2CppLast.UI.KeyInput.LoadGameWindowController;
using GameCursor = Il2CppLast.UI.Cursor;
using SavePopup = Il2CppLast.UI.KeyInput.SavePopup;
using InterruptionController = Il2CppLast.UI.KeyInput.InterruptionWindowController;
using OverwriteSaveController = Il2CppLast.UI.Save.KeyInput.SaveWindowController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks save/load menu state for suppression.
    /// Delegates IsActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class SaveLoadMenuState
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.SAVE_LOAD_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.SAVE_LOAD_MENU, value);
        }

        public static bool IsInConfirmation { get; set; } = false;

        public static bool ShouldSuppress()
        {
            return IsActive && IsInConfirmation;
        }

        public static void ResetState()
        {
            IsActive = false;
            IsInConfirmation = false;
        }
    }

    /// <summary>
    /// Patches for Save/Load menus.
    /// Hooks SelectContent methods on SaveListController to announce save slots.
    /// Hooks SetPopupActive/SetActive on window controllers to clear state.
    /// </summary>
    public static class SaveLoadPatches
    {
        // SaveContentView field offsets - Touch version (title screen, dump.cs line 434568)
        // slotNameText: 0x20, slotNumText: 0x30, charaNameText: 0x38, levelText: 0x70
        // areaNameText: 0x78, floorNameText: 0x80, emptyText: 0x88
        // hourText: 0x58, minuteText: 0x68, timeStampDate: 0xF0, timeStampTime: 0xF8
        private const int TOUCH_VIEW_SLOT_NAME_TEXT = 0x20;
        private const int TOUCH_VIEW_SLOT_NUM_TEXT = 0x30;
        private const int TOUCH_VIEW_CHARA_NAME_TEXT = 0x38;
        private const int TOUCH_VIEW_LEVEL_TEXT = 0x70;
        private const int TOUCH_VIEW_AREA_NAME_TEXT = 0x78;
        private const int TOUCH_VIEW_FLOOR_NAME_TEXT = 0x80;
        private const int TOUCH_VIEW_EMPTY_TEXT = 0x88;
        private const int TOUCH_VIEW_HOUR_TEXT = 0x58;
        private const int TOUCH_VIEW_MINUTE_TEXT = 0x68;
        private const int TOUCH_VIEW_TIMESTAMP_DATE = 0xF0;
        private const int TOUCH_VIEW_TIMESTAMP_TIME = 0xF8;

        // SaveContentView field offsets - KeyInput version (main menu, dump.cs line 469320)
        // slotNameText: 0x28, slotNumText: 0x38, charaNameText: 0x40, levelText: 0x50
        // areaNameText: 0x58, floorNameText: 0x60, emptyText: 0x88
        // hourText: 0x70, minuteText: 0x80, timeStampDate: 0xD0, timeStampTime: 0xD8
        private const int KEYINPUT_VIEW_SLOT_NAME_TEXT = 0x28;
        private const int KEYINPUT_VIEW_SLOT_NUM_TEXT = 0x38;
        private const int KEYINPUT_VIEW_CHARA_NAME_TEXT = 0x40;
        private const int KEYINPUT_VIEW_LEVEL_TEXT = 0x50;
        private const int KEYINPUT_VIEW_AREA_NAME_TEXT = 0x58;
        private const int KEYINPUT_VIEW_FLOOR_NAME_TEXT = 0x60;
        private const int KEYINPUT_VIEW_EMPTY_TEXT = 0x88;
        private const int KEYINPUT_VIEW_HOUR_TEXT = 0x70;
        private const int KEYINPUT_VIEW_MINUTE_TEXT = 0x80;
        private const int KEYINPUT_VIEW_TIMESTAMP_DATE = 0xD0;
        private const int KEYINPUT_VIEW_TIMESTAMP_TIME = 0xD8;

        // SaveContentController field offsets
        // Touch: view at 0x28 (dump.cs line 434386)
        // KeyInput: view at 0x20 (dump.cs line 469153)
        private const int TOUCH_CONTROLLER_VIEW_OFFSET = 0x28;
        private const int KEYINPUT_CONTROLLER_VIEW_OFFSET = 0x20;

        // SaveContentController.<SlotData>k__BackingField (SaveSlotData): KeyInput 0x38 (dump.cs
        // 469147), Touch 0x50 (dump.cs 434378). SaveSlotData.id at 0x30.
        // The autosave and quick-save rows use the reserved ids above the numbered slots
        // (SaveSlotManager.MaxSlotCount 20, AutoSlotId 21, SuspendedSlotId 22).
        // Every PC save/load list — title Load (key_loadgame), field Save/Load (key_menu,
        // key_savewindow_ui) — is the KeyInput controller; the Touch SaveListController is only
        // instantiated in touch_savewindow_ui. Both are read by id anyway.
        private const int KEYINPUT_CONTROLLER_SLOT_DATA_OFFSET = 0x38;
        private const int TOUCH_CONTROLLER_SLOT_DATA_OFFSET = 0x50;
        private const int SLOT_DATA_ID_OFFSET = 0x30;
        private const int MAX_NUMBERED_SLOT_ID = 20;

        // SaveListController field offsets
        // Touch: contentList at 0x40 (dump.cs line 434801)
        // KeyInput: contentList at 0x68 (dump.cs line 469565)
        private const int TOUCH_LIST_CONTENT_LIST = 0x40;
        private const int KEYINPUT_LIST_CONTENT_LIST = 0x68;

        // SavePopup button navigation offsets (from dump.cs)
        // selectCursor: 0x58 (Cursor), commandList: 0x60 (List<CommonCommand>)
        private const int SAVE_POPUP_SELECT_CURSOR_OFFSET = 0x58;
        private const int SAVE_POPUP_COMMAND_LIST_OFFSET_V2 = 0x60;
        private const int COMMON_COMMAND_TEXT_OFFSET = 0x18;


        // Each controller's SavePopup field (dump.cs): KeyInput LoadGameWindowController (title
        // Load) 0x58, KeyInput LoadWindowController / SaveWindowController (field menu) 0x28,
        // KeyInput InterruptionWindowController (quick save) 0x38, and
        // Save.KeyInput.SaveWindowController view 0x30 → SaveWindowView.savePopup 0x28 (overwrite).
        private const int LOAD_GAME_WINDOW_SAVE_POPUP_OFFSET = 0x58;
        private const int MENU_WINDOW_SAVE_POPUP_OFFSET = 0x28;
        private const int INTERRUPTION_SAVE_POPUP_OFFSET = 0x38;
        private const int OVERWRITE_CONTROLLER_VIEW_OFFSET = 0x30;
        private const int OVERWRITE_VIEW_SAVE_POPUP_OFFSET = 0x28;

        private static int lastAnnouncedIndex = -1;
        private static int lastPopupButtonIndex = -1;

        // The open SavePopup whose Yes/No moves are read (set by the open read, cleared on close).
        // Holding the wrapper keeps the object alive while its cursor pointer is compared.
        private static SavePopup activeSavePopup;

        // True from a popup's open hook until its delayed open read has spoken, so a move in those
        // two frames doesn't speak a button ahead of the message.
        private static bool savePopupOpenReadPending;

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch KeyInput SaveListController.SelectContent for main menu
                TryPatchKeyInputSaveListSelectContent(harmony);

                // Patch Touch SaveListController.SelectContent for title screen
                TryPatchTouchSaveListSelectContent(harmony);

                // Patch SetPopupActive for confirmation dialogs
                TryPatchLoadGameWindowController(harmony);
                TryPatchLoadWindowController(harmony);
                TryPatchSaveWindowController(harmony);

                // Patch SetActive to clear state when menus close
                TryPatchLoadGameWindowSetActive(harmony);
                TryPatchLoadWindowSetActive(harmony);
                TryPatchSaveWindowSetActive(harmony);

                // SavePopup buttons: each controller's own open hook reads the popup (title,
                // message, focused button) and registers it; Yes/No moves are then read by
                // TryReadSavePopupMove from the Cursor.NextIndex/PrevIndex patches. Replaces a
                // postfix on SavePopup.UpdateCommand, which ran every frame (round 2, 2026-09-24).
                TryPatchOverwriteConfirmInit(harmony);

                // Patch InterruptionWindowController for QuickSave popup message
                TryPatchInterruptionController(harmony);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveLoad] Failed to apply patches: {ex.Message}");
            }
        }

        #region SaveListController Patches

        /// <summary>
        /// Patches KeyInput SaveListController.SelectContent (main menu navigation).
        /// Method signature: private void SelectContent(Cursor targetCursor, CustomScrollView.WithinRangeType type = 0)
        /// </summary>
        private static void TryPatchKeyInputSaveListSelectContent(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputSaveListController);
                var method = AccessTools.Method(controllerType, "SelectContent");

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(KeyInputSaveListSelectContent_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] KeyInput SaveListController.SelectContent not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch KeyInput SaveListController: {ex.Message}");
            }
        }

        /// <summary>
        /// Patches Touch SaveListController.SelectContent (title screen navigation).
        /// Method signature: private void SelectContent(SaveSlotData data)
        /// </summary>
        private static void TryPatchTouchSaveListSelectContent(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(TouchSaveListController);
                var method = AccessTools.Method(controllerType, "SelectContent");

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(TouchSaveListSelectContent_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] Touch SaveListController.SelectContent not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch Touch SaveListController: {ex.Message}");
            }
        }

        #endregion

        #region SetPopupActive Patches

        private static void TryPatchLoadGameWindowController(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputLoadGameWindowController);
                var method = AccessTools.Method(controllerType, "SetPopupActive");

                if (method != null)
                {
                    var prefix = typeof(SaveLoadPatches).GetMethod(nameof(LoadGameWindowSetPopupActive_Prefix),
                        BindingFlags.Public | BindingFlags.Static);
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(LoadGameWindowSetPopupActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch LoadGameWindowController.SetPopupActive: {ex.Message}");
            }
        }

        private static void TryPatchLoadWindowController(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputLoadWindowController);
                var method = AccessTools.Method(controllerType, "SetPopupActive");

                if (method != null)
                {
                    var prefix = typeof(SaveLoadPatches).GetMethod(nameof(LoadWindowSetPopupActive_Prefix),
                        BindingFlags.Public | BindingFlags.Static);
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(LoadWindowSetPopupActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] LoadWindowController.SetPopupActive not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch LoadWindowController.SetPopupActive: {ex.Message}");
            }
        }

        private static void TryPatchSaveWindowController(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputSaveWindowController);
                var method = AccessTools.Method(controllerType, "SetPopupActive");

                if (method != null)
                {
                    var prefix = typeof(SaveLoadPatches).GetMethod(nameof(SaveWindowSetPopupActive_Prefix),
                        BindingFlags.Public | BindingFlags.Static);
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(SaveWindowSetPopupActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] SaveWindowController.SetPopupActive not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch SaveWindowController.SetPopupActive: {ex.Message}");
            }
        }

        #endregion

        #region SetActive Patches for State Clearing

        private static void TryPatchLoadGameWindowSetActive(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputLoadGameWindowController);
                var method = AccessTools.Method(controllerType, "SetActive", new Type[] { typeof(bool) });

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(LoadGameWindowSetActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch LoadGameWindowController.SetActive: {ex.Message}");
            }
        }

        private static void TryPatchLoadWindowSetActive(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputLoadWindowController);
                var method = AccessTools.Method(controllerType, "SetActive", new Type[] { typeof(bool) });

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(LoadWindowSetActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch LoadWindowController.SetActive: {ex.Message}");
            }
        }

        private static void TryPatchSaveWindowSetActive(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(KeyInputSaveWindowController);
                var method = AccessTools.Method(controllerType, "SetActive", new Type[] { typeof(bool) });

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(SaveWindowSetActive_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch SaveWindowController.SetActive: {ex.Message}");
            }
        }

        #endregion

        #region Popup Button Navigation Patches

        /// <summary>
        /// Patches OverwriteConfirmInit on Last.UI.Save.KeyInput.SaveWindowController (0x8411B0,
        /// a virtual override; unique RVA): the overwrite confirmation. It drives the SavePopup at
        /// view (0x30) → savePopup (0x28), and calls SavePopup.ResetCursor itself.
        /// </summary>
        private static void TryPatchOverwriteConfirmInit(HarmonyLib.Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(typeof(OverwriteSaveController), "OverwriteConfirmInit");
                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(OverwriteConfirmInit_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] Save.KeyInput.SaveWindowController.OverwriteConfirmInit not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch OverwriteConfirmInit: {ex.Message}");
            }
        }

        /// <summary>
        /// Patches InterruptionWindowController (QuickSave). Three hooks:
        ///   SetEnablePopup — confirmation popup opens/closes
        ///   InitComplite   — completion popup ("Quick save complete")
        ///   Close          — window teardown, clears menu state
        /// </summary>
        private static void TryPatchInterruptionController(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type controllerType = typeof(InterruptionController);
                var method = AccessTools.Method(controllerType, "SetEnablePopup");

                if (method != null)
                {
                    var prefix = typeof(SaveLoadPatches).GetMethod(nameof(InterruptionSetEnablePopup_Prefix),
                        BindingFlags.Public | BindingFlags.Static);
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(InterruptionSetEnablePopup_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] InterruptionWindowController.SetEnablePopup not found");
                }

                // InitComplite (the game's spelling) — dump.cs:465262, RVA 0x802830.
                //
                // QuickSave reuses ONE SavePopup instance across Confirmation -> Complite and has
                // no *Exit methods, so SetEnablePopup never fires a second time, and InitComplite
                // does not call SavePopup.ResetCursor. This is the completion popup's open hook:
                // without it the completion message would never be read.
                var initComplite = AccessTools.Method(controllerType, "InitComplite");
                if (initComplite != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(InterruptionInitComplite_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(initComplite, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] InterruptionWindowController.InitComplite not found -- "
                        + "QuickSave completion popup will read only its button");
                }

                // Close — dump.cs:465232, RVA 0x802400.
                //
                // The other three save/load windows clear menu state from their SetActive
                // postfix. InterruptionWindowController has no SetActive, so without this its
                // IsActive flag set by InterruptionSetEnablePopup_Prefix stays true after the
                // window closes, and every Cursor.*Index patch keeps returning early.
                var close = AccessTools.Method(controllerType, "Close");
                if (close != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(InterruptionClose_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(close, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] InterruptionWindowController.Close not found -- "
                        + "menu state will stay active after QuickSave");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch InterruptionWindowController: {ex.Message}");
            }
        }

        #endregion

        #region SaveListController Postfix Methods

        /// <summary>
        /// Postfix for KeyInput SaveListController.SelectContent - announces save slot info.
        /// </summary>
        public static void KeyInputSaveListSelectContent_Postfix(object __instance, GameCursor targetCursor)
        {
            try
            {
                if (targetCursor == null) return;

                var controller = __instance as KeyInputSaveListController;
                if (controller == null) return;

                // Check if the save/load window is actually visible
                var gameObject = controller.gameObject;
                if (gameObject == null || !gameObject.activeInHierarchy)
                    return;

                // Check if the cursor is visible
                if (targetCursor.gameObject == null || !targetCursor.gameObject.activeInHierarchy)
                    return;

                int index = targetCursor.Index;

                // Deduplicate announcements
                if (index == lastAnnouncedIndex)
                    return;
                lastAnnouncedIndex = index;

                // Mark that we're in the save/load menu
                SaveLoadMenuState.IsActive = true;

                // Start coroutine to read slot after UI updates
                CoroutineManager.StartManaged(ReadKeyInputSaveSlotDelayed(controller.Pointer, index));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in KeyInputSaveListSelectContent_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for Touch SaveListController.SelectContent - announces save slot info.
        /// </summary>
        public static void TouchSaveListSelectContent_Postfix(object __instance)
        {
            try
            {
                var controller = __instance as TouchSaveListController;
                if (controller == null) return;

                // Check if the save/load window is actually visible
                var gameObject = controller.gameObject;
                if (gameObject == null || !gameObject.activeInHierarchy)
                    return;

                // Get current cursor from selectCursor field (0x30)
                IntPtr controllerPtr = controller.Pointer;
                IntPtr cursorPtr = Marshal.ReadIntPtr(controllerPtr + 0x30);
                if (cursorPtr == IntPtr.Zero) return;

                var cursor = new GameCursor(cursorPtr);
                if (cursor.gameObject == null || !cursor.gameObject.activeInHierarchy)
                    return;

                int index = cursor.Index;

                // Deduplicate announcements
                if (index == lastAnnouncedIndex)
                    return;
                lastAnnouncedIndex = index;

                // Mark that we're in the save/load menu
                SaveLoadMenuState.IsActive = true;

                // Start coroutine to read slot after UI updates
                CoroutineManager.StartManaged(ReadTouchSaveSlotDelayed(controllerPtr, index));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in TouchSaveListSelectContent_Postfix: {ex.Message}");
            }
        }

        private static IEnumerator ReadKeyInputSaveSlotDelayed(IntPtr controllerPtr, int index)
        {
            yield return null; // Wait 1 frame for UI to update

            try
            {
                string slotInfo = ReadSaveSlotInfo(controllerPtr, index, isKeyInput: true, out int count);
                if (!string.IsNullOrEmpty(slotInfo))
                {
                    // Append slot position last (after all slot details).
                    slotInfo = MenuPosition.Format(slotInfo, index, count);
                    FFV_ScreenReaderMod.SpeakText(slotInfo, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading KeyInput save slot: {ex.Message}");
            }
        }

        private static IEnumerator ReadTouchSaveSlotDelayed(IntPtr controllerPtr, int index)
        {
            yield return null; // Wait 1 frame for UI to update

            try
            {
                string slotInfo = ReadSaveSlotInfo(controllerPtr, index, isKeyInput: false, out int count);
                if (!string.IsNullOrEmpty(slotInfo))
                {
                    // Append slot position last (after all slot details).
                    slotInfo = MenuPosition.Format(slotInfo, index, count);
                    FFV_ScreenReaderMod.SpeakText(slotInfo, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading Touch save slot: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the last announced slot so the next SelectContent announces even on the same row.
        /// </summary>
        internal static void ResetLastAnnouncedIndex() => lastAnnouncedIndex = -1;

        /// <summary>
        /// Reads and speaks one save slot immediately (no frame delay — callers that need one
        /// provide it). Returns true when it spoke. Used by SaveListPatches for the initial-focus
        /// read on menu open; priming lastAnnouncedIndex stops the SelectContent nav postfix from
        /// repeating the same slot right afterwards.
        /// </summary>
        internal static bool AnnounceSlotAtIndex(IntPtr controllerPtr, int index, bool isKeyInput)
        {
            string slotInfo = ReadSaveSlotInfo(controllerPtr, index, isKeyInput, out int count);
            if (string.IsNullOrEmpty(slotInfo)) return false;

            lastAnnouncedIndex = index;
            FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(slotInfo, index, count), interrupt: true);
            return true;
        }

        /// <summary>
        /// Reads save slot information from SaveListController.contentList[index].
        /// Format: "Quick Save, 01/26/2026 17:09, Tule - Armor Shop, Bartz Level 7, Time 03:03"
        /// </summary>
        private static string ReadSaveSlotInfo(IntPtr controllerPtr, int index, bool isKeyInput, out int count)
        {
            count = 0;
            try
            {
                int contentListOffset = isKeyInput ? KEYINPUT_LIST_CONTENT_LIST : TOUCH_LIST_CONTENT_LIST;
                int controllerViewOffset = isKeyInput ? KEYINPUT_CONTROLLER_VIEW_OFFSET : TOUCH_CONTROLLER_VIEW_OFFSET;

                // Read contentList from controller
                IntPtr contentListPtr = Marshal.ReadIntPtr(controllerPtr + contentListOffset);
                if (contentListPtr == IntPtr.Zero)
                    return null;

                // IL2CPP List: _size at 0x18, _items at 0x10
                int size = Marshal.ReadInt32(contentListPtr + 0x18);
                count = size;
                if (index < 0 || index >= size)
                    return null;

                IntPtr itemsPtr = Marshal.ReadIntPtr(contentListPtr + 0x10);
                if (itemsPtr == IntPtr.Zero) return null;

                // Array elements start at 0x20, 8 bytes per pointer
                IntPtr contentControllerPtr = Marshal.ReadIntPtr(itemsPtr + 0x20 + (index * 8));
                if (contentControllerPtr == IntPtr.Zero) return null;

                // Get SaveContentView from SaveContentController
                IntPtr viewPtr = Marshal.ReadIntPtr(contentControllerPtr + controllerViewOffset);
                if (viewPtr == IntPtr.Zero)
                    return null;

                bool isNumberedSlot = IsNumberedSlot(contentControllerPtr, isKeyInput);
                return ReadSaveContentView(viewPtr, isKeyInput, isNumberedSlot);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading slot info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads all fields from SaveContentView and formats the announcement.
        /// </summary>
        /// <summary>
        /// False for the autosave and quick-save rows, which take no slot number. Decided from the
        /// row's SaveSlotData id rather than its displayed name, which is localized. An unreadable
        /// id counts as numbered, so the number is still read.
        /// </summary>
        private static bool IsNumberedSlot(IntPtr contentControllerPtr, bool isKeyInput)
        {
            try
            {
                int slotDataOffset = isKeyInput ? KEYINPUT_CONTROLLER_SLOT_DATA_OFFSET : TOUCH_CONTROLLER_SLOT_DATA_OFFSET;
                IntPtr slotDataPtr = Marshal.ReadIntPtr(contentControllerPtr + slotDataOffset);
                if (slotDataPtr == IntPtr.Zero) return true;
                return Marshal.ReadInt32(slotDataPtr + SLOT_DATA_ID_OFFSET) <= MAX_NUMBERED_SLOT_ID;
            }
            catch
            {
                return true;
            }
        }

        private static string ReadSaveContentView(IntPtr viewPtr, bool isKeyInput, bool isNumberedSlot)
        {
            try
            {
                // Select offsets based on version
                int slotNameOffset = isKeyInput ? KEYINPUT_VIEW_SLOT_NAME_TEXT : TOUCH_VIEW_SLOT_NAME_TEXT;
                int slotNumOffset = isKeyInput ? KEYINPUT_VIEW_SLOT_NUM_TEXT : TOUCH_VIEW_SLOT_NUM_TEXT;
                int charaNameOffset = isKeyInput ? KEYINPUT_VIEW_CHARA_NAME_TEXT : TOUCH_VIEW_CHARA_NAME_TEXT;
                int levelOffset = isKeyInput ? KEYINPUT_VIEW_LEVEL_TEXT : TOUCH_VIEW_LEVEL_TEXT;
                int areaOffset = isKeyInput ? KEYINPUT_VIEW_AREA_NAME_TEXT : TOUCH_VIEW_AREA_NAME_TEXT;
                int floorOffset = isKeyInput ? KEYINPUT_VIEW_FLOOR_NAME_TEXT : TOUCH_VIEW_FLOOR_NAME_TEXT;
                int emptyOffset = isKeyInput ? KEYINPUT_VIEW_EMPTY_TEXT : TOUCH_VIEW_EMPTY_TEXT;
                int hourOffset = isKeyInput ? KEYINPUT_VIEW_HOUR_TEXT : TOUCH_VIEW_HOUR_TEXT;
                int minuteOffset = isKeyInput ? KEYINPUT_VIEW_MINUTE_TEXT : TOUCH_VIEW_MINUTE_TEXT;
                int dateOffset = isKeyInput ? KEYINPUT_VIEW_TIMESTAMP_DATE : TOUCH_VIEW_TIMESTAMP_DATE;
                int timeOffset = isKeyInput ? KEYINPUT_VIEW_TIMESTAMP_TIME : TOUCH_VIEW_TIMESTAMP_TIME;

                // Read slot name ("File", "Quick Save", "Autosave")
                string slotName = ReadTextAtOffset(viewPtr, slotNameOffset);
                string slotNum = ReadTextAtOffset(viewPtr, slotNumOffset);

                // Build slot identifier (e.g., "File 2" or "Quick Save")
                string slotId = slotName ?? "";
                if (!string.IsNullOrEmpty(slotNum) && isNumberedSlot)
                {
                    slotId = $"{slotName} {slotNum}".Trim();
                }

                // Check if empty - read emptyText and character name
                string emptyText = ReadTextAtOffset(viewPtr, emptyOffset);
                string charaName = ReadTextAtOffset(viewPtr, charaNameOffset);

                // If no character name, slot is empty
                if (string.IsNullOrEmpty(charaName) && !string.IsNullOrEmpty(emptyText))
                {
                    return $"{slotId}, {emptyText}";
                }

                // Read timestamp (date and time)
                string date = ReadTextAtOffset(viewPtr, dateOffset);
                string time = ReadTextAtOffset(viewPtr, timeOffset);

                // Read location
                string area = ReadTextAtOffset(viewPtr, areaOffset);
                string floor = ReadTextAtOffset(viewPtr, floorOffset);

                // Read level
                string level = ReadTextAtOffset(viewPtr, levelOffset);

                // Read play time
                string hours = ReadTextAtOffset(viewPtr, hourOffset);
                string minutes = ReadTextAtOffset(viewPtr, minuteOffset);

                // Build announcement matching visual display order:
                // "Quick Save, 01/26/2026 17:09, Tule - Armor Shop, Bartz Level 7, Time 03:03"
                var parts = new System.Collections.Generic.List<string>();

                // Slot identifier
                if (!string.IsNullOrEmpty(slotId))
                    parts.Add(slotId);

                // Timestamp (date time)
                if (!string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(time))
                    parts.Add($"{date} {time}");
                else if (!string.IsNullOrEmpty(date))
                    parts.Add(date);

                // Location (area + floor if present)
                if (!string.IsNullOrEmpty(area))
                {
                    if (!string.IsNullOrEmpty(floor))
                        parts.Add($"{area} {floor}");
                    else
                        parts.Add(area);
                }

                // Character name and level
                if (!string.IsNullOrEmpty(charaName))
                {
                    if (!string.IsNullOrEmpty(level))
                        parts.Add($"{charaName} {T("Level")} {level}");
                    else
                        parts.Add(charaName);
                }

                // Play time
                if (!string.IsNullOrEmpty(hours) && !string.IsNullOrEmpty(minutes))
                    parts.Add(string.Format(T("Time {0}"), $"{hours}:{minutes}"));

                return string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading SaveContentView: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper to read Text component at a given offset.
        /// </summary>
        internal static string ReadTextAtOffset(IntPtr basePtr, int offset)
        {
            try
            {
                IntPtr textPtr = Marshal.ReadIntPtr(basePtr + offset);
                if (textPtr == IntPtr.Zero) return null;

                var textComponent = new UnityEngine.UI.Text(textPtr);
                string text = textComponent.text;

                if (string.IsNullOrWhiteSpace(text)) return null;

                return StripRichTextTags(text.Trim());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Strips Unity rich text tags from a string.
        /// </summary>
        private static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return Regex.Replace(text, @"<[^>]+>", string.Empty);
        }

        #endregion

        #region Prefix Methods

        // These four run as PREFIXES so IsInConfirmation is set before the game method opens the
        // popup. That flag feeds SaveLoadMenuState.ShouldSuppress(), which MessagePatches checks
        // to keep dialogue text from talking over a confirmation dialog, and IsActive gates the
        // Cursor.*Index patches so the generic cursor reader doesn't double-read popup buttons
        // that TryReadSavePopupMove already handles.
        //
        // They do NOT suppress PopupOpen_Postfix, despite what earlier comments here claimed --
        // that postfix only checks IsShopActive(). It never fires for these popups anyway:
        // SavePopup derives from MonoBehaviour, not Il2CppLast.UI.Popup (dump.cs:469928).

        /// <summary>
        /// Prefix for LoadGameWindowController.SetPopupActive.
        /// </summary>
        public static void LoadGameWindowSetPopupActive_Prefix(bool isEnable)
        {
            if (isEnable)
            {
                SaveLoadMenuState.IsActive = true;
                SaveLoadMenuState.IsInConfirmation = true;
            }
        }

        /// <summary>
        /// Prefix for LoadWindowController.SetPopupActive.
        /// </summary>
        public static void LoadWindowSetPopupActive_Prefix(bool isEnable)
        {
            if (isEnable)
            {
                SaveLoadMenuState.IsActive = true;
                SaveLoadMenuState.IsInConfirmation = true;
            }
        }

        /// <summary>
        /// Prefix for SaveWindowController.SetPopupActive.
        /// </summary>
        public static void SaveWindowSetPopupActive_Prefix(bool isEnable)
        {
            if (isEnable)
            {
                SaveLoadMenuState.IsActive = true;
                SaveLoadMenuState.IsInConfirmation = true;
            }
        }

        /// <summary>
        /// Prefix for InterruptionWindowController.SetEnablePopup (QuickSave).
        /// IsActive set here is cleared by InterruptionClose_Postfix, not by a SetActive hook.
        /// </summary>
        public static void InterruptionSetEnablePopup_Prefix(bool isEnable)
        {
            if (isEnable)
            {
                SaveLoadMenuState.IsActive = true;
                SaveLoadMenuState.IsInConfirmation = true;
            }
        }

        #endregion

        #region Postfix Methods

        public static void LoadGameWindowSetPopupActive_Postfix(object __instance, bool isEnable)
            => OnPopupActive(__instance, isEnable, LOAD_GAME_WINDOW_SAVE_POPUP_OFFSET, "LoadGameWindowController");

        public static void LoadWindowSetPopupActive_Postfix(object __instance, bool isEnable)
            => OnPopupActive(__instance, isEnable, MENU_WINDOW_SAVE_POPUP_OFFSET, "LoadWindowController");

        public static void SaveWindowSetPopupActive_Postfix(object __instance, bool isEnable)
            => OnPopupActive(__instance, isEnable, MENU_WINDOW_SAVE_POPUP_OFFSET, "SaveWindowController");

        /// <summary>
        /// A controller's popup opened or closed (SetPopupActive / SetEnablePopup). On open, reads
        /// and registers the controller's SavePopup at <paramref name="savePopupOffset"/>.
        /// </summary>
        private static void OnPopupActive(object instance, bool isEnable, int savePopupOffset, string context)
        {
            try
            {
                if (isEnable)
                {
                    ReadSavePopupAt(ReadPointerField(instance, savePopupOffset));
                }
                else
                {
                    SaveLoadMenuState.IsInConfirmation = false;
                    ForgetSavePopup();
                }
            }
            catch (Exception ex)
            {
                savePopupOpenReadPending = false;
                MelonLogger.Warning($"[SaveLoad] Error in {context} popup postfix: {ex.Message}");
            }
        }

        /// <summary>The pointer stored at <paramref name="offset"/> in an IL2CPP object, or zero.</summary>
        private static IntPtr ReadPointerField(object instance, int offset)
        {
            var obj = instance as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase;
            if (obj == null || obj.Pointer == IntPtr.Zero) return IntPtr.Zero;
            return Marshal.ReadIntPtr(obj.Pointer + offset);
        }

        /// <summary>
        /// Registers an opened SavePopup (confirmation state, fresh button guard, open read pending)
        /// and reads its title, message and focused button two frames later, once the texts and
        /// the cursor are set. Its Yes/No moves are then read by TryReadSavePopupMove.
        /// </summary>
        private static void ReadSavePopupAt(IntPtr popupPtr)
        {
            if (popupPtr == IntPtr.Zero)
            {
                savePopupOpenReadPending = false;
                return;
            }

            SaveLoadMenuState.IsActive = true;
            SaveLoadMenuState.IsInConfirmation = true;
            lastPopupButtonIndex = -1;
            savePopupOpenReadPending = true;
            activeSavePopup = new SavePopup(popupPtr);

            CoroutineManager.StartManaged(DelayedSavePopupRead(popupPtr));
        }

        private static void ForgetSavePopup()
        {
            activeSavePopup = null;
            savePopupOpenReadPending = false;
            lastPopupButtonIndex = -1;
        }

        /// <summary>
        /// Yes/No moves of the open SavePopup, called from the Cursor.NextIndex/PrevIndex patches.
        /// SavePopup.UpdateSelect moves its selectCursor (0x58) only through Cursor.NextIndex /
        /// PrevIndex (&lt;UpdateSelect&gt;b__32_0, 0x7E1DB0), which set the index (Cursor 0x18)
        /// before invoking the move callback, so the index is current here. Returns true when the
        /// cursor is that popup's (the move is handled, spoken or not).
        /// </summary>
        public static bool TryReadSavePopupMove(GameCursor cursor)
        {
            try
            {
                var popup = activeSavePopup;
                if (popup == null || cursor == null) return false;
                IntPtr ptr = popup.Pointer;
                if (ptr == IntPtr.Zero || Marshal.ReadIntPtr(ptr + SAVE_POPUP_SELECT_CURSOR_OFFSET) != cursor.Pointer)
                    return false;

                if (savePopupOpenReadPending) return true;

                int index = cursor.Index;
                if (index == lastPopupButtonIndex) return true;
                lastPopupButtonIndex = index;

                string buttonText = ReadPopupButton(ptr, SAVE_POPUP_COMMAND_LIST_OFFSET_V2, index);
                if (!string.IsNullOrWhiteSpace(buttonText))
                    FFV_ScreenReaderMod.SpeakText(buttonText, interrupt: false);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading SavePopup move: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The focused button of a SavePopup-style popup (selectCursor, commandList). When the first
        /// command is hidden (a single-button popup such as the quick-save completion), the game's
        /// UpdateSelect forces the cursor to index 1 every frame, so that is the focused button
        /// whatever the index says yet.
        /// </summary>
        internal static int FocusedSaveStyleIndex(IntPtr popupPtr, int cursorOffset, int cmdListOffset)
        {
            int index = -1;
            try
            {
                IntPtr cursorPtr = Marshal.ReadIntPtr(popupPtr + cursorOffset);
                if (cursorPtr != IntPtr.Zero)
                    index = new GameCursor(cursorPtr).Index;

                IntPtr listPtr = Marshal.ReadIntPtr(popupPtr + cmdListOffset);
                if (listPtr == IntPtr.Zero || Marshal.ReadInt32(listPtr + 0x18) < 2) return index;
                IntPtr itemsPtr = Marshal.ReadIntPtr(listPtr + 0x10);
                IntPtr firstPtr = itemsPtr == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(itemsPtr + 0x20);
                if (firstPtr == IntPtr.Zero) return index;
                var first = new UnityEngine.Component(firstPtr).gameObject;
                if (first != null && !first.activeSelf) return 1;
            }
            catch { }
            return index;
        }

        /// <summary>
        /// Postfix for OverwriteConfirmInit (Save.KeyInput.SaveWindowController): the overwrite
        /// confirmation's open read. Every popup call in that body goes through view → savePopup.
        /// </summary>
        public static void OverwriteConfirmInit_Postfix(object __instance)
        {
            try
            {
                IntPtr viewPtr = ReadPointerField(__instance, OVERWRITE_CONTROLLER_VIEW_OFFSET);
                ReadSavePopupAt(viewPtr == IntPtr.Zero ? IntPtr.Zero
                    : Marshal.ReadIntPtr(viewPtr + OVERWRITE_VIEW_SAVE_POPUP_OFFSET));
            }
            catch (Exception ex)
            {
                savePopupOpenReadPending = false;
                MelonLogger.Warning($"[SaveLoad] Error in OverwriteConfirmInit_Postfix: {ex.Message}");
            }
        }

        public static void LoadGameWindowSetActive_Postfix(bool isActive)
        {
            try
            {
                if (!isActive)
                {
                    SaveLoadMenuState.ResetState();
                    lastAnnouncedIndex = -1;
                    ForgetSavePopup();
                }
                else
                {
                    SaveLoadMenuState.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in LoadGameWindowSetActive_Postfix: {ex.Message}");
            }
        }

        public static void LoadWindowSetActive_Postfix(bool isActive)
        {
            try
            {
                if (!isActive)
                {
                    SaveLoadMenuState.ResetState();
                    lastAnnouncedIndex = -1;
                    ForgetSavePopup();
                }
                else
                {
                    SaveLoadMenuState.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in LoadWindowSetActive_Postfix: {ex.Message}");
            }
        }

        public static void SaveWindowSetActive_Postfix(bool isActive)
        {
            try
            {
                if (!isActive)
                {
                    SaveLoadMenuState.ResetState();
                    lastAnnouncedIndex = -1;
                    ForgetSavePopup();
                }
                else
                {
                    SaveLoadMenuState.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in SaveWindowSetActive_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the save/load menu state.
        /// Called on scene change or when backing out of save/load menu.
        /// </summary>
        public static void ClearSaveLoadMenuState()
        {
            SaveLoadMenuState.ResetState();
            lastAnnouncedIndex = -1;
            ForgetSavePopup();
        }

        #endregion

        #region Popup Button Navigation Methods

        /// <summary>
        /// SavePopup field offsets for title/message (dump.cs line 469928)
        /// </summary>
        private const int SAVE_POPUP_TITLE_TEXT_OFFSET = 0x38;
        private const int SAVE_POPUP_MESSAGE_TEXT_OFFSET = 0x40;


        /// <summary>
        /// Open read of a SavePopup: title + message, then the focused button queued behind them.
        /// Two frames after the controller's open hook, so the texts and the cursor (ResetCursor,
        /// and UpdateSelect's single-button correction) are in place. Event-started, bounded.
        /// </summary>
        private static IEnumerator DelayedSavePopupRead(IntPtr popupPtr)
        {
            yield return null;
            yield return null;

            // From here on, Yes/No moves are read by TryReadSavePopupMove.
            savePopupOpenReadPending = false;

            try
            {
                if (popupPtr == IntPtr.Zero) yield break;
                if (activeSavePopup == null || activeSavePopup.Pointer != popupPtr) yield break; // closed meanwhile

                // Read title + message
                string title = ReadTextAtOffset(popupPtr, SAVE_POPUP_TITLE_TEXT_OFFSET);
                string message = ReadTextAtOffset(popupPtr, SAVE_POPUP_MESSAGE_TEXT_OFFSET);
                string announcement = PopupPatches.BuildAnnouncement(title, message);
                if (!string.IsNullOrEmpty(announcement))
                {
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
                }

                // Read the focused button (queues after title+message) and claim its index so the
                // first move away is the next thing read.
                int index = FocusedSaveStyleIndex(popupPtr, SAVE_POPUP_SELECT_CURSOR_OFFSET, SAVE_POPUP_COMMAND_LIST_OFFSET_V2);
                lastPopupButtonIndex = index;
                string buttonText = ReadPopupButton(popupPtr, SAVE_POPUP_COMMAND_LIST_OFFSET_V2, index);
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    FFV_ScreenReaderMod.SpeakText(buttonText, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in DelayedSavePopupRead: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for InterruptionWindowController.SetEnablePopup: the QuickSave confirmation
        /// opens (InitConfirmation) or closes.
        /// </summary>
        public static void InterruptionSetEnablePopup_Postfix(object __instance, bool isEnable)
            => OnPopupActive(__instance, isEnable, INTERRUPTION_SAVE_POPUP_OFFSET, "InterruptionWindowController");

        /// <summary>
        /// Postfix for InterruptionWindowController.InitComplite -- the Quick Save completion
        /// popup. QuickSave reuses ONE SavePopup across Confirmation -> Complite and
        /// SetEnablePopup does not fire again, so this is the completion's open hook: title +
        /// message, then its single button (the first command is hidden; see FocusedSaveStyleIndex).
        /// </summary>
        public static void InterruptionInitComplite_Postfix(object __instance)
        {
            try
            {
                ReadSavePopupAt(ReadPointerField(__instance, INTERRUPTION_SAVE_POPUP_OFFSET));
            }
            catch (Exception ex)
            {
                savePopupOpenReadPending = false;
                MelonLogger.Warning($"[SaveLoad] Error in InterruptionInitComplite_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for InterruptionWindowController.Close -- QuickSave has no SetActive hook,
        /// so this is where its menu state gets cleared.
        /// </summary>
        public static void InterruptionClose_Postfix()
        {
            SaveLoadMenuState.ResetState();
            ForgetSavePopup();
        }

        /// <summary>
        /// Reads button text from popup's commandList at the given index.
        /// </summary>
        private static string ReadPopupButton(IntPtr popupPtr, int cmdListOffset, int index)
        {
            try
            {
                IntPtr listPtr = Marshal.ReadIntPtr(popupPtr + cmdListOffset);
                if (listPtr == IntPtr.Zero) return null;

                int size = Marshal.ReadInt32(listPtr + 0x18);
                if (index < 0 || index >= size) return null;

                IntPtr itemsPtr = Marshal.ReadIntPtr(listPtr + 0x10);
                if (itemsPtr == IntPtr.Zero) return null;

                IntPtr commandPtr = Marshal.ReadIntPtr(itemsPtr + 0x20 + (index * 8));
                if (commandPtr == IntPtr.Zero) return null;

                IntPtr textPtr = Marshal.ReadIntPtr(commandPtr + COMMON_COMMAND_TEXT_OFFSET);
                if (textPtr == IntPtr.Zero) return null;

                var text = new UnityEngine.UI.Text(textPtr);
                return text?.text;
            }
            catch { return null; }
        }

        #endregion
    }
}
