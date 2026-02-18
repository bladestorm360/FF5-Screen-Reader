using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Modal text input dialog using SDL focus stealing.
    /// Creates an invisible window to capture keyboard input, preventing keys from reaching the game.
    /// Used for waypoint naming and other text input scenarios.
    /// </summary>
    public static class TextInputWindow
    {
        /// <summary>
        /// Whether the text input window is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static StringBuilder inputBuffer = new StringBuilder();
        private static string prompt = "";
        private static Action<string> onConfirmCallback;
        private static Action onCancelCallback;

        // Cursor position for navigation
        private static int cursorPosition = 0;

        // All keys tracked by this dialog
        private static ModKey[] _trackedKeys;

        /// <summary>
        /// Opens the text input dialog.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="initialText">Initial text in the input field</param>
        /// <param name="onConfirm">Callback when user presses Enter (receives final text)</param>
        /// <param name="onCancel">Callback when user presses Escape</param>
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

            // Initialize cursor position to end of text
            cursorPosition = inputBuffer.Length;

            // Build tracked key array including A-Z and 0-9 ranges
            var trackedKeys = new List<ModKey> {
                ModKey.Backspace, ModKey.Return, ModKey.LeftShift, ModKey.RightShift, ModKey.Escape, ModKey.Space,
                ModKey.LeftArrow, ModKey.UpArrow, ModKey.RightArrow, ModKey.DownArrow, ModKey.Home, ModKey.End,
                ModKey.Minus, ModKey.Period, ModKey.Comma, ModKey.Quote,
                ModKey.Semicolon, ModKey.Slash, ModKey.Backtick, ModKey.LeftBracket, ModKey.Backslash, ModKey.RightBracket, ModKey.Equals
            };
            for (int i = 0; i < 26; i++) trackedKeys.Add((ModKey)(0x41 + i)); // A-Z
            for (int i = 0; i < 10; i++) trackedKeys.Add((ModKey)(0x30 + i)); // 0-9

            _trackedKeys = trackedKeys.ToArray();
            InputManager.InitializeKeyStates(_trackedKeys);

            // Steal focus from game
            InputManager.StealFocus("FFV_TextInput");

            // Delay prompt announcement to let NVDA finish announcing window title
            CoroutineManager.StartManaged(DelayedPromptAnnouncement(prompt, inputBuffer.ToString()));
        }

        /// <summary>
        /// Delays the prompt announcement to avoid being interrupted by NVDA's window title announcement.
        /// </summary>
        private static IEnumerator DelayedPromptAnnouncement(string promptText, string initialText)
        {
            // Wait for NVDA focus announcement to complete
            yield return new WaitForSeconds(0.3f);

            string announcement = promptText;
            if (!string.IsNullOrEmpty(initialText))
            {
                announcement += $": {initialText}";
            }
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
        }

        /// <summary>
        /// Converts a character to a speakable name for screen readers.
        /// Letters and numbers are returned as-is; punctuation gets descriptive names.
        /// </summary>
        private static string GetCharacterName(char c)
        {
            switch (c)
            {
                case ' ': return "space";
                case '.': return "period";
                case ',': return "comma";
                case '\'': return "apostrophe";
                case '"': return "quote";
                case '-': return "dash";
                case '_': return "underscore";
                case ';': return "semicolon";
                case ':': return "colon";
                case '!': return "exclamation";
                case '?': return "question";
                case '/': return "slash";
                case '\\': return "backslash";
                case '(': return "open paren";
                case ')': return "close paren";
                case '[': return "open bracket";
                case ']': return "close bracket";
                case '{': return "open brace";
                case '}': return "close brace";
                case '`': return "backtick";
                case '~': return "tilde";
                case '=': return "equals";
                case '+': return "plus";
                case '|': return "pipe";
                default: return c.ToString();
            }
        }

        /// <summary>
        /// Closes the text input dialog and restores focus to game.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            InputManager.RestoreFocus();

            // Clear callbacks
            onConfirmCallback = null;
            onCancelCallback = null;
        }

        /// <summary>
        /// Handles keyboard input for the text input dialog.
        /// Should be called from InputManager.Update() before any other input handling.
        /// Returns true if input was consumed (dialog is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Poll all tracked keys
            InputManager.Poll(_trackedKeys);

            // Enter - confirm
            if (InputManager.IsKeyDown(ModKey.Return))
            {
                string finalText = inputBuffer.ToString().Trim();
                if (string.IsNullOrEmpty(finalText))
                {
                    FFV_ScreenReaderMod.SpeakText("Name cannot be empty", interrupt: true);
                    return true;
                }

                FFV_ScreenReaderMod.SpeakText($"Confirmed: {finalText}", interrupt: true);
                var callback = onConfirmCallback;
                Close();
                callback?.Invoke(finalText);
                return true;
            }

            // Escape - cancel
            if (InputManager.IsKeyDown(ModKey.Escape))
            {
                FFV_ScreenReaderMod.SpeakText("Cancelled", interrupt: true);
                var callback = onCancelCallback;
                Close();
                callback?.Invoke();
                return true;
            }

            // Backspace - delete character before cursor
            if (InputManager.IsKeyDown(ModKey.Backspace))
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

            // Left Arrow - move cursor left
            if (InputManager.IsKeyDown(ModKey.LeftArrow))
            {
                if (cursorPosition > 0)
                {
                    cursorPosition--;
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                }
                return true;
            }

            // Right Arrow - move cursor right
            if (InputManager.IsKeyDown(ModKey.RightArrow))
            {
                if (cursorPosition < inputBuffer.Length)
                {
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                    cursorPosition++;
                }
                return true;
            }

            // Up Arrow - read full text
            if (InputManager.IsKeyDown(ModKey.UpArrow))
            {
                string text = inputBuffer.Length > 0 ? inputBuffer.ToString() : "empty";
                FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
                return true;
            }

            // Down Arrow - read full text
            if (InputManager.IsKeyDown(ModKey.DownArrow))
            {
                string text = inputBuffer.Length > 0 ? inputBuffer.ToString() : "empty";
                FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
                return true;
            }

            // Home - move cursor to start
            if (InputManager.IsKeyDown(ModKey.Home))
            {
                cursorPosition = 0;
                if (inputBuffer.Length > 0)
                {
                    FFV_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[0]), interrupt: true);
                }
                return true;
            }

            // End - move cursor to end
            if (InputManager.IsKeyDown(ModKey.End))
            {
                cursorPosition = inputBuffer.Length;
                return true;
            }

            // Space - silent when typing
            if (InputManager.IsKeyDown(ModKey.Space))
            {
                inputBuffer.Insert(cursorPosition, ' ');
                cursorPosition++;
                return true;
            }

            bool shiftHeld = InputManager.IsKeyHeld(ModKey.LeftShift) || InputManager.IsKeyHeld(ModKey.RightShift);

            // Letters A-Z - silent when typing
            for (int i = 0; i < 26; i++)
            {
                ModKey mk = (ModKey)(0x41 + i);
                if (InputManager.IsKeyDown(mk))
                {
                    char c = (char)('a' + i);
                    if (shiftHeld)
                        c = char.ToUpper(c);

                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Numbers 0-9 - silent when typing
            for (int i = 0; i < 10; i++)
            {
                ModKey mk = (ModKey)(0x30 + i);
                if (InputManager.IsKeyDown(mk))
                {
                    char c = (char)('0' + i);
                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Punctuation - all silent when typing
            if (InputManager.IsKeyDown(ModKey.Minus))
            {
                char c = shiftHeld ? '_' : '-';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Period))
            {
                char c = shiftHeld ? '>' : '.';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Comma))
            {
                char c = shiftHeld ? '<' : ',';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Quote))
            {
                char c = shiftHeld ? '"' : '\'';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Semicolon))
            {
                char c = shiftHeld ? ':' : ';';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Slash))
            {
                char c = shiftHeld ? '?' : '/';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Backtick))
            {
                char c = shiftHeld ? '~' : '`';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.LeftBracket))
            {
                char c = shiftHeld ? '{' : '[';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Backslash))
            {
                char c = shiftHeld ? '|' : '\\';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.RightBracket))
            {
                char c = shiftHeld ? '}' : ']';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Equals))
            {
                char c = shiftHeld ? '+' : '=';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            return true; // Consume all input while dialog is open
        }
    }
}
