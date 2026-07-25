using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using Il2Cpp;
using Il2CppLast.UI;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for the in-game main menu (Items/Magic/Equip/Status/etc.).
    /// MainMenuController.Show clears stale tracker state on menu open.
    ///
    /// Note: CommandMenuController.SetFocus is deliberately NOT patched. The game re-asserts it
    /// on the focused command (nav echo, confirm-into-submenu, return-from-submenu), which is
    /// indistinguishable from its arguments, so it cannot tell "moved here" from "still here".
    /// Navigation is owned by the generic cursor reader (Cursor.NextIndex -> MenuTextDiscovery,
    /// which has a main-menu strategy) and menu open / return-from-submenu by FieldMenuPatches.
    /// The two fire on disjoint events, so neither needs to suppress the other.
    /// </summary>

    /// <summary>
    /// Clears stale menu tracker state when the main menu opens.
    /// Prevents sub-menu content (item names, job names, etc.) from bleeding
    /// into main menu readings when returning from sub-menus.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.MainMenuController), nameof(Il2CppLast.UI.KeyInput.MainMenuController.Show))]
    public static class MainMenuController_Show_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Mark main menu as active so cursor navigation patches skip it
                MenuStateRegistry.SetActive(MenuStateRegistry.MAIN_MENU, true);

                // Clear all menu tracker states to prevent stale data
                ItemMenuTracker.ClearState();
                JobAbilityTrackerHelper.ClearAllTrackers();
                SaveLoadMenuState.ResetState();
                ConfigMenuState.ClearState();
                GameObjectCache.ClearAll();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MainMenu] Error in Show patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clears main menu state when the menu closes (back to field).
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.MainMenuController), nameof(Il2CppLast.UI.KeyInput.MainMenuController.Close))]
    public static class MainMenuController_Close_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                MenuStateRegistry.SetActive(MenuStateRegistry.MAIN_MENU, false);

                // Re-populate cache entries that were wiped by ClearAll() in Show
                GameObjectCache.Refresh<Il2CppLast.Map.FieldPlayerController>();
                GameObjectCache.Refresh<FieldMap>();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MainMenu] Error in Close patch: {ex.Message}");
            }
        }
    }

}
