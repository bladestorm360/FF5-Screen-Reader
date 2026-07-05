using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Simple Yes/No confirmation dialog (virtual — no window focus stealing).
    /// Game input is suppressed via ControllerRouter.SuppressGameInput + InputPassthroughPatches
    /// while IsOpen. Keys are read through GamepadManager (SDL3 + GetAsyncKeyState).
    ///
    /// Supports chained prompts: a Yes/No callback may itself open a new confirmation. When it
    /// does, this dialog stays open and the new prompt is re-announced immediately, so the player
    /// flows from one question to the next without a close/reopen gap.
    /// </summary>
    public static class ConfirmationDialog
    {
        public static bool IsOpen { get; private set; }

        private static string prompt = "";
        private static Action onYesCallback;
        private static Action onNoCallback;
        private static bool selectedYes = true; // Default selection is Yes

        /// <summary>
        /// Opens the confirmation dialog. If a dialog is already open (a callback chained into a
        /// new prompt), the new prompt is announced immediately instead of via the delayed coroutine.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="onYes">Callback when user confirms Yes</param>
        /// <param name="onNo">Callback when user confirms No</param>
        public static void Open(string promptText, Action onYes, Action onNo = null)
        {
            bool wasAlreadyOpen = IsOpen;

            IsOpen = true;
            prompt = promptText ?? "";
            onYesCallback = onYes;
            onNoCallback = onNo;
            selectedYes = true; // Default to Yes

            if (!wasAlreadyOpen)
            {
                // First open — announce prompt with a short delay so it settles cleanly.
                CoroutineManager.StartManaged(DelayedPromptAnnouncement($"{prompt} {T("Yes or No")}"));
            }
            else
            {
                // Continuation — dialog already open, just announce the new prompt immediately.
                FFV_ScreenReaderMod.SpeakText($"{prompt} {T("Yes or No")}", interrupt: true);
            }
        }

        private static IEnumerator DelayedPromptAnnouncement(string text)
        {
            yield return new WaitForSeconds(0.1f);
            FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
        }

        /// <summary>
        /// Announces the chosen option after a short delay, then invokes the callback. If the
        /// callback opened a new prompt (chained confirmation), this dialog stays open (the new
        /// prompt re-announced itself); otherwise the dialog closes.
        /// </summary>
        private static IEnumerator DelayedCloseAnnouncement(string text, Action callback)
        {
            // Clear callbacks up front so we can detect whether the invoked callback opens a new
            // prompt (which repopulates onYesCallback).
            onYesCallback = null;
            onNoCallback = null;

            yield return new WaitForSeconds(0.1f);
            FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
            callback?.Invoke();

            if (onYesCallback == null)
                Close();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            onYesCallback = null;
            onNoCallback = null;
        }

        /// <summary>
        /// Handles keyboard input for the confirmation dialog. Reads keys via GamepadManager;
        /// game input is suppressed while IsOpen. Returns true if input was consumed (dialog open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Y key - confirm Yes immediately
            if (GamepadManager.IsKeyCodePressed(KeyCode.Y))
            {
                var callback = onYesCallback;
                CoroutineManager.StartManaged(DelayedCloseAnnouncement(T("Yes"), callback));
                return true;
            }

            // N key - confirm No immediately
            if (GamepadManager.IsKeyCodePressed(KeyCode.N))
            {
                var callback = onNoCallback;
                CoroutineManager.StartManaged(DelayedCloseAnnouncement(T("No"), callback));
                return true;
            }

            // Escape - same as No
            if (GamepadManager.IsKeyCodePressed(KeyCode.Escape))
            {
                var callback = onNoCallback;
                CoroutineManager.StartManaged(DelayedCloseAnnouncement(T("Cancelled"), callback));
                return true;
            }

            // Enter - confirm current selection
            if (GamepadManager.IsKeyCodePressed(KeyCode.Return))
            {
                if (selectedYes)
                {
                    var callback = onYesCallback;
                    CoroutineManager.StartManaged(DelayedCloseAnnouncement(T("Yes"), callback));
                }
                else
                {
                    var callback = onNoCallback;
                    CoroutineManager.StartManaged(DelayedCloseAnnouncement(T("No"), callback));
                }
                return true;
            }

            // Left/Right arrows - toggle selection
            if (GamepadManager.IsKeyCodePressed(KeyCode.LeftArrow) || GamepadManager.IsKeyCodePressed(KeyCode.RightArrow))
            {
                selectedYes = !selectedYes;
                string selection = selectedYes ? T("Yes") : T("No");
                FFV_ScreenReaderMod.SpeakText(selection, interrupt: true);
                return true;
            }

            return true; // Consume all input while dialog is open
        }
    }
}
