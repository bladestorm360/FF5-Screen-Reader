using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Utils;
using Il2CppInterop.Runtime;

namespace FFV_ScreenReader.Patches
{
    // ============================================================
    // Per-Page Dialogue System with Multi-Line Support
    // Ported from FF4. Announces dialogue text page-by-page as
    // player advances, combining multiple display lines per page.
    // Pipeline: SetContent → store pages → PlayingInit per page → announce
    // Popups:   SetMessage → announce directly (no dedup)
    // Speakers: SetSpeker → store in tracker → prepended on page announce
    // ============================================================

    /// <summary>
    /// Tracks dialogue state for per-page announcements and navigation suppression.
    /// Stores content from SetContent, announces via PlayingInit hook.
    /// Handles multi-line pages by combining lines within page boundaries.
    /// </summary>
    public static class DialogueTracker
    {
        private static bool _isInDialogue = false;
        private static MessageWindowView _cachedView;

        // Page tracking (ported from FF4)
        private static List<string> currentMessageList = new List<string>();
        private static List<int> currentPageBreaks = new List<int>();
        private static int lastAnnouncedPageIndex = -1;

        // Speaker tracking
        private static string currentSpeaker = "";
        private static string lastAnnouncedSpeaker = "";

        /// <summary>
        /// True while a dialogue/message window is active.
        /// </summary>
        public static bool IsInDialogue => _isInDialogue;

        /// <summary>
        /// Caches the MessageWindowView instance for staleness checks.
        /// </summary>
        public static void CacheView(MessageWindowView view) => _cachedView = view;

        /// <summary>
        /// Validates that dialogue is still actually active by checking the MessageWindowView.
        /// If the view is gone or inactive, auto-clears stale dialogue state.
        /// Returns true if dialogue is genuinely active.
        /// </summary>
        public static bool ValidateState()
        {
            if (!_isInDialogue) return false;

            try
            {
                if (_cachedView != null && _cachedView.gameObject != null && _cachedView.gameObject.activeInHierarchy)
                    return true;

                _cachedView = GameObjectCache.Refresh<MessageWindowView>();
                if (_cachedView != null && _cachedView.gameObject != null && _cachedView.gameObject.activeInHierarchy)
                    return true;
            }
            catch
            {
                // View reference became invalid
            }

            OnDialogueEnd();
            _cachedView = null;
            return false;
        }

        /// <summary>
        /// Called when dialogue starts. Suppresses navigation features.
        /// </summary>
        public static void OnDialogueStart()
        {
            if (_isInDialogue) return;
            if (BattleState.IsInBattle) return; // Don't suppress if already in battle

            _isInDialogue = true;

            var mod = FFV_ScreenReaderMod.Instance;
            mod?.SuppressNavigationForDialogue();
        }

        /// <summary>
        /// Called when dialogue ends. Restores navigation features.
        /// </summary>
        public static void OnDialogueEnd()
        {
            if (!_isInDialogue) return;

            _isInDialogue = false;

            var mod = FFV_ScreenReaderMod.Instance;
            mod?.RestoreNavigationAfterDialogue();
        }

        /// <summary>
        /// Store messages and page breaks for per-page retrieval.
        /// Called from SetContent postfix with data read from instance via pointers.
        /// </summary>
        public static void StoreMessages(List<string> messages, List<int> pageBreaks)
        {
            currentMessageList.Clear();
            currentPageBreaks.Clear();

            if (messages == null || messages.Count == 0)
            {
                return;
            }

            // Store cleaned messages
            foreach (var msg in messages)
            {
                currentMessageList.Add(msg != null ? CleanMessage(msg) : "");
            }

            // Convert page breaks (ending line indices) to start indices
            // newPageLineList contains the ENDING line index (inclusive) for each page
            // e.g., [0, 2] means: page 0 ends at line 0, page 1 ends at line 2
            currentPageBreaks.Add(0); // First page always starts at line 0

            if (pageBreaks != null && pageBreaks.Count > 0)
            {
                for (int i = 0; i < pageBreaks.Count; i++)
                {
                    int nextStart = pageBreaks[i] + 1;
                    if (nextStart < currentMessageList.Count)
                    {
                        currentPageBreaks.Add(nextStart);
                    }
                }
            }

            // Reset page tracking and enter dialogue state
            lastAnnouncedPageIndex = -1;
            OnDialogueStart();
        }

