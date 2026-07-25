using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Utils;

using StatusWindowController = Il2CppLast.UI.KeyInput.StatusWindowController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces the initially-focused character when the status character-select list is entered
    /// or returned to.
    ///
    /// StatusWindowController.SelectContent only fires on cursor movement, and it explicitly
    /// rejects calls whose cursor GameObject is not yet activeInHierarchy — which is exactly the
    /// state during initial population, so opening Status was silent. The settle loop here re-reads
    /// once the list IS active.
    ///
    /// Manual patching because the state-entry methods are protected overrides.
    ///
    /// Note on the shop: no equivalent hook is added there. ShopCommandMenuController.SetCursor and
    /// ShopListItemContentController.SetFocus already fire on shop entry, so the command bar and
    /// the buy/sell lists announce themselves — adding a second announcer would be the redundant
    /// call pattern, not a fix for it.
    /// </summary>
    public static class FieldStatusReannouncePatches
    {
        // StatusWindowController (KeyInput, dump.cs 445149): contentList 0xC8
        // StatusWindowControllerBase (296263): selectCursor 0x40 (inherited)
        // Both read typed below; offsets documented for traceability only.

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, "ListInit", nameof(List_Init_Postfix));
            Patch(harmony, "SelectInit", nameof(List_Init_Postfix));

            // Safe to hook: StatusWindowController.NonInit has a real body at its own unique
            // address. Empty *Init methods are NOT safe — IL2CPP folds every empty body in the
            // game onto one shared native address, so patching one detours thousands and crashes
            // on launch. Check script.json for a duplicated "Address" before hooking any *Init.
            Patch(harmony, "NonInit", nameof(Exit_Init_Postfix));
        }

        /// <summary>The window left the list — drop any pending read.</summary>
        public static void Exit_Init_Postfix() => MenuFocusAnnouncer.Cancel();

        public static void List_Init_Postfix(object __instance)
        {
            var controller = __instance as StatusWindowController;
            if (controller == null) return;

            // A state (re)entry is authoritative: clear the nav guard so the focused character
            // speaks even when it is the one that was last announced before leaving.
            StatusMenuState.ClearLast();
            MenuFocusAnnouncer.Request("Status", () => TryAnnounceCharacter(controller));
        }

        private static bool TryAnnounceCharacter(StatusWindowController controller)
        {
            if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
            if (!MenuFocusAnnouncer.IsMenuOpen()) return false; // false during a map/asset load

            var contentList = controller.contentList;           // List<StatusWindowContentController> @ 0xC8
            if (contentList == null || contentList.Count == 0) return false;

            var cursor = controller.selectCursor;               // Cursor @ 0x40 (inherited)
            if (cursor == null) return false;

            int index = cursor.Index;
            if (index < 0 || index >= contentList.Count) return false;

            var content = contentList[index];
            if (content == null) return false;

            // Marks the menu as user-opened only once the window is genuinely up, so the existing
            // load-time suppression in StatusDetailsPatches.InitDisplay still holds.
            StatusMenuTracker.IsUserOpened = true;

            return StatusMenuState.AnnounceCharacterRow(content.transform, index, contentList.Count);
        }

        private static void Patch(HarmonyLib.Harmony harmony, string methodName, string postfixName)
        {
            try
            {
                var target = AccessTools.Method(typeof(StatusWindowController), methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[Status] StatusWindowController.{methodName} not found");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(FieldStatusReannouncePatches).GetMethod(postfixName)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Status] Failed to patch StatusWindowController.{methodName}: {ex.Message}");
            }
        }
    }
}
