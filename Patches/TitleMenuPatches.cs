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
        private static bool _announcePending;

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.TitleMenuCommandController __instance, int index)
        {
            // Clear config menu state when returning to title menu
            ConfigMenuState.ClearState();

            Queue(__instance, index);
        }

        /// <summary>
        /// Queues the command at <paramref name="index"/> for announcement, returning true when
        /// there was a readable command there. Everything funnels through the one-frame coalesce
        /// below, so a list-entry hook and a SetCursor firing in the same frame produce a single
        /// announcement rather than a double.
        /// </summary>
        internal static bool Queue(Il2CppLast.UI.KeyInput.TitleMenuCommandController controller, int index)
        {
            try
            {
                // Safety checks
                if (controller == null)
                {
                    return false;
                }

                // Get the active contents list
                var activeContents = controller.activeContents;
                if (activeContents == null || activeContents.Count == 0)
                {
                    return false;
                }

                // Validate index
                if (index < 0 || index >= activeContents.Count)
                {
                    return false;
                }

                // Get the view at the cursor position - no hierarchy walking!
                var contentView = activeContents[index];
                if (contentView == null)
                {
                    return false;
                }

                // Get the command data which contains the localized name
                var commandData = contentView.Data;
                if (commandData == null)
                {
                    return false;
                }

                // Get the localized name from the data
                string commandName = commandData.Name;
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return false;
                }

                // Cache the announcement — if multiple SetCursor calls happen in the same frame
                // (e.g., SetDefaultCursor then SetCursorPositionMemory), only the last one wins.
                bool wasAlreadyPending = _announcePending;
                _pendingText = commandName;
                _announcePending = true;

                if (!wasAlreadyPending)
                {
                    CoroutineManager.StartManaged(DeferredAnnounce());
                }

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in TitleMenuCommandController.SetCursor patch: {ex.Message}");
            }
            return false;
        }

        private static IEnumerator DeferredAnnounce()
        {
            // Wait one frame so all same-frame SetCursor calls can overwrite the cache
            yield return null;

            if (_announcePending)
            {
                _announcePending = false;
                string text = _pendingText;

                // No cross-frame guard: the _announcePending coalesce above already collapses the
                // same-frame SetDefaultCursor + SetCursorPositionMemory pair, and returning to the
                // title from Extras SHOULD re-announce the focused command.
                if (!string.IsNullOrWhiteSpace(text))
                {
                    FFV_ScreenReaderMod.SpeakText(text, interrupt: true);
                }
            }
        }
    }

    /// <summary>
    /// Announces the initially-focused command when the title OPTIONS list (Config / Privacy
    /// Policy / …) is entered. Scope is deliberately just that one list — see ApplyPatches.
    ///
    /// TitleMenuCommandController.SetCursor only fires on cursor movement, so confirming into the
    /// Options list left it silent: the cursor is placed without moving. This is the KeyInput
    /// state-entry method; note the KeyInput controller names it InitializeOption while the Touch
    /// variant uses InitOption — patching the Touch name would silently never fire.
    ///
    /// Reads funnel through TitleMenuCommandController_SetCursor_Patch.Queue, so if the game also
    /// fires SetCursor during entry the one-frame coalesce collapses both into one announcement.
    ///
    /// Manual patching because all three targets are private. Addresses verified unique in
    /// script.json (6605984 / 6608256 / 6607552) — patching a folded empty stub crashes on launch.
    /// </summary>
    public static class TitleListFocusPatches
    {
        // KeyInput TitleWindowController (dump.cs 479257): commandController 0x50
        // KeyInput TitleMenuCommandController (479028): activeContents 0x28, selectCursor 0x30
        // Both read typed below; offsets documented for traceability only.

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            // ONLY the Options list is hooked — it is the one title list the game leaves silent on
            // entry. Everything else here already announces itself and must be left alone:
            //   InitSelect      — the main title list fires SetCursor on first appearance and on
            //                     back-out from a sub-list; hooking it spoke "Load Game" / "Options"
            //                     twice, one frame apart.
            //   InitializeExtra — the Extras list already announced correctly.
            Patch(harmony, "InitializeOption", nameof(TitleList_Init_Postfix));
        }

        public static void TitleList_Init_Postfix(object __instance)
        {
            var window = __instance as Il2CppLast.UI.KeyInput.TitleWindowController;
            if (window == null) return;

            MenuFocusAnnouncer.Request("TitleList", () => TryAnnounceTitleFocus(window));
        }

        private static bool TryAnnounceTitleFocus(Il2CppLast.UI.KeyInput.TitleWindowController window)
        {
            if (!MenuFocusAnnouncer.IsAlive(window)) return false;

            // The title screen is not a MenuManager menu, so IsMenuOpen() cannot gate this; the
            // command list being populated and on screen is the readiness signal.
            var commandController = window.commandController;       // @ 0x50
            if (commandController == null || !MenuFocusAnnouncer.IsAlive(commandController)) return false;

            var cursor = commandController.selectCursor;            // @ 0x30
            if (cursor == null) return false;

            return TitleMenuCommandController_SetCursor_Patch.Queue(commandController, cursor.Index);
        }

        private static void Patch(HarmonyLib.Harmony harmony, string methodName, string postfixName)
        {
            try
            {
                var target = AccessTools.Method(typeof(Il2CppLast.UI.KeyInput.TitleWindowController), methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[TitleList] TitleWindowController.{methodName} not found");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(TitleListFocusPatches).GetMethod(postfixName)));
                MelonLogger.Msg($"[TitleList] TitleWindowController.{methodName} patch applied");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TitleList] Failed to patch TitleWindowController.{methodName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Speaks the title screen's "Press any button" prompt when it appears. InitShortcutCommand
    /// (private, unique RVA 0x64D070) is the ShortcutCommand state's entry: it hides the menu and
    /// activates view.startParent, the prompt. Read one frame later so the label is rendered; if it
    /// is still blank, fall back to the game's own MENU_TITLE_PRESS_TEXT message.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.TitleWindowController), "InitShortcutCommand")]
    public static class TitleWindowController_InitShortcutCommand_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.TitleWindowController __instance)
        {
            if (__instance != null)
                CoroutineManager.StartManaged(AnnouncePressPrompt(__instance));
        }

        private static IEnumerator AnnouncePressPrompt(Il2CppLast.UI.KeyInput.TitleWindowController window)
        {
            yield return null;

            try
            {
                var startText = window?.view?.startText;                 // TitleWindowView.startText @ 0x30
                if (startText == null || startText.gameObject == null || !startText.gameObject.activeInHierarchy)
                    yield break;

                string text = TextUtils.StripIconMarkup(startText.text);
                if (string.IsNullOrWhiteSpace(text))
                    text = TextUtils.StripIconMarkup(LocalizationHelper.GetGameMessage("MENU_TITLE_PRESS_TEXT"));
                if (!string.IsNullOrWhiteSpace(text))
                    FFV_ScreenReaderMod.SpeakText(text, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Title] Error announcing press prompt: {ex.Message}");
            }
        }
    }

}
