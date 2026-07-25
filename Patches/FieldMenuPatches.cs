using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

using MainMenuController = Il2CppLast.UI.KeyInput.MainMenuController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces the initially-focused command of the FIELD menu (the in-game main menu opened
    /// while walking the map — Item / Magic / Equip / Status / Config / Save / etc.) on open and
    /// on every return from a sub-menu.
    ///
    /// Navigation already flows through the generic cursor reader (Cursor.NextIndex ->
    /// MenuTextDiscovery), but the initial cursor placement never fires Cursor.NextIndex, so
    /// nothing is announced on entry. We hook MainMenuController.Show and InitNone instead, which
    /// the cursor path never fires — the two are disjoint, so neither needs to suppress the other.
    ///
    /// Gated on MenuManager.IsOpen so it never reads during the scene-construction flurry of a
    /// map/asset load (Show and cursor activation can fire then, not just on a real player open).
    /// The non-Touch field command bar (Il2CppLast.UI.CommandMenuController) carries the generic
    /// selectCursor; we reach it via typed Il2CppInterop accessors, no manual offsets.
    ///
    /// Do NOT read MainMenuController.focusId — that is the SELECTED command, not the focused one.
    /// </summary>
    internal static class FieldMenuReader
    {
        internal static void AnnounceFocus(MainMenuController inst)
        {
            if (inst == null) return;
            MenuFocusAnnouncer.Request("Field", () => TryAnnounceFieldFocus(inst));
        }

        /// <summary>
        /// Reads CommandMenuController.contents directly rather than handing the cursor to
        /// MenuTextDiscovery.WaitAndReadCursor: that helper is void (so it cannot report whether
        /// it spoke, and could not drive the settle loop) and takes the list count as a parameter
        /// with no fallback, so passing 0 would silently drop the "(X of Y)" suffix. This is the
        /// same read MenuTextDiscovery.TryReadMainMenu performs for navigation.
        /// </summary>
        private static bool TryAnnounceFieldFocus(MainMenuController inst)
        {
            if (!MenuFocusAnnouncer.IsAlive(inst)) return false;
            if (!MenuFocusAnnouncer.IsMenuOpen()) return false; // false during a map/asset load

            var cmd = inst.commandMenuController;               // Il2CppLast.UI.CommandMenuController @ 0x38
            if (cmd == null || !MenuFocusAnnouncer.IsAlive(cmd)) return false;

            var cursor = cmd.selectCursor;                      // Il2CppLast.UI.Cursor @ 0x38
            var contents = cmd.contents;                        // List<CommandMenuContentView> @ 0x30
            if (cursor == null || contents == null || contents.Count == 0) return false;

            int index = cursor.Index;
            if (index < 0 || index >= contents.Count) return false;

            var content = contents[index];
            if (content == null || content.NameText == null) return false;

            string name = content.NameText.text?.Trim();
            if (string.IsNullOrEmpty(name)) return false;

            // Append list position last, matching every other menu reader.
            FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(name, index, contents.Count), interrupt: true);
            return true;
        }
    }

    [HarmonyPatch(typeof(MainMenuController), "Show", new Type[] { typeof(bool) })]
    public static class MainMenuController_Show_FieldFocus_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MainMenuController __instance) => FieldMenuReader.AnnounceFocus(__instance);
    }

    /// <summary>
    /// Re-announce the focused field command when the menu returns to the command-select (None)
    /// state from any sub-menu OR the quicksave popup. MainMenuController's state machine is keyed
    /// on MenuCommandId; every sub-menu (Item/Magic/Equipment/Status/Sort/Words/Config/Job/Ability/
    /// Save/Load) and the Interruption (quicksave) state transitions back to None on cancel,
    /// firing InitNone. The generation latch in MenuFocusAnnouncer collapses the Show+InitNone
    /// pair on initial open so it announces once.
    /// </summary>
    [HarmonyPatch]
    public static class MainMenuController_InitNone_FieldReturn_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
            => AccessTools.Method(typeof(MainMenuController), "InitNone");

        [HarmonyPostfix]
        public static void Postfix(MainMenuController __instance)
        {
            FieldMenuReader.AnnounceFocus(__instance);
        }
    }

    /// <summary>
    /// Cancels any pending focus read when the field menu closes, so a settle loop still running
    /// from the open cannot fire into the field.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuController), nameof(MainMenuController.Close))]
    public static class MainMenuController_Close_FieldFocus_Patch
    {
        [HarmonyPostfix]
        public static void Postfix() => MenuFocusAnnouncer.Cancel();
    }
}
