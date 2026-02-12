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
    /// CommandMenuController.SetFocus reads menu item names.
    /// MainMenuController.Show clears stale tracker state on menu open.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.CommandMenuController), nameof(Il2CppLast.UI.CommandMenuController.SetFocus))]
    public static class CommandMenuController_SetFocus_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.CommandMenuController __instance, Il2CppLast.Defaine.MenuCommandId id)
        {
            try
            {
                if (__instance == null) return;

                var contents = __instance.contents;
                if (contents == null) return;

                // Find the content view matching the focused command ID
                for (int i = 0; i < contents.Count; i++)
                {
                    var content = contents[i];
                    if (content == null) continue;

                    if (content.Data != null && content.Data.Id == id)
                    {
                        if (content.NameText != null && !string.IsNullOrEmpty(content.NameText.text))
                        {
                            string menuText = content.NameText.text.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                bool shouldAnnounce = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.MAIN_MENU_SET_FOCUS, menuText);
                                if (shouldAnnounce)
                                {
                                    FFV_ScreenReaderMod.SpeakText(menuText, interrupt: true);
                                }
                                else
                                {
                                    // Already announced by cursor nav. Reset so return-from-submenu can re-announce.
                                    AnnouncementDeduplicator.Reset(AnnouncementContexts.MAIN_MENU_SET_FOCUS);
                                }
                            }
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MainMenu] Error in SetFocus patch: {ex.Message}");
            }
        }
    }

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

                // Reset announcement deduplicator for main menu to allow re-reading
                AnnouncementDeduplicator.Reset(AnnouncementContexts.MAIN_MENU_SET_FOCUS);

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
