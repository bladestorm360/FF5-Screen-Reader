using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using UnityEngine.UI;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

// Type aliases for IL2CPP types - Base
using BasePopup = Il2CppLast.UI.Popup;
using GameCursor = Il2CppLast.UI.Cursor;

// Type aliases for IL2CPP types - KeyInput Popups
using KeyInputCommonPopup = Il2CppLast.UI.KeyInput.CommonPopup;
using KeyInputGameOverSelectPopup = Il2CppLast.UI.KeyInput.GameOverSelectPopup;
using KeyInputGameOverLoadPopup = Il2CppLast.UI.KeyInput.GameOverLoadPopup;
using KeyInputGameOverPopupController = Il2CppLast.UI.KeyInput.GameOverPopupController;
using KeyInputInfomationPopup = Il2CppLast.UI.KeyInput.InfomationPopup;
using KeyInputJobChangePopup = Il2CppLast.UI.KeyInput.JobChangePopup;
using KeyInputChangeNamePopup = Il2CppLast.UI.KeyInput.ChangeNamePopup;

// Type aliases for IL2CPP types - Touch Popups
using TouchCommonPopup = Il2CppLast.UI.Touch.CommonPopup;
using TouchGameOverSelectPopup = Il2CppLast.UI.Touch.GameOverSelectPopup;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks popup state for handling in CursorNavigation.
    /// Delegates IsConfirmationPopupActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class PopupState
    {
        public static bool IsConfirmationPopupActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.POPUP);
            private set => MenuStateRegistry.SetActive(MenuStateRegistry.POPUP, value);
        }

        public static string CurrentPopupType { get; private set; }
        public static IntPtr ActivePopupPtr { get; private set; }
        public static int CommandListOffset { get; private set; }

        public static void SetActive(string typeName, IntPtr ptr, int cmdListOffset)
        {
            IsConfirmationPopupActive = true;
            CurrentPopupType = typeName;
            ActivePopupPtr = ptr;
            CommandListOffset = cmdListOffset;
        }

        public static void Clear()
        {
            IsConfirmationPopupActive = false;
            CurrentPopupType = null;
            ActivePopupPtr = IntPtr.Zero;
            CommandListOffset = -1;
        }

        public static bool ShouldSuppress() => IsConfirmationPopupActive && CommandListOffset >= 0;
    }

    /// <summary>
    /// Patches for popup dialogs - handles ALL popup reading (message + buttons).
    /// Uses TryCast for IL2CPP-safe type detection.
    /// </summary>
    public static class PopupPatches
    {
        private static bool isPatched = false;

        // Memory offsets
        private const int ICON_TEXT_VIEW_NAME_TEXT_OFFSET = 0x20;
        private const int COMMON_COMMAND_TEXT_OFFSET = 0x18;
        private const int COMMON_TITLE_OFFSET = 0x38;
        private const int COMMON_MESSAGE_OFFSET = 0x40;
        private const int COMMON_SELECT_CURSOR_OFFSET = 0x68;
        private const int COMMON_CMDLIST_OFFSET = 0x70;
        private const int GAMEOVER_CMDLIST_OFFSET = 0x40;
        private const int INFO_TITLE_OFFSET = 0x28;
        private const int INFO_MESSAGE_OFFSET = 0x30;
        private const int JOB_CHANGE_NAME_OFFSET = 0x40;
        private const int JOB_CHANGE_CONFIRM_OFFSET = 0x48;
        private const int JOB_CHANGE_CMDLIST_OFFSET = 0x50;
        private const int CHANGE_NAME_DESCRIPTION_OFFSET = 0x30;
        private const int TOUCH_COMMON_TITLE_OFFSET = 0x28;
        private const int TOUCH_COMMON_MESSAGE_OFFSET = 0x38;

        // GameOverLoadPopup (KeyInput) - from dump.cs line 474386
        // titleText at 0x38, messageText at 0x40, selectCursor at 0x58, commandList at 0x60
        private const int GAMEOVERLOAD_TITLE_OFFSET = 0x38;
        private const int GAMEOVERLOAD_MESSAGE_OFFSET = 0x40;
        private const int GAMEOVERLOAD_SELECT_CURSOR_OFFSET = 0x58;
        private const int GAMEOVERLOAD_CMDLIST_OFFSET = 0x60;

        // GameOverPopupController -> View -> LoadPopup navigation
        private const int GAMEOVERPOPUPCTRL_VIEW_OFFSET = 0x30;
        private const int GAMEOVERPOPUPVIEW_LOADPOPUP_OFFSET = 0x18;

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            if (isPatched)
                return;

            try
            {
                TryPatchBasePopup(harmony);
                TryPatchGameOverLoadPopup(harmony);
                isPatched = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error applying patches: {ex.Message}");
            }
        }

        private static void TryPatchBasePopup(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type popupType = typeof(BasePopup);

                var openMethod = AccessTools.Method(popupType, "Open");
                if (openMethod != null)
                {
                    var openPostfix = typeof(PopupPatches).GetMethod(nameof(PopupOpen_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(openMethod, postfix: new HarmonyMethod(openPostfix));
                }

                var closeMethod = AccessTools.Method(popupType, "Close");
                if (closeMethod != null)
                {
                    var closePostfix = typeof(PopupPatches).GetMethod(nameof(PopupClose_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(closeMethod, postfix: new HarmonyMethod(closePostfix));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error patching base Popup: {ex.Message}");
            }
        }

        private static bool IsShopActive()
        {
            try
            {
                return ShopMenuTracker.IsShopMenuActive;
            }
            catch
            {
                return false;
            }
        }

        #region Text Reading Helpers

        private static string ReadTextFromPointer(IntPtr textPtr)
        {
            if (textPtr == IntPtr.Zero) return null;
            try
            {
                var text = new Text(textPtr);
                return text?.text;
            }
            catch (Exception) { return null; }
        }

        private static string ReadIconTextViewText(IntPtr iconTextViewPtr)
        {
            if (iconTextViewPtr == IntPtr.Zero) return null;
            try
            {
                IntPtr nameTextPtr = Marshal.ReadIntPtr(iconTextViewPtr + ICON_TEXT_VIEW_NAME_TEXT_OFFSET);
                return ReadTextFromPointer(nameTextPtr);
            }
            catch (Exception) { return null; }
        }

        internal static string BuildAnnouncement(string title, string message)
        {
            title = string.IsNullOrWhiteSpace(title) ? null : TextUtils.StripRichTextTags(title.Trim());
            message = string.IsNullOrWhiteSpace(message) ? null : TextUtils.StripRichTextTags(message.Trim());

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(message))
                return $"{title}. {message}";
            else if (!string.IsNullOrEmpty(title))
                return title;
            else if (!string.IsNullOrEmpty(message))
                return message;
            return null;
        }

        #endregion

        #region Type-Specific Readers

        private static string ReadCommonPopup(IntPtr ptr)
        {
            IntPtr titleViewPtr = Marshal.ReadIntPtr(ptr + COMMON_TITLE_OFFSET);
            string title = ReadIconTextViewText(titleViewPtr);
            IntPtr messagePtr = Marshal.ReadIntPtr(ptr + COMMON_MESSAGE_OFFSET);
            string message = ReadTextFromPointer(messagePtr);
            string announcement = BuildAnnouncement(title, message);

            // Append initially focused button text
            try
            {
                IntPtr cursorPtr = Marshal.ReadIntPtr(ptr + COMMON_SELECT_CURSOR_OFFSET);
                if (cursorPtr != IntPtr.Zero)
                {
                    var cursor = new GameCursor(cursorPtr);
                    string buttonText = ReadButtonFromCommandList(ptr, COMMON_CMDLIST_OFFSET, cursor.Index);
                    if (!string.IsNullOrWhiteSpace(buttonText))
                    {
                        buttonText = TextUtils.StripRichTextTags(buttonText);
                        announcement = $"{announcement} {buttonText}";
                    }
                }
            }
            catch { }

            return announcement;
        }

        private static string ReadGameOverSelectPopup(IntPtr ptr)
        {
            return "Game Over";
        }

        private static string ReadInfomationPopup(IntPtr ptr)
        {
            IntPtr titleViewPtr = Marshal.ReadIntPtr(ptr + INFO_TITLE_OFFSET);
            string title = ReadIconTextViewText(titleViewPtr);
            IntPtr messagePtr = Marshal.ReadIntPtr(ptr + INFO_MESSAGE_OFFSET);
            string message = ReadTextFromPointer(messagePtr);
            return BuildAnnouncement(title, message);
        }

        private static string ReadJobChangePopup(IntPtr ptr)
        {
            IntPtr jobNamePtr = Marshal.ReadIntPtr(ptr + JOB_CHANGE_NAME_OFFSET);
            string jobName = ReadTextFromPointer(jobNamePtr);
            IntPtr confirmPtr = Marshal.ReadIntPtr(ptr + JOB_CHANGE_CONFIRM_OFFSET);
            string confirmText = ReadTextFromPointer(confirmPtr);
            return BuildAnnouncement(jobName, confirmText);
        }

        private static string ReadChangeNamePopup(IntPtr ptr)
        {
            IntPtr descPtr = Marshal.ReadIntPtr(ptr + CHANGE_NAME_DESCRIPTION_OFFSET);
            string description = ReadTextFromPointer(descPtr);
            string hint = LocalizationHelper.GetModString("default_name_hint");
            if (!string.IsNullOrEmpty(description))
                return $"{description}. {hint}";
            return hint;
        }

        private static string ReadTouchCommonPopup(IntPtr ptr)
        {
            IntPtr titlePtr = Marshal.ReadIntPtr(ptr + TOUCH_COMMON_TITLE_OFFSET);
            string title = ReadTextFromPointer(titlePtr);
            IntPtr msgPtr = Marshal.ReadIntPtr(ptr + TOUCH_COMMON_MESSAGE_OFFSET);
            string msg = ReadTextFromPointer(msgPtr);
            return BuildAnnouncement(title, msg);
        }

        #endregion

        #region Button Reading

        public static void ReadCurrentButton(GameCursor cursor)
        {
            try
            {
                if (PopupState.ActivePopupPtr == IntPtr.Zero || PopupState.CommandListOffset < 0)
                    return;

                string buttonText = ReadButtonFromCommandList(
                    PopupState.ActivePopupPtr,
                    PopupState.CommandListOffset,
                    cursor.Index);

                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    buttonText = TextUtils.StripRichTextTags(buttonText);
                    FFV_ScreenReaderMod.SpeakText(buttonText, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error reading button: {ex.Message}");
            }
        }

        private static string ReadButtonFromCommandList(IntPtr popupPtr, int cmdListOffset, int index)
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
                return ReadTextFromPointer(textPtr);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error reading command list: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Popup Open/Close Postfixes

        public static void PopupOpen_Postfix(BasePopup __instance)
        {
            try
            {
                if (__instance == null || IsShopActive())
                    return;

                // KeyInput types first
                var commonPopup = __instance.TryCast<KeyInputCommonPopup>();
                if (commonPopup != null)
                {
                    HandlePopupDetected("CommonPopup", commonPopup.Pointer, COMMON_CMDLIST_OFFSET,
                        () => ReadCommonPopup(commonPopup.Pointer));
                    return;
                }

                var gameOver = __instance.TryCast<KeyInputGameOverSelectPopup>();
                if (gameOver != null)
                {
                    HandlePopupDetected("GameOverSelectPopup", gameOver.Pointer, GAMEOVER_CMDLIST_OFFSET,
                        () => ReadGameOverSelectPopup(gameOver.Pointer));
                    return;
                }

                var info = __instance.TryCast<KeyInputInfomationPopup>();
                if (info != null)
                {
                    HandlePopupDetected("InfomationPopup", info.Pointer, -1,
                        () => ReadInfomationPopup(info.Pointer));
                    return;
                }

                var jobChange = __instance.TryCast<KeyInputJobChangePopup>();
                if (jobChange != null)
                {
                    HandlePopupDetected("JobChangePopup", jobChange.Pointer, JOB_CHANGE_CMDLIST_OFFSET,
                        () => ReadJobChangePopup(jobChange.Pointer));
                    return;
                }

                var changeName = __instance.TryCast<KeyInputChangeNamePopup>();
                if (changeName != null)
                {
                    HandlePopupDetected("ChangeNamePopup", changeName.Pointer, -1,
                        () => ReadChangeNamePopup(changeName.Pointer));
                    return;
                }

                // Touch types (fallback)
                var touchCommon = __instance.TryCast<TouchCommonPopup>();
                if (touchCommon != null)
                {
                    HandlePopupDetected("TouchCommonPopup", touchCommon.Pointer, -1,
                        () => ReadTouchCommonPopup(touchCommon.Pointer));
                    return;
                }

                var touchGameOver = __instance.TryCast<TouchGameOverSelectPopup>();
                if (touchGameOver != null)
                {
                    HandlePopupDetected("TouchGameOverSelectPopup", touchGameOver.Pointer, -1,
                        () => "Game Over");
                    return;
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in Open postfix: {ex.Message}");
            }
        }

        private static void HandlePopupDetected(string typeName, IntPtr ptr, int cmdListOffset, Func<string> readFunc)
        {
            PopupState.SetActive(typeName, ptr, cmdListOffset);
            CoroutineManager.StartManaged(DelayedPopupRead(ptr, typeName, readFunc));
        }

        private static IEnumerator DelayedPopupRead(IntPtr popupPtr, string typeName, Func<string> readFunc)
        {
            yield return null;

            try
            {
                if (popupPtr == IntPtr.Zero) yield break;

                string announcement = readFunc();
                if (!string.IsNullOrEmpty(announcement))
                {
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in delayed read: {ex.Message}");
            }
        }

        public static void PopupClose_Postfix()
        {
            try
            {
                if (PopupState.IsConfirmationPopupActive)
                {
                    PopupState.Clear();
                    // Reset config dedup so option can be re-announced after popup dismissal
                    AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_COMMAND);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in Close postfix: {ex.Message}");
            }
        }

        #endregion

        #region GameOver Load Popup Patches

        /// <summary>
        /// Patch GameOverLoadPopup.UpdateCommand for Yes/No button navigation
        /// and GameOverPopupController.InitSaveLoadPopup to announce the popup message.
        /// </summary>
        private static void TryPatchGameOverLoadPopup(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch UpdateCommand for button navigation
                Type loadPopupType = typeof(KeyInputGameOverLoadPopup);
                var updateCommandMethod = AccessTools.Method(loadPopupType, "UpdateCommand");

                if (updateCommandMethod != null)
                {
                    var postfix = typeof(PopupPatches).GetMethod(nameof(GameOverLoadPopup_UpdateCommand_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(updateCommandMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[Popup] GameOverLoadPopup.UpdateCommand method not found");
                }

                // Patch InitSaveLoadPopup to announce popup message when it opens
                Type controllerType = typeof(KeyInputGameOverPopupController);
                var initMethod = AccessTools.Method(controllerType, "InitSaveLoadPopup");

                if (initMethod != null)
                {
                    var postfix = typeof(PopupPatches).GetMethod(nameof(GameOverPopupController_InitSaveLoadPopup_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(initMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[Popup] GameOverPopupController.InitSaveLoadPopup method not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error patching GameOverLoadPopup: {ex.Message}");
            }
        }

        private static int lastGameOverLoadIndex = -1;

        public static void GameOverLoadPopup_UpdateCommand_Postfix(KeyInputGameOverLoadPopup __instance)
        {
            try
            {
                if (__instance == null) return;

                IntPtr ptr = __instance.Pointer;
                if (ptr == IntPtr.Zero) return;

                // Read cursor index from offset 0x58
                IntPtr cursorPtr = Marshal.ReadIntPtr(ptr + GAMEOVERLOAD_SELECT_CURSOR_OFFSET);
                if (cursorPtr == IntPtr.Zero) return;

                var cursor = new GameCursor(cursorPtr);
                int index = cursor.Index;

                // Deduplicate by index
                if (index == lastGameOverLoadIndex) return;
                lastGameOverLoadIndex = index;

                // Read button text from commandList at offset 0x60
                string buttonText = ReadButtonFromCommandList(ptr, GAMEOVERLOAD_CMDLIST_OFFSET, index);
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    buttonText = TextUtils.StripIconMarkup(buttonText);
                    FFV_ScreenReaderMod.SpeakText(buttonText, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in GameOverLoadPopup.UpdateCommand: {ex.Message}");
            }
        }

        public static void GameOverPopupController_InitSaveLoadPopup_Postfix(KeyInputGameOverPopupController __instance)
        {
            try
            {
                if (__instance == null) return;

                // Reset button tracking for fresh state
                lastGameOverLoadIndex = -1;
                SaveLoadMenuState.IsActive = true; // Suppress PopupOpen_Postfix for this flow

                // Use coroutine to delay reading until UI has populated
                CoroutineManager.StartManaged(DelayedGameOverLoadPopupRead(__instance.Pointer));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in GameOverPopupController.InitSaveLoadPopup: {ex.Message}");
            }
        }

        private static IEnumerator DelayedGameOverLoadPopupRead(IntPtr controllerPtr)
        {
            yield return null; // Wait one frame

            try
            {
                if (controllerPtr == IntPtr.Zero) yield break;

                // Navigate: controller->view(0x30)->loadPopup(0x18)->messageText(0x40)
                IntPtr viewPtr = Marshal.ReadIntPtr(controllerPtr + GAMEOVERPOPUPCTRL_VIEW_OFFSET);
                if (viewPtr == IntPtr.Zero)
                {
                    yield break;
                }

                IntPtr loadPopupPtr = Marshal.ReadIntPtr(viewPtr + GAMEOVERPOPUPVIEW_LOADPOPUP_OFFSET);
                if (loadPopupPtr == IntPtr.Zero)
                {
                    yield break;
                }

                IntPtr messageTextPtr = Marshal.ReadIntPtr(loadPopupPtr + GAMEOVERLOAD_MESSAGE_OFFSET);
                string message = ReadTextFromPointer(messageTextPtr);

                if (!string.IsNullOrWhiteSpace(message))
                {
                    message = TextUtils.StripIconMarkup(message.Trim());
                    FFV_ScreenReaderMod.SpeakText(message, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Popup] Error in DelayedGameOverLoadPopupRead: {ex.Message}");
            }
        }

        #endregion
    }
}
