using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Defaine;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for the title menu.
    /// Announces menu items directly from TitleMenuCommandController instead of hierarchy walking.
    /// </summary>

    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.TitleMenuCommandController), nameof(Il2CppLast.UI.KeyInput.TitleMenuCommandController.SetCursor))]
    public static class TitleMenuCommandController_SetCursor_Patch
    {
        private static string _pendingText;
        private static int _pendingCommandId;
        private static bool _announcePending;

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.TitleMenuCommandController __instance, int index)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Clear config menu state when returning to title menu
                ConfigMenuState.ClearState();

                // Get the active contents list
                var activeContents = __instance.activeContents;
                if (activeContents == null || activeContents.Count == 0)
                {
                    return;
                }

                // Validate index
                if (index < 0 || index >= activeContents.Count)
                {
                    return;
                }

                // Get the view at the cursor position - no hierarchy walking!
                var contentView = activeContents[index];
                if (contentView == null)
                {
                    return;
                }

                // Get the command data which contains the localized name
                var commandData = contentView.Data;
                if (commandData == null)
                {
                    return;
                }

                // Get the localized name from the data
                string commandName = commandData.Name;
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                // Cache the announcement — if multiple SetCursor calls happen in the same frame
                // (e.g., SetDefaultCursor then SetCursorPositionMemory), only the last one wins.
                bool wasAlreadyPending = _announcePending;
                _pendingText = commandName;
                _pendingCommandId = (int)contentView.CommandId;
                _announcePending = true;

                if (!wasAlreadyPending)
                {
                    CoroutineManager.StartManaged(DeferredAnnounce());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in TitleMenuCommandController.SetCursor patch: {ex.Message}");
            }
        }

        private static IEnumerator DeferredAnnounce()
        {
            // Wait one frame so all same-frame SetCursor calls can overwrite the cache
            yield return null;

            if (_announcePending)
            {
                _announcePending = false;
                string text = _pendingText;
                int commandId = _pendingCommandId;

                if (!string.IsNullOrWhiteSpace(text) &&
                    AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.TITLE_MENU_COMMAND, commandId))
                {
                    FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
                }
            }
        }
    }

}
