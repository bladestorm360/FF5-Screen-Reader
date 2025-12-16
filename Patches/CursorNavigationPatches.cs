using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Menus;
using FFV_ScreenReader.Utils;
using Il2CppLast.UI;
using GameCursor = Il2CppLast.UI.Cursor;

// Import target selection state
using FFV_ScreenReader.Patches;

namespace FFV_ScreenReader.Patches
{
    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.NextIndex))]
    public static class Cursor_NextIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, System.Action<int> action, int count, bool isLoop)
        {
            try
            {
                // Skip ALL battle navigation - battle controller patches handle everything in battle
                // This is more reliable than tracking target selection state flags
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities != null && enemyEntities.Length > 0)
                {
                    return; // We're in battle - let controller patches handle it
                }

                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in NextIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in NextIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in NextIndex patch");
                    return;
                }

                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "NextIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
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
        public static void Postfix(GameCursor __instance, System.Action<int> action, int count, bool isLoop = false)
        {
            try
            {
                // Skip ALL battle navigation - battle controller patches handle everything in battle
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities != null && enemyEntities.Length > 0)
                {
                    return; // We're in battle - let controller patches handle it
                }

                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in PrevIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in PrevIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in PrevIndex patch");
                    return;
                }

                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "PrevIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
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
        public static void Postfix(GameCursor __instance, System.Action<int> action, int count, int skipCount, bool isEndPoint = false, bool isLoop = false)
        {
            try
            {
                // Skip ALL battle navigation - battle controller patches handle everything in battle
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities != null && enemyEntities.Length > 0)
                {
                    return; // We're in battle - let controller patches handle it
                }

                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in SkipNextIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in SkipNextIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in SkipNextIndex patch");
                    return;
                }

                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "SkipNextIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
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
        public static void Postfix(GameCursor __instance, System.Action<int> action, int count, int skipCount, bool isEndPoint = false, bool isLoop = false)
        {
            try
            {
                // Skip ALL battle navigation - battle controller patches handle everything in battle
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities != null && enemyEntities.Length > 0)
                {
                    return; // We're in battle - let controller patches handle it
                }

                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in SkipPrevIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in SkipPrevIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in SkipPrevIndex patch");
                    return;
                }

                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "SkipPrevIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
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
