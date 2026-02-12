using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using FFV_ScreenReader.Field;
using SubSceneManagerMainGame = Il2CppLast.Management.SubSceneManagerMainGame;
using UserDataManager = Il2CppLast.Management.UserDataManager;
using FieldNavigationHelper = FFV_ScreenReader.Field.FieldNavigationHelper;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks battle state to suppress navigation features during combat.
    /// Stores pre-battle settings and restores them when battle ends.
    /// </summary>
    public static class BattleState
    {
        private static bool _isInBattle = false;
        private static NavigationStateSnapshot _preBattleSnapshot;

        /// <summary>
        /// True while in battle. Checked by InputManager to block navigation keys.
        /// </summary>
        public static bool IsInBattle => _isInBattle;

        /// <summary>
        /// Called when battle starts. Stores current navigation settings and suppresses them.
        /// </summary>
        public static void SetActive()
        {
            if (_isInBattle) return;

            _isInBattle = true;

            var mod = FFV_ScreenReader.Core.FFV_ScreenReaderMod.Instance;
            if (mod == null) return;

            _preBattleSnapshot = NavigationStateSnapshot.Capture(AudioLoopManager.Instance);

            mod.SuppressNavigationForBattle();
        }

        /// <summary>
        /// Called when battle ends. Restores pre-battle navigation settings.
        /// </summary>
        public static void Reset()
        {
            if (!_isInBattle) return;

            _isInBattle = false;

            ActiveBattleCharacterTracker.CurrentActiveCharacter = null;

            var mod = FFV_ScreenReader.Core.FFV_ScreenReaderMod.Instance;
            if (mod == null) return;

            _preBattleSnapshot.RestoreTo(AudioLoopManager.Instance);
        }
    }

    /// <summary>
    /// Patches for game state transitions (field, battle, menu, etc.).
    /// Hooks SubSceneManagerMainGame.ChangeState for event-driven map transition
    /// instead of per-frame polling.
    /// Also provides fade detection via cached reflection on FadeManager.
    /// </summary>
    public static class GameStatePatches
    {
        // Field states that indicate player is on the field map
        private const int STATE_CHANGE_MAP = 1;
        private const int STATE_FIELD_READY = 2;
        private const int STATE_PLAYER = 3;

        // Fade detection via cached reflection
        private static bool fadeInitialized = false;
        private static PropertyInfo fadeInstanceProperty;
        private static MethodInfo isFadeFinishMethod;

        /// <summary>
        /// True while the screen is fading (fade not finished).
        /// Checked by wall tone loop to suppress tones during transitions.
        /// </summary>
        public static bool IsScreenFading
        {
            get
            {
                if (!fadeInitialized) return false;
                try
                {
                    object instance = fadeInstanceProperty.GetValue(null);
                    if (instance == null) return false;

                    bool isFadeFinish = (bool)isFadeFinishMethod.Invoke(instance, null);
                    return !isFadeFinish;
                }
                catch { return false; }
            }
        }

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                var changeStateMethod = AccessTools.Method(
                    typeof(SubSceneManagerMainGame),
                    "ChangeState",
                    new Type[] { typeof(SubSceneManagerMainGame.State) }
                );

                if (changeStateMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(GameStatePatches), nameof(ChangeState_Postfix));
                    harmony.Patch(changeStateMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    MelonLogger.Warning("[GameState] Could not find SubSceneManagerMainGame.ChangeState method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GameState] Error applying patches: {ex.Message}");
            }

            InitializeFadeDetection();
        }

        /// <summary>
        /// Initializes cached reflection for FadeManager polling.
        /// Scans assemblies for FadeManager type and caches Instance property and IsFadeFinish method.
        /// </summary>
        private static void InitializeFadeDetection()
        {
            if (fadeInitialized) return;

            try
            {
                Type fadeManagerType = FindFadeManagerType();
                if (fadeManagerType == null)
                {
                    MelonLogger.Warning("[GameState] FadeManager type not found — fade detection disabled");
                    return;
                }

                fadeInstanceProperty = AccessTools.Property(fadeManagerType, "Instance");
                if (fadeInstanceProperty == null)
                {
                    fadeInstanceProperty = fadeManagerType.BaseType?.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                }

                if (fadeInstanceProperty == null)
                {
                    MelonLogger.Warning("[GameState] FadeManager Instance property not found — fade detection disabled");
                    return;
                }

                isFadeFinishMethod = AccessTools.Method(fadeManagerType, "IsFadeFinish");
                if (isFadeFinishMethod == null)
                {
                    MelonLogger.Warning("[GameState] IsFadeFinish method not found — fade detection disabled");
                    return;
                }

                fadeInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Error initializing fade detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the FadeManager type via assembly scanning.
        /// </summary>
        private static Type FindFadeManagerType()
        {
            string[] typeNames = new[]
            {
                "Il2CppSystem.Fade.FadeManager",
                "System.Fade.FadeManager"
            };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var name in typeNames)
                    {
                        var type = asm.GetType(name);
                        if (type != null)
                            return type;
                    }
                }
                catch { }
            }

            // Broader search: look for any type named FadeManager
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (type.Name == "FadeManager" && !type.IsNested)
                            return type;
                    }
                }
                catch { }
            }

            return null;
        }

        public static void ChangeState_Postfix(SubSceneManagerMainGame.State state)
        {
            try
            {
                int stateValue = (int)state;

                if (stateValue == STATE_FIELD_READY || stateValue == STATE_PLAYER || stateValue == STATE_CHANGE_MAP)
                {
                    CheckMapTransition();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Error in ChangeState_Postfix: {ex.Message}");
            }
        }

        private static void CheckMapTransition()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                    return;

                // Reset battle state when returning to field map
                if (BattleState.IsInBattle)
                {
                    BattleState.Reset();
                }

                int currentMapId = userDataManager.CurrentMapId;
                int lastMapId = AnnouncementDeduplicator.GetLastIndex(AnnouncementContexts.GAME_STATE_MAP_ID);
                bool isFirstRun = (lastMapId == -1);
                bool mapChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.GAME_STATE_MAP_ID, currentMapId);

                if (!isFirstRun && mapChanged)
                {
                    // Map has changed - announce new map
                    string mapName = MapNameResolver.GetCurrentMapName();
                    FFV_ScreenReaderMod.SpeakText($"Entering {mapName}", interrupt: false);

                    // Check if entering interior map - switch to on-foot state
                    bool isWorldMap = FFV_ScreenReaderMod.Instance?.IsCurrentMapWorldMap() ?? false;
                    MoveStateHelper.OnMapTransition(isWorldMap);

                    // Reset vehicle type map before entity rescan
                    FieldNavigationHelper.ResetVehicleTypeMap();

                    // Force entity rescan to clear stale entities from previous map
                    FFV_ScreenReaderMod.Instance?.ForceEntityRescan();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Error in CheckMapTransition: {ex.Message}");
            }
        }
    }
}
