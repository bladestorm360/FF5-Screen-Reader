using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Utils;

using GameCursor = Il2CppLast.UI.Cursor;
using KeyInputSaveListController = Il2CppLast.UI.KeyInput.SaveListController;
using KeyInputLoadGameWindowController = Il2CppLast.UI.KeyInput.LoadGameWindowController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces the initially-focused save slot when the save/load list opens. One hook covers
    /// all three entry points (title Load, field Save, field Load) because they all activate the
    /// same SaveListController.
    ///
    /// Navigation is already handled by SaveListController.SelectContent in SaveLoadPatches, but
    /// that does not fire for the initial cursor placement, so opening the list was silent.
    ///
    /// Only the KeyInput controller is hooked: Il2CppLast.UI.Touch.SaveListController (dump.cs
    /// 434790) has no SetActive at all.
    /// </summary>
    [HarmonyPatch(typeof(KeyInputSaveListController), nameof(KeyInputSaveListController.SetActive),
        new Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    public static class SaveListController_SetActive_Patch
    {
        // SaveListController.selectCursor @ 0x58 (dump.cs 469543) — read typed below.

        [HarmonyPostfix]
        public static void Postfix(KeyInputSaveListController __instance, bool isActive)
        {
            try
            {
                if (!isActive || __instance == null) return;

                // The list was just (re)activated, so the slot under the cursor has not been
                // announced yet regardless of what the nav path said last time.
                SaveLoadPatches.ResetLastAnnouncedIndex();

                MenuFocusAnnouncer.Request("SaveList", () => TryReadSlot(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveList] Error scheduling slot read: {ex.Message}");
            }
        }

        private static bool TryReadSlot(KeyInputSaveListController controller)
        {
            if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
            if (!ShouldReadSaveSlot()) return false;

            // A confirmation popup on top of the list owns the speech — don't talk over it.
            if (PopupState.ShouldSuppress()) return false;
            if (SaveLoadMenuState.IsInConfirmation) return false;

            var cursor = controller.selectCursor;
            if (cursor == null || !MenuFocusAnnouncer.IsAlive(cursor)) return false;

            return SaveLoadPatches.AnnounceSlotAtIndex(controller.Pointer, cursor.Index, isKeyInput: true);
        }

        /// <summary>
        /// True when save-slot content should be announced: a real MenuManager menu is open, OR
        /// the title Load screen (LoadGameWindowController, which is NOT a MenuManager menu) is
        /// on-screen. This is what excludes the BACKGROUND AUTOSAVE, whose SaveListController is
        /// momentarily active during scene construction on a map load.
        /// </summary>
        public static bool ShouldReadSaveSlot()
        {
            if (MenuFocusAnnouncer.IsMenuOpen()) return true;

            try
            {
                var loadWindow = UnityEngine.Object.FindObjectOfType<KeyInputLoadGameWindowController>();
                return MenuFocusAnnouncer.IsAlive(loadWindow);
            }
            catch
            {
                return false;
            }
        }
    }
}
