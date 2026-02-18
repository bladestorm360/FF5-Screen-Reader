using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

// FF5 Save/Load UI types
// Touch version (title screen): SaveListController has contentList at 0x40, SelectContent(SaveSlotData data)
// KeyInput version (main menu): SaveListController has contentList at 0x68, SelectContent(Cursor targetCursor, ...)
using TouchSaveListController = Il2CppLast.UI.Touch.SaveListController;
using KeyInputSaveListController = Il2CppLast.UI.KeyInput.SaveListController;
using KeyInputLoadWindowController = Il2CppLast.UI.KeyInput.LoadWindowController;
using KeyInputSaveWindowController = Il2CppLast.UI.KeyInput.SaveWindowController;
using KeyInputLoadGameWindowController = Il2CppLast.UI.KeyInput.LoadGameWindowController;
using GameCursor = Il2CppLast.UI.Cursor;
using SavePopup = Il2CppLast.UI.KeyInput.SavePopup;
using InterruptionController = Il2CppLast.UI.KeyInput.InterruptionWindowController;

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

        // SaveListController field offsets
        // Touch: contentList at 0x40 (dump.cs line 434801)
        // KeyInput: contentList at 0x68 (dump.cs line 469565)
        private const int TOUCH_LIST_CONTENT_LIST = 0x40;
        private const int KEYINPUT_LIST_CONTENT_LIST = 0x68;

        // SavePopup offsets for confirmation dialogs (from PopupPatches)
        // LoadWindowController/SaveWindowController: savePopup at 0x28
        // LoadGameWindowController: savePopup at 0x58
        private const int MAIN_MENU_SAVE_POPUP_OFFSET = 0x28;


        private const int SAVE_POPUP_COMMAND_LIST_OFFSET = 0x70;

        // SavePopup button navigation offsets (from dump.cs)
        // selectCursor: 0x58 (Cursor), commandList: 0x60 (List<CommonCommand>)
        private const int SAVE_POPUP_SELECT_CURSOR_OFFSET = 0x58;
        private const int SAVE_POPUP_COMMAND_LIST_OFFSET_V2 = 0x60;
        private const int COMMON_COMMAND_TEXT_OFFSET = 0x18;


        private static int lastAnnouncedIndex = -1;
        private static int lastPopupButtonIndex = -1;

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

                // Patch SavePopup.UpdateCommand for button navigation (covers ALL save/load popups)
                TryPatchSavePopupUpdateCommand(harmony);

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
        /// Patches SavePopup.UpdateCommand for button navigation.
        /// This single patch handles ALL popup button navigation since all controllers use the same SavePopup class.
        /// </summary>
        private static void TryPatchSavePopupUpdateCommand(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type popupType = typeof(SavePopup);
                var method = AccessTools.Method(popupType, "UpdateCommand");

                if (method != null)
                {
                    var postfix = typeof(SaveLoadPatches).GetMethod(nameof(SavePopupUpdateCommand_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[SaveLoad] SavePopup.UpdateCommand not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Failed to patch SavePopup.UpdateCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Patches InterruptionWindowController.SetEnablePopup for QuickSave popup message reading.
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
                string slotInfo = ReadSaveSlotInfo(controllerPtr, index, isKeyInput: true);
                if (!string.IsNullOrEmpty(slotInfo))
                {
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
                string slotInfo = ReadSaveSlotInfo(controllerPtr, index, isKeyInput: false);
                if (!string.IsNullOrEmpty(slotInfo))
                {
                    FFV_ScreenReaderMod.SpeakText(slotInfo, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error reading Touch save slot: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads save slot information from SaveListController.contentList[index].
        /// Format: "Quick Save, 01/26/2026 17:09, Tule - Armor Shop, Bartz Level 7, Time 03:03"
        /// </summary>
        private static string ReadSaveSlotInfo(IntPtr controllerPtr, int index, bool isKeyInput)
        {
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

                return ReadSaveContentView(viewPtr, isKeyInput);
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
        private static string ReadSaveContentView(IntPtr viewPtr, bool isKeyInput)
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
                if (!string.IsNullOrEmpty(slotNum) &&
                    slotName != "Autosave" && slotName != "Quick Save" &&
                    !slotName?.Contains("Auto") == true && !slotName?.Contains("Quick") == true)
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
                        parts.Add($"{charaName} Level {level}");
                    else
                        parts.Add(charaName);
                }

                // Play time
                if (!string.IsNullOrEmpty(hours) && !string.IsNullOrEmpty(minutes))
                    parts.Add($"Time {hours}:{minutes}");

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

        /// <summary>
        /// Prefix for LoadGameWindowController.SetPopupActive -- sets flags BEFORE the game method
        /// calls Popup.Open() internally, so PopupOpen_Postfix sees IsActive=true and returns early.
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
        /// Prefix for LoadWindowController.SetPopupActive -- sets flags BEFORE Popup.Open().
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
        /// Prefix for SaveWindowController.SetPopupActive -- sets flags BEFORE Popup.Open().
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
        /// Prefix for InterruptionWindowController.SetEnablePopup -- sets flags BEFORE Popup.Open().
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
        {
            try
            {
                if (isEnable)
                {
                    if (__instance != null)
                    {
                        // Reset button index for fresh popup
                        lastPopupButtonIndex = -1;
                    }
                }
                else
                {
                    SaveLoadMenuState.IsInConfirmation = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in LoadGameWindowSetPopupActive_Postfix: {ex.Message}");
            }
        }

        public static void LoadWindowSetPopupActive_Postfix(object __instance, bool isEnable)
        {
            try
            {
                if (isEnable)
                {
                    // Reset button index for fresh popup
                    lastPopupButtonIndex = -1;
                }
                else
                {
                    SaveLoadMenuState.IsInConfirmation = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in LoadWindowSetPopupActive_Postfix: {ex.Message}");
            }
        }

        public static void SaveWindowSetPopupActive_Postfix(object __instance, bool isEnable)
        {
            try
            {
                if (isEnable)
                {
                    // Reset button index for fresh popup
                    lastPopupButtonIndex = -1;
                }
                else
                {
                    SaveLoadMenuState.IsInConfirmation = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in SaveWindowSetPopupActive_Postfix: {ex.Message}");
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
        }

        #endregion

        #region Popup Button Navigation Methods

        /// <summary>
        /// SavePopup field offsets for title/message (dump.cs line 469928)
        /// </summary>
        private const int SAVE_POPUP_TITLE_TEXT_OFFSET = 0x38;
        private const int SAVE_POPUP_MESSAGE_TEXT_OFFSET = 0x40;

        public static void SavePopupUpdateCommand_Postfix(SavePopup __instance)
        {
            try
            {
                if (__instance == null) return;

                IntPtr ptr = __instance.Pointer;
                if (ptr == IntPtr.Zero) return;

                // Read cursor from offset 0x58
                IntPtr cursorPtr = Marshal.ReadIntPtr(ptr + SAVE_POPUP_SELECT_CURSOR_OFFSET);
                if (cursorPtr == IntPtr.Zero) return;

                var cursor = new GameCursor(cursorPtr);
                int index = cursor.Index;

                // Deduplicate
                if (index == lastPopupButtonIndex) return;

                bool isFirstCall = (lastPopupButtonIndex == -1);
                lastPopupButtonIndex = index;

                // On first call after popup opens, delay read by 1 frame -- title text may not be populated yet
                if (isFirstCall)
                {
                    CoroutineManager.StartManaged(DelayedSavePopupRead(ptr, index));
                    return; // Button will be read inside the coroutine after title+message
                }

                // Subsequent calls: read button text normally
                string buttonText = ReadPopupButton(ptr, SAVE_POPUP_COMMAND_LIST_OFFSET_V2, index);
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    FFV_ScreenReaderMod.SpeakText(buttonText, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in SavePopupUpdateCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Delayed read of save popup title + message + initial button, waiting 1 frame for UI to populate.
        /// </summary>
        private static IEnumerator DelayedSavePopupRead(IntPtr popupPtr, int buttonIndex)
        {
            yield return null; // Wait 1 frame for UI to populate

            try
            {
                if (popupPtr == IntPtr.Zero) yield break;

                // Read title + message
                string title = ReadTextAtOffset(popupPtr, SAVE_POPUP_TITLE_TEXT_OFFSET);
                string message = ReadTextAtOffset(popupPtr, SAVE_POPUP_MESSAGE_TEXT_OFFSET);
                string announcement = PopupPatches.BuildAnnouncement(title, message);
                if (!string.IsNullOrEmpty(announcement))
                {
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
                }

                // Read initial button text (queues after title+message)
                string buttonText = ReadPopupButton(popupPtr, SAVE_POPUP_COMMAND_LIST_OFFSET_V2, buttonIndex);
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
        /// Postfix for InterruptionWindowController.SetEnablePopup - reads QuickSave popup message.
        /// </summary>
        public static void InterruptionSetEnablePopup_Postfix(object __instance, bool isEnable)
        {
            try
            {
                if (isEnable)
                {
                    if (__instance != null)
                    {
                        // Reset button index for fresh popup
                        lastPopupButtonIndex = -1;
                    }
                }
                else
                {
                    SaveLoadMenuState.IsInConfirmation = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveLoad] Error in InterruptionSetEnablePopup: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for InterruptionWindowController.InitComplite - reads Quick Save completion popup.
        /// </summary>
        public static void InterruptionInitComplite_Postfix()
        {
            // Reset lastPopupButtonIndex so SavePopup.UpdateCommand re-triggers first-call flow
            lastPopupButtonIndex = -1;
        }

        /// <summary>
        /// Postfix for SaveWindowController.CompleteInit - reads Normal Save completion popup.
        /// </summary>
        public static void SaveWindowCompleteInit_Postfix()
        {
            // Reset lastPopupButtonIndex so SavePopup.UpdateCommand re-triggers first-call flow
            lastPopupButtonIndex = -1;
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
