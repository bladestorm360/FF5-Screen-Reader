using System;
using System.Collections;
using UnityEngine;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Simple Yes/No confirmation dialog using Windows API focus stealing.
    /// Used for waypoint deletion confirmations.
    /// </summary>
    public static class ConfirmationDialog
    {
        /// <summary>
        /// Whether the confirmation dialog is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static string prompt = "";
        private static Action onYesCallback;
        private static Action onNoCallback;
        private static bool selectedYes = true; // Default selection is Yes

        /// <summary>
        /// Opens the confirmation dialog.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="onYes">Callback when user confirms Yes</param>
        /// <param name="onNo">Callback when user confirms No</param>
        public static void Open(string promptText, Action onYes, Action onNo = null)
        {
            if (IsOpen) return;

            IsOpen = true;
            prompt = promptText ?? "";
            onYesCallback = onYes;
            onNoCallback = onNo;
            selectedYes = true; // Default to Yes

            // Initialize key states to prevent keys from triggering immediately
            WindowsFocusHelper.InitializeKeyStates(new[] {
                WindowsFocusHelper.VK_RETURN, WindowsFocusHelper.VK_ESCAPE, WindowsFocusHelper.VK_LEFT, WindowsFocusHelper.VK_RIGHT, WindowsFocusHelper.VK_Y, WindowsFocusHelper.VK_N
            });

            // Steal focus from game
            WindowsFocusHelper.StealFocus("FFV_ConfirmDialog");

            // Announce prompt with delay to avoid NVDA window title interruption
            CoroutineManager.StartManaged(DelayedPromptAnnouncement($"{prompt} Yes or No"));
        }

        /// <summary>
        /// Announces the prompt after a short delay to avoid NVDA announcing the window title first.
        /// </summary>
        private static IEnumerator DelayedPromptAnnouncement(string text)
        {
            yield return new WaitForSeconds(0.1f);
            FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
        }

        /// <summary>
        /// Closes the confirmation dialog and restores focus to game.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            WindowsFocusHelper.RestoreFocus();

            // Clear callbacks
            onYesCallback = null;
            onNoCallback = null;
        }

        /// <summary>
        /// Closes the dialog and announces text after focus is restored.
        /// Uses a coroutine delay to prevent NVDA window title from interrupting.
        /// </summary>
        public static void CloseWithAnnouncement(string text)
        {
            if (!IsOpen) return;

            IsOpen = false;
            WindowsFocusHelper.RestoreFocus();

            // Clear callbacks
            onYesCallback = null;
            onNoCallback = null;

            // Announce after focus restoration settles
            if (!string.IsNullOrEmpty(text))
            {
                CoroutineManager.StartManaged(DelayedPromptAnnouncement(text));
            }
        }

        /// <summary>
        /// Handles keyboard input for the confirmation dialog.
        /// Should be called from InputManager.Update() before any other input handling.
        /// Returns true if input was consumed (dialog is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Y key - confirm Yes immediately
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_Y))
            {
                FFV_ScreenReaderMod.SpeakText("Yes", interrupt: true);
                var callback = onYesCallback;
                Close();
                callback?.Invoke();
                return true;
            }

            // N key - confirm No immediately
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_N))
            {
                FFV_ScreenReaderMod.SpeakText("No", interrupt: true);
                var callback = onNoCallback;
                Close();
                callback?.Invoke();
                return true;
            }

            // Escape - same as No
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_ESCAPE))
            {
                FFV_ScreenReaderMod.SpeakText("Cancelled", interrupt: true);
                var callback = onNoCallback;
                Close();
                callback?.Invoke();
                return true;
            }

            // Enter - confirm current selection
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RETURN))
            {
                if (selectedYes)
                {
                    FFV_ScreenReaderMod.SpeakText("Yes", interrupt: true);
                    var callback = onYesCallback;
                    Close();
                    callback?.Invoke();
                }
                else
                {
                    FFV_ScreenReaderMod.SpeakText("No", interrupt: true);
                    var callback = onNoCallback;
                    Close();
                    callback?.Invoke();
                }
                return true;
            }

            // Left/Right arrows - toggle selection
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_LEFT) || WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RIGHT))
            {
                selectedYes = !selectedYes;
                string selection = selectedYes ? "Yes" : "No";
                FFV_ScreenReaderMod.SpeakText(selection, interrupt: true);
                return true;
            }

            return true; // Consume all input while dialog is open
        }
    }
}
