using System;
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
        /// Opens the confirmation dialog and announces the prompt. If a dialog is already open (a
        /// callback chained into a new prompt), it stays open with the new prompt.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="onYes">Callback when user confirms Yes</param>
        /// <param name="onNo">Callback when user confirms No</param>
        public static void Open(string promptText, Action onYes, Action onNo = null)
        {
            IsOpen = true;
            prompt = promptText ?? "";
            onYesCallback = onYes;
            onNoCallback = onNo;
            selectedYes = true; // Default to Yes

            // Spoken at once, first open or a chained prompt alike. The old 0.1 s delay dated from
            // the real-window version, which had to wait for NVDA's focus announcement; the
            // dialog is virtual now, so there is no focus change to wait for (Rule 3: no timers).
            FFV_ScreenReaderMod.SpeakText($"{prompt} {T("Yes or No")}", interrupt: true);
        }

        /// <summary>
        /// Announces the chosen option, then invokes the callback. If the callback opened a new
        /// prompt (chained confirmation), this dialog stays open (the new prompt announced itself);
        /// otherwise the dialog closes. The callback's own result is queued behind the echo
        /// (FFV_ScreenReaderMod.SpeakTextQueued). A null echo speaks nothing, for when the callback's
        /// result already says it ("Cancelled" twice otherwise).
        /// </summary>
        private static void CloseWith(string text, Action callback)
        {
            // Clear callbacks up front so we can detect whether the invoked callback opens a new
            // prompt (which repopulates onYesCallback).
            onYesCallback = null;
            onNoCallback = null;

            if (!string.IsNullOrEmpty(text))
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
                CloseWith(T("Yes"), onYesCallback);
                return true;
            }

            // N key - confirm No immediately
            if (GamepadManager.IsKeyCodePressed(KeyCode.N))
            {
                CloseWith(T("No"), onNoCallback);
                return true;
            }

            // Escape - same as No. Every caller's No callback says "Cancelled" itself, so the
            // echo is only spoken when there is no callback to say it.
            if (GamepadManager.IsKeyCodePressed(KeyCode.Escape))
            {
                var callback = onNoCallback;
                CloseWith(callback == null ? T("Cancelled") : null, callback);
                return true;
            }

            // Enter - confirm current selection
            if (GamepadManager.IsKeyCodePressed(KeyCode.Return))
            {
                if (selectedYes)
                    CloseWith(T("Yes"), onYesCallback);
                else
                    CloseWith(T("No"), onNoCallback);
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
