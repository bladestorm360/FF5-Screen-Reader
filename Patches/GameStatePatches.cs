using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Utils;
using SubSceneManagerMainGame = Il2CppLast.Management.SubSceneManagerMainGame;
using GameSceneManager = Il2CppLast.Management.SceneManager;

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
        /// Unconditionally clears battle state without restoring navigation.
        /// </summary>
        public static void ForceReset()
        {
            _isInBattle = false;
            ActiveBattleCharacterTracker.CurrentActiveCharacter = null;
        }

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
    /// Provides game state queries, map transition detection, and ChangeState hook.
    /// Hooks SubSceneManagerMainGame.ChangeState for event-driven map transition detection.
    /// IsInEventState reads directly from the game's state machine (no hook needed).
    /// Fade detection via cached reflection on FadeManager.
    /// </summary>
    public static class GameStatePatches
    {
        // Field states from SubSceneManagerMainGame.State enum
        private const int STATE_CHANGE_MAP = 1;
        private const int STATE_FIELD_READY = 2;
        private const int STATE_PLAYER = 3;
        private const int STATE_EVENT = 12;

        // Cached event state
        private static bool _cachedIsInEvent = false;

        // Fade detection via cached reflection
        private static bool fadeInitialized = false;
        private static PropertyInfo fadeInstanceProperty;
        private static MethodInfo isFadeFinishMethod;

        /// <summary>
        /// Manually patches SubSceneManagerMainGame.ChangeState with a postfix.
        /// Called from FFV_ScreenReaderMod initialization.
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                // 1. ChangeState postfix — state tracking + map transitions
                var changeStateMethod = AccessTools.Method(
                    typeof(SubSceneManagerMainGame),
                    "ChangeState",
                    new Type[] { typeof(SubSceneManagerMainGame.State) }
                );

                if (changeStateMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(GameStatePatches), nameof(ChangeState_Postfix));
                    harmony.Patch(changeStateMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[GameState] Patched SubSceneManagerMainGame.ChangeState");
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
        }

        /// <summary>
        /// Postfix for SubSceneManagerMainGame.ChangeState.
        /// Fires on every state transition — handles map transitions and battle state clearing.
        /// </summary>
        public static void ChangeState_Postfix(SubSceneManagerMainGame.State state)
        {
            try
            {
                int stateValue = (int)state;

                // Track event state for IsInEventState property
                if (stateValue == STATE_EVENT)
                    _cachedIsInEvent = true;
                else if (_cachedIsInEvent)
                    _cachedIsInEvent = false;

                // Field states: check map transitions and clear battle state
                if (stateValue == STATE_FIELD_READY || stateValue == STATE_PLAYER || stateValue == STATE_CHANGE_MAP)
                {
                    if (BattleState.IsInBattle)
                    {
                        BattleState.Reset();
                    }
                    CheckMapTransition();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Error in ChangeState_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// True while the game is in Event state (state 12).
        /// Cached via ChangeState_Postfix — no IL2CPP calls.
        /// </summary>
        public static bool IsInEventState => _cachedIsInEvent;

        /// <summary>
        /// True while the screen is fading (fade not finished).
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

        /// <summary>
        /// Checks if the map has changed and announces the new map name.
        /// Called from ChangeState_Postfix on field state transitions, and from
        /// MovementSpeechPatches.FieldReady_Postfix as a backup.
        /// </summary>
        public static void CheckMapTransition()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager == null) return;

                int currentMapId = userDataManager.CurrentMapId;

                int lastMapId = AnnouncementDeduplicator.GetLastIndex(AnnouncementContexts.GAME_STATE_MAP_ID);
                bool isFirstRun = (lastMapId == -1);
                bool mapChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.GAME_STATE_MAP_ID, currentMapId);

                if (!isFirstRun && mapChanged)
                {
                    string mapName = MapNameResolver.GetCurrentMapName();
                    FFV_ScreenReaderMod.SpeakText($"Entering {mapName}", interrupt: false);

                    bool isWorldMap = GameConstants.IsWorldMap(currentMapId);
                    MoveStateHelper.OnMapTransition(isWorldMap);
                    FieldNavigationHelper.ResetVehicleTypeMap();

                    // Schedule entity scan after a frame to let scene finish loading
                    FFV_ScreenReaderMod.Instance?.ScheduleDeferredEntityScan();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Error in CheckMapTransition: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes cached reflection for FadeManager polling.
        /// </summary>
        public static void InitializeFadeDetection()
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

                fadeInstanceProperty = HarmonyLib.AccessTools.Property(fadeManagerType, "Instance");
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

                isFadeFinishMethod = HarmonyLib.AccessTools.Method(fadeManagerType, "IsFadeFinish");
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

        /// <summary>
        /// Resets internal state.
        /// </summary>
        public static void ResetState()
        {
            _cachedIsInEvent = false;
        }
    }
}