        /// <summary>
        /// Set the current speaker. Will be prepended to page text if changed.
        /// </summary>
        public static void SetSpeaker(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
                return;

            currentSpeaker = speaker.Trim();
        }

        /// <summary>
        /// Gets all lines for a given page index, combined into one string.
        /// </summary>
        public static string GetPageText(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= currentPageBreaks.Count)
                return null;

            int startLine = currentPageBreaks[pageIndex];
            int endLine = (pageIndex + 1 < currentPageBreaks.Count)
                ? currentPageBreaks[pageIndex + 1]
                : currentMessageList.Count;

            var sb = new StringBuilder();
            for (int i = startLine; i < endLine && i < currentMessageList.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(currentMessageList[i]))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append(currentMessageList[i]);
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// Announce the current page. Called from PlayingInit.
        /// Skips already-announced pages, prepends speaker if changed.
        /// </summary>
        public static void AnnounceForPage(int pageIndex, string speakerFromInstance)
        {
            // Update speaker from instance if available
            if (!string.IsNullOrWhiteSpace(speakerFromInstance))
            {
                SetSpeaker(speakerFromInstance);
            }

            // Skip if not in dialogue or no pages stored
            if (!_isInDialogue || currentPageBreaks.Count == 0)
                return;

            // Skip if already announced this page or out of range
            if (pageIndex < 0 || pageIndex >= currentPageBreaks.Count || pageIndex == lastAnnouncedPageIndex)
                return;

            string pageText = GetPageText(pageIndex);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                lastAnnouncedPageIndex = pageIndex; // Advance past empty page
                return;
            }

            // Build announcement with speaker if changed
            string announcement;
            if (!string.IsNullOrEmpty(currentSpeaker) && currentSpeaker != lastAnnouncedSpeaker)
            {
                announcement = $"{currentSpeaker}: {pageText}";
                lastAnnouncedSpeaker = currentSpeaker;
            }
            else
            {
                announcement = pageText;
            }

            lastAnnouncedPageIndex = pageIndex;
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }

        /// <summary>
        /// Full reset — clears page data, speaker, and ends dialogue state.
        /// Called when dialogue window closes (MessageWindowManager.Close).
        /// </summary>
        public static void Reset()
        {
            currentMessageList.Clear();
            currentPageBreaks.Clear();
            lastAnnouncedPageIndex = -1;
            currentSpeaker = "";
            lastAnnouncedSpeaker = "";
            OnDialogueEnd();
            _cachedView = null;
        }

