using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Menus;
using FFV_ScreenReader.Utils;
using GameCursor = Il2CppLast.UI.Cursor;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Shared helper for cursor navigation patches.
    /// Walks the hierarchy ONCE and checks all exclusion patterns in a single pass,
    /// replacing the previous approach of 11 separate hierarchy walks per patch
    /// plus a FindObjectsOfType call.
    /// </summary>
    internal static class CursorExclusionHelper
    {
        // All parent name substrings that should cause cursor announcement to be skipped.
        // Checked case-insensitively against each parent in a single hierarchy walk.
        // "battle" covers all battle UI parents (battle_target, battle_command, battle_item, etc.)
        // Note: "command_menu" (main/camp menu) is NOT excluded here — it's handled by
        // TryReadMainMenu in MenuTextDiscovery with a hierarchy guard instead.
        private static readonly string[] ExclusionPatterns = new string[]
        {
            "item_target_select",
            "battle",
            "title_command",
            "titlemenu",
            "config",
            "list_window",
            "equip_select",
            "equip_info_content",
            "equipmentinfo",
            "shop",
            "party",
            "status",
            "ability_command",
            "command_window",
            "job",
            "ability_change",
        };

        /// <summary>
        /// Returns true if the cursor announcement should be skipped.
        /// Performs null checks, then a single hierarchy walk checking all exclusion patterns.
        /// Also checks MenuStateRegistry for main menu state as a fallback.
        /// </summary>
        public static bool ShouldSkip(GameCursor instance)
        {
            // Suppress generic cursor when save/load menu is active (handled by SaveLoadPatches)
            if (SaveLoadMenuState.IsActive)
                return true;

            // All battle UI has dedicated Harmony patches (command, target, item, ability,
            // message, results). The "battle" exclusion pattern exists in the hierarchy
            // check below, but some cursors (common_cursor) have parents like
            // "menu_parent -> KeyParent" that don't contain "battle".
            if (BattleState.IsInBattle)
                return true;

            if (instance == null || instance.gameObject == null || instance.transform == null)
                return true;

            var parent = instance.transform.parent;
            while (parent != null)
            {
                string parentName = parent.name.ToLower();

                for (int i = 0; i < ExclusionPatterns.Length; i++)
                {
                    if (parentName.Contains(ExclusionPatterns[i]))
                    {
                        // Allow generic cursor through "shop" exclusion when navigating
                        // equipment command bar from shop (EquipmentCommandView.SetFocus
                        // doesn't fire in shop context, so generic cursor must handle it)
                        if (ExclusionPatterns[i] == "shop" && ShopMenuTracker.EnteredEquipmentFromShop)
                            continue;
                        return true;
                    }
                }

                parent = parent.parent;
            }

            return false;
        }
    }

    /// <summary>
    /// Harmony patches for cursor navigation.
    /// Hooks NextIndex, PrevIndex, SkipNextIndex, SkipPrevIndex to announce menu items as players navigate.
    /// Ported from FF6 screen reader.
    /// </summary>
    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.NextIndex))]
    public static class Cursor_NextIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop)
        {
            try
            {
                // Suppress generic cursor during save/load — SavePopupUpdateCommand handles buttons
                if (SaveLoadMenuState.IsActive)
                    return;

                if (PopupState.ShouldSuppress())
                {
                    PopupPatches.ReadCurrentButton(__instance);
                    return;
                }

                if (CursorExclusionHelper.ShouldSkip(__instance))
                    return;

                CoroutineManager.StartManaged(
                    MenuTextDiscovery.WaitAndReadCursor(__instance, "NextIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NextIndex patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.PrevIndex))]
    public static class Cursor_PrevIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop = false)
        {
            try
            {
                // Suppress generic cursor during save/load — SavePopupUpdateCommand handles buttons
                if (SaveLoadMenuState.IsActive)
                    return;

                if (PopupState.ShouldSuppress())
                {
                    PopupPatches.ReadCurrentButton(__instance);
                    return;
                }

                if (CursorExclusionHelper.ShouldSkip(__instance))
                    return;

                CoroutineManager.StartManaged(
                    MenuTextDiscovery.WaitAndReadCursor(__instance, "PrevIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrevIndex patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.SkipNextIndex))]
    public static class Cursor_SkipNextIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, int skipCount, bool isEndPoint = false, bool isLoop = false)
        {
            try
            {
                // Suppress generic cursor during save/load — SavePopupUpdateCommand handles buttons
                if (SaveLoadMenuState.IsActive)
                    return;

                if (PopupState.ShouldSuppress())
                {
                    PopupPatches.ReadCurrentButton(__instance);
                    return;
                }

                if (CursorExclusionHelper.ShouldSkip(__instance))
                    return;

                CoroutineManager.StartManaged(
                    MenuTextDiscovery.WaitAndReadCursor(__instance, "SkipNextIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SkipNextIndex patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.SkipPrevIndex))]
    public static class Cursor_SkipPrevIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, int skipCount, bool isEndPoint = false, bool isLoop = false)
        {
            try
            {
                // Suppress generic cursor during save/load — SavePopupUpdateCommand handles buttons
                if (SaveLoadMenuState.IsActive)
                    return;

                if (PopupState.ShouldSuppress())
                {
                    PopupPatches.ReadCurrentButton(__instance);
                    return;
                }

                if (CursorExclusionHelper.ShouldSkip(__instance))
                    return;

                CoroutineManager.StartManaged(
                    MenuTextDiscovery.WaitAndReadCursor(__instance, "SkipPrevIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SkipPrevIndex patch: {ex.Message}");
            }
        }
    }

    // NOTE: Cursor.set_Index patch was removed - it caused crashes on game load.
    // Menu initialization announcements will need a different approach.
}
