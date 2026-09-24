using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Modal text input dialog (virtual — no window focus stealing).
    /// Game input is suppressed via ControllerRouter.SuppressGameInput + InputPassthroughPatches
    /// while IsOpen. Keys are read through GamepadManager (SDL3 + GetAsyncKeyState).
    /// Used for waypoint naming and other text input scenarios.
    /// </summary>
    public static class TextInputWindow
    {
        public static bool IsOpen { get; private set; }

        private static StringBuilder inputBuffer = new StringBuilder();
        private static string prompt = "";
        private static Action<string> onConfirmCallback;
        private static Action onCancelCallback;
        private static int cursorPosition = 0;

        public static void Open(string promptText, string initialText, Action<string> onConfirm, Action onCancel = null)
        {
            if (IsOpen) return;

            IsOpen = true;
            prompt = promptText ?? "";
            inputBuffer.Clear();
            if (!string.IsNullOrEmpty(initialText))
                inputBuffer.Append(initialText);

            onConfirmCallback = onConfirm;
            onCancelCallback = onCancel;
            cursorPosition = inputBuffer.Length;

            // Spoken at once. The old 0.3 s delay dated from the real-window version, which had to
            // wait for NVDA's focus announcement; the window is virtual now (Rule 3: no timers).
            string announcement = prompt;
            if (inputBuffer.Length > 0)
                announcement += $": {inputBuffer}";
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
        }

        /// <summary>
        /// Closes the window, speaks the echo, then runs the callback, whose own result is queued
        /// behind the echo (FFV_ScreenReaderMod.SpeakTextQueued). A null echo speaks nothing.
        /// </summary>
        private static void CloseWith(string text, Action callback)
        {
            Close();
            if (!string.IsNullOrEmpty(text))
                FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
            callback?.Invoke();
        }

        private static string GetCharacterName(char c)
        {
            switch (c)
            {
                case ' ': return T("space");
                case '.': return T("period");
                case ',': return T("comma");
                case '\'': return T("apostrophe");
                case '"': return T("quote");
                case '-': return T("dash");
                case '_': return T("underscore");
                case ';': return T("semicolon");
                case ':': return T("colon");
                case '!': return T("exclamation");
                case '?': return T("question");
                case '/': return T("slash");
                case '\\': return T("backslash");
                case '(': return T("open paren");
                case ')': return T("close paren");
                case '[': return T("open bracket");
                case ']': return T("close bracket");
                case '{': return T("open brace");
                case '}': return T("close brace");
                case '`': return T("backtick");
                case '~': return T("tilde");
                case '=': return T("equals");
                case '+': return T("plus");
                case '|': return T("pipe");
                default: return c.ToString();
            }
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            onConfirmCallback = null;
            onCancelCallback = null;
        }

        /// <summary>
        /// Handles keyboard input for the text input dialog. Reads keys via GamepadManager;
        /// game input is suppressed while IsOpen. Returns true if input was consumed (dialog open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            if (GamepadManager.IsKeyCodePressed(KeyCode.Return))
            {
                string finalText = inputBuffer.ToString().Trim();
                if (string.IsNullOrEmpty(finalText))
                {
                    FFV_ScreenReaderMod.SpeakText(T("Name cannot be empty"), interrupt: true);
                    return true;
                }
                var callback = onConfirmCallback;
                CloseWith(string.Format(T("Confirmed: {0}"), finalText), () => callback?.Invoke(finalText));
                return true;
            }

            // Every caller's cancel callback names what was cancelled ("Rename cancelled"), so the
            // generic echo is only spoken when there is no callback.
            if (GamepadManager.IsKeyCodePressed(KeyCode.Escape))
            {
                var callback = onCancelCallback;
                CloseWith(callback == null ? T("Cancelled") : null, callback);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.Backspace))
            {
                if (cursorPosition > 0)
                {
                    char deletedChar = inputBuffer[cursorPosition - 1];
                    inputBuffer.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(deletedChar), interrupt: true);
                }
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.LeftArrow))
            {
                if (cursorPosition > 0)
                {
                    cursorPosition--;
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                }
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.RightArrow))
            {
                if (cursorPosition < inputBuffer.Length)
                {
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                    cursorPosition++;
                }
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.UpArrow) || GamepadManager.IsKeyCodePressed(KeyCode.DownArrow))
            {
                string text = inputBuffer.Length > 0 ? inputBuffer.ToString() : T("empty");
                FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.Home))
            {
                cursorPosition = 0;
                if (inputBuffer.Length > 0)
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[0]), interrupt: true);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.End))
            {
                cursorPosition = inputBuffer.Length;
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.Space))
            {
                inputBuffer.Insert(cursorPosition, ' ');
                cursorPosition++;
                return true;
            }

            bool shiftHeld = GamepadManager.IsKeyCodeHeld(KeyCode.LeftShift) || GamepadManager.IsKeyCodeHeld(KeyCode.RightShift);

            // Letters A-Z
            for (KeyCode kc = KeyCode.A; kc <= KeyCode.Z; kc++)
            {
                if (GamepadManager.IsKeyCodePressed(kc))
                {
                    char c = (char)('a' + (kc - KeyCode.A));
                    if (shiftHeld) c = char.ToUpper(c);
                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Numbers 0-9
            for (KeyCode kc = KeyCode.Alpha0; kc <= KeyCode.Alpha9; kc++)
            {
                if (GamepadManager.IsKeyCodePressed(kc))
                {
                    char c = (char)('0' + (kc - KeyCode.Alpha0));
                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Punctuation
            if (HandlePunctuation(KeyCode.Minus, shiftHeld, '_', '-')) return true;
            if (HandlePunctuation(KeyCode.Period, shiftHeld, '>', '.')) return true;
            if (HandlePunctuation(KeyCode.Comma, shiftHeld, '<', ',')) return true;
            if (HandlePunctuation(KeyCode.Quote, shiftHeld, '"', '\'')) return true;
            if (HandlePunctuation(KeyCode.Semicolon, shiftHeld, ':', ';')) return true;
            if (HandlePunctuation(KeyCode.Slash, shiftHeld, '?', '/')) return true;
            if (HandlePunctuation(KeyCode.BackQuote, shiftHeld, '~', '`')) return true;
            if (HandlePunctuation(KeyCode.LeftBracket, shiftHeld, '{', '[')) return true;
            if (HandlePunctuation(KeyCode.Backslash, shiftHeld, '|', '\\')) return true;
            if (HandlePunctuation(KeyCode.RightBracket, shiftHeld, '}', ']')) return true;
            if (HandlePunctuation(KeyCode.Equals, shiftHeld, '+', '=')) return true;

            return true; // Consume all input while dialog is open
        }

        private static bool HandlePunctuation(KeyCode key, bool shiftHeld, char shiftChar, char normalChar)
        {
            if (GamepadManager.IsKeyCodePressed(key))
            {
                char c = shiftHeld ? shiftChar : normalChar;
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }
            return false;
        }
    }
}