        private static string CleanMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            string clean = TextUtils.StripIconMarkup(message);
            return TextUtils.NormalizeWhitespace(clean);
        }
    }

    /// <summary>
    /// Pointer-based access to MessageWindowManager IL2CPP fields.
    /// Offsets verified identical to FF4 in FF5 dump.cs.
    /// </summary>
    public static class MessageWindowHelper
    {
        private const int OFFSET_MESSAGE_LIST = 0x88;        // List<string> messageList
        private const int OFFSET_NEW_PAGE_LINE_LIST = 0xA0;  // List<int> newPageLineList
        private const int OFFSET_SPEAKER_VALUE = 0xA8;       // string spekerValue
        private const int OFFSET_CURRENT_PAGE_NUMBER = 0xF8; // int currentPageNumber

        /// <summary>
        /// Reads the messageList field from a manager instance using pointer-based access.
        /// </summary>
        public static List<string> ReadMessageListFromInstance(MessageWindowManager instance)
        {
            if (instance == null)
                return null;

            try
            {
                IntPtr instancePtr = instance.Pointer;
                if (instancePtr == IntPtr.Zero)
                    return null;

                unsafe
                {
                    IntPtr listPtr = *(IntPtr*)((byte*)instancePtr.ToPointer() + OFFSET_MESSAGE_LIST);
                    if (listPtr == IntPtr.Zero)
                        return null;

                    var il2cppList = new Il2CppSystem.Collections.Generic.List<string>(listPtr);
                    if (il2cppList == null)
                        return null;

                    var result = new List<string>();
                    int count = il2cppList.Count;

                    for (int i = 0; i < count; i++)
                    {
                        var msg = il2cppList[i];
                        result.Add(msg ?? "");
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessageWindow] Error reading messageList: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the newPageLineList field from a manager instance using pointer-based access.
        /// </summary>
        public static List<int> ReadPageBreaksFromInstance(MessageWindowManager instance)
        {
            if (instance == null)
                return null;

            try
            {
                IntPtr instancePtr = instance.Pointer;
                if (instancePtr == IntPtr.Zero)
                    return null;

                unsafe
                {
                    IntPtr listPtr = *(IntPtr*)((byte*)instancePtr.ToPointer() + OFFSET_NEW_PAGE_LINE_LIST);
                    if (listPtr == IntPtr.Zero)
                        return null;

                    var il2cppList = new Il2CppSystem.Collections.Generic.List<int>(listPtr);
                    if (il2cppList == null)
                        return null;

                    var result = new List<int>();
                    int count = il2cppList.Count;

                    for (int i = 0; i < count; i++)
                    {
                        result.Add(il2cppList[i]);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessageWindow] Error reading newPageLineList: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the spekerValue field from a manager instance using pointer-based access.
        /// </summary>
        public static string ReadSpeakerFromInstance(MessageWindowManager instance)
        {
            if (instance == null)
                return null;

            try
            {
                IntPtr instancePtr = instance.Pointer;
                if (instancePtr == IntPtr.Zero)
                    return null;

                unsafe
                {
                    IntPtr stringPtr = *(IntPtr*)((byte*)instancePtr.ToPointer() + OFFSET_SPEAKER_VALUE);
                    if (stringPtr == IntPtr.Zero)
                        return null;

                    return IL2CPP.Il2CppStringToManaged(stringPtr);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessageWindow] Error reading speaker: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current page number from MessageWindowManager instance using pointer-based access.
        /// </summary>
        public static int GetCurrentPageNumber(MessageWindowManager instance)
        {
            if (instance == null)
                return -1;

            try
            {
                IntPtr instancePtr = instance.Pointer;
                if (instancePtr == IntPtr.Zero)
                    return -1;

                unsafe
                {
                    int pageNum = *(int*)((byte*)instancePtr.ToPointer() + OFFSET_CURRENT_PAGE_NUMBER);
                    return pageNum;
                }
            }
            catch
            {
                return -1;
            }
        }
    }

    // ============================================================
    // Harmony Patches
    // ============================================================

    /// <summary>
    /// Stores speaker name in DialogueTracker for prepending to page text.
    /// Speaker is announced as part of the page, not separately.
    /// </summary>
    [HarmonyPatch(typeof(MessageWindowManager), "SetSpeker")]
    public static class MessageWindowManager_SetSpeker_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string name)
        {
            try
            {
                DialogueTracker.SetSpeaker(name);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowManager.SetSpeker patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles popup/non-dialogue text from MessageWindowView.
    /// During dialogue, text flows through SetContent → PlayingInit pipeline instead.
    /// No dedup — popups must re-read when re-triggered.
    /// </summary>
    [HarmonyPatch(typeof(MessageWindowView), "SetMessage")]
    public static class MessageWindowView_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MessageWindowView __instance, string message)
        {
            try
            {
                DialogueTracker.CacheView(__instance);
                if (string.IsNullOrWhiteSpace(message))
                    return;

                if (DialogueTracker.IsInDialogue)
                {
                    return;
                }

                CoroutineManager.StartUntracked(DelayedSetMessageSpeak(message.Trim()));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowView.SetMessage patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedSetMessageSpeak(string message)
        {
            yield return null;

            if (PopupState.IsConfirmationPopupActive)
            {
                yield break;
            }

            if (SaveLoadMenuState.ShouldSuppress())
            {
                yield break;
            }

            if (NamingPatches.ShouldSuppress())
            {
                yield break;
            }

            FFV_ScreenReaderMod.SpeakText(message, interrupt: false);
        }
    }

    /// <summary>
    /// Reads messageList and page breaks from instance via pointers, stores in DialogueTracker.
    /// Does NOT speak — text is announced per-page by PlayingInit.
    /// </summary>
    [HarmonyPatch(typeof(MessageWindowManager), "SetContent")]
    public static class MessageWindowManager_SetContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MessageWindowManager __instance)
        {
            try
            {
                var messageList = MessageWindowHelper.ReadMessageListFromInstance(__instance);
                var pageBreaks = MessageWindowHelper.ReadPageBreaksFromInstance(__instance);
                DialogueTracker.StoreMessages(messageList, pageBreaks);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowManager.SetContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Per-page dialogue announcement. Fires when entering Playing state — once per page.
    /// Gets current page number from instance and announces via DialogueTracker.
    /// </summary>
    [HarmonyPatch(typeof(MessageWindowManager), "PlayingInit")]
    public static class MessageWindowManager_PlayingInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MessageWindowManager __instance)
        {
            try
            {
                int currentPage = MessageWindowHelper.GetCurrentPageNumber(__instance);
                string speaker = MessageWindowHelper.ReadSpeakerFromInstance(__instance);
                DialogueTracker.AnnounceForPage(currentPage, speaker);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowManager.PlayingInit patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch FadeMessageManager for location names, chapter titles, etc.
    /// </summary>
    [HarmonyPatch(typeof(FadeMessageManager), "Play")]
    public static class FadeMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                string cleanMessage = message.Trim();
                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in FadeMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch LineFadeMessageManager for scrolling credits, intro text, etc.
    /// </summary>
    [HarmonyPatch(typeof(LineFadeMessageManager), "Play")]
    public static class LineFadeMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSystem.Collections.Generic.List<string> messages)
        {
            try
            {
                if (messages == null || messages.Count == 0)
                {
                    return;
                }

                var sb = new StringBuilder();
                for (int i = 0; i < messages.Count; i++)
                {
                    string msg = messages[i];
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        sb.AppendLine(msg.Trim());
                    }
                }

                string fullText = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    FFV_ScreenReaderMod.SpeakText(fullText, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in LineFadeMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch SystemMessageWindowManager for system messages.
    /// </summary>
    [HarmonyPatch(typeof(SystemMessageWindowManager), "SetMessage")]
    public static class SystemMessageWindowManager_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string messageId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    return;
                }

                // Try to resolve the message ID to actual text
                var messageManager = MessageManager.Instance;
                if (messageManager != null)
                {
                    string message = messageManager.GetMessage(messageId);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        // Skip if this is the current map name — already announced by GameStatePatches
                        string currentMap = MapNameResolver.GetCurrentMapName();
                        if (!string.IsNullOrEmpty(currentMap) &&
                            message.Trim().Equals(currentMap.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        FFV_ScreenReaderMod.SpeakText(message, interrupt: true);
                    }
                }
                else
                {
                    // Fallback: speak the ID itself
                    FFV_ScreenReaderMod.SpeakText(messageId, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SystemMessageWindowManager.SetMessage patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch MessageChoiceWindowManager for choice menus.
    /// </summary>
    [HarmonyPatch(typeof(MessageChoiceWindowManager), "Play", new Type[] { typeof(string[]) })]
    public static class MessageChoiceWindowManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                {
                    return;
                }

                var sb = new StringBuilder("Choices: ");
                for (int i = 0; i < values.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(values[i]))
                    {
                        sb.Append(values[i].Trim());
                        if (i < values.Length - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                }

                string choicesText = sb.ToString();
                FFV_ScreenReaderMod.SpeakText(choicesText, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageChoiceWindowManager.Play patch: {ex.Message}");
            }
        }
    }
}
