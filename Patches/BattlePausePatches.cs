using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using BattlePauseController = Il2CppLast.UI.KeyInput.BattlePauseController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Reads the battle pause menu (Resume / Return to Title, ...). The generic cursor reader skips
    /// every cursor in battle, so without this the menu was silent.
    ///
    /// SetCommandSelectCursor (private, unique RVA 0x43F7E0) is the event hook: it re-parents the
    /// cursor onto the focused command and runs once per focus change — on open (SetCursorToDefault,
    /// and the first-show branch of UpdateSelect) and on every move (the Cursor.NextIndex/PrevIndex
    /// callback, UpdateSelect's b__27_1). Do NOT hook UpdateFocus: UpdateSelect calls it every frame
    /// while the menu is up with no popup open.
    /// </summary>
    [HarmonyPatch(typeof(BattlePauseController), nameof(BattlePauseController.SetCommandSelectCursor))]
    public static class BattlePauseController_SetCommandSelectCursor_Patch
    {
        // Opening runs the hook twice in one frame (SetCursorToDefault, then UpdateSelect's
        // first-show branch) on the same row. Same row + same frame = the same event.
        private static int _lastIndex = -1;
        private static int _lastFrame = -1;

        [HarmonyPostfix]
        public static void Postfix(BattlePauseController __instance)
        {
            try
            {
                var cursor = __instance?.selectCommandCursor;
                var commands = __instance.view?.commandControllerList;
                if (cursor == null || commands == null) return;

                int index = cursor.Index;
                int frame = UnityEngine.Time.frameCount;
                if (index == _lastIndex && frame == _lastFrame) return;
                _lastIndex = index;
                _lastFrame = frame;

                var command = SelectContentHelper.TryGetItem(commands, index);
                if (command == null) return;

                string name = command.CommandText != null ? command.CommandText.text : null;
                if (string.IsNullOrWhiteSpace(name)) name = command.Name;
                if (string.IsNullOrWhiteSpace(name)) return;

                FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(name.Trim(), index, commands.Count), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BattlePause] Error reading pause command: {ex.Message}");
            }
        }
    }
}
