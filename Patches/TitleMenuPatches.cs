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

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.TITLE_MENU_COMMAND, (int)contentView.CommandId))
                {
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(commandName, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in TitleMenuCommandController.SetCursor patch: {ex.Message}");
            }
        }
    }
}
