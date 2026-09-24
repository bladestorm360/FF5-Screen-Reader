using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
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
        /// Called when battle starts. Stores current navigation settings and suppresses them.
        /// </summary>
        public static void SetActive()
        {
            if (_isInBattle) return;

            _isInBattle = true;

            // Scope the battle-message guards to this battle. Both are intra-battle by design —
            // one suppresses per-hit re-invocation for the same BattleActData, the other a
            // dual-wield second swing — but BattleActData is POOLED, so the next battle hands
            // back an object at the same address and the first action is swallowed before a
            // string is ever built. Their per-turn reset (SetCommandSelectTarget) never runs at
            // a battle boundary, and cannot: a preemptive strike or an enemy acting first
            // produces actions before any command window exists.
            //
            // Placed above the mod null-check, which returns early.
            ScrollMessageManager_Play_Patch.ResetLastMessage();
            ParameterActFunctionManagment_CreateActFunction_Patch.ResetLastAction();

            // Same scoping for the two guards that had no battle-boundary reset at all: the
            // condition set was only cleared per turn, so a KO or status carried over from the
            // previous battle's last turn stayed swallowed; the system-message guard was never
            // cleared, so a repeat "The party was defeated" / "Preemptive strike!" in a later
            // battle stayed silent for the whole session.
            BattleConditionController_Add_Patch.ResetLastCondition();
            BattleConditionController_RemoveFunction_Patch.SetBattleOver(false);
            BattleCommandMessagePatches.ResetState();

            // Results from an earlier battle are stale now, and while they exist they hold the
            // input context on BattleResult (see Reset below).
            BattleResultDataStore.Clear();

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

            // Results are normally dropped by ResultMenuController.EndWaitInit. If that state is
            // skipped, HasData would pin InputManager's context on BattleResult — ahead of
            // Field — and every field hotkey would stay dead back on the map.
            BattleResultDataStore.Clear();

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
        private const int STATE_BATTLE = 13;

        // Config menu bestiary states (SubSceneManagerMainGame.State)
        private const int STATE_MENU_LIBRARY_UI = 17;
        private const int STATE_MENU_LIBRARY_INFO = 18;

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
                {
                    _cachedIsInEvent = false;

                    // A tile's foot ID — the integer the routing grid is built from — is not
                    // fixed for the whole game: MiscAssetDesc.MapFootAttribute remaps foot
                    // IDs between story flags, applied through MapModel.SetConversionFootId.
                    // A cutscene that flips such a flag changes what is passable without a
                    // map reload, so drop the cached grid on the way out of every event.
                    // Rebuilding is lazy, so this costs nothing unless the player then routes.
                    FFV_ScreenReader.Field.Routing.VehicleRouteSearcher.InvalidateAll();
                }

                // Scene-load backstop for battle entry. BattleStartPatches normally gets here
                // first, at the encounter itself, but Colosseum and AR battles never route
                // through EventProcedure. SetActive() is idempotent, so whichever fires first
                // wins and the other is a no-op.
                if (stateValue == STATE_BATTLE)
                    BattleState.SetActive();

                // Field states: check map transitions and clear battle state
                if (stateValue == STATE_FIELD_READY || stateValue == STATE_PLAYER || stateValue == STATE_CHANGE_MAP)
                {
                    if (BattleState.IsInBattle)
                    {
                        BattleState.Reset();
                    }

                    // The game is back in field control, and menus live in their own states
                    // (Menu=5, Shop=9, MenuLibraryUi=17...), so nothing can legitimately still be
                    // open here. Drop any menu flag whose Close hook never fired — otherwise one
                    // missed hook leaves MenuStateRegistry.AnyActive() true forever, which pins
                    // the input context off Field and silently kills the entity scanner,
                    // pathfinding, and every field audio cue until the game is restarted.
                    // (Real case: backing out of item-use targeting leaves ITEM_USE latched,
                    // because that path returns to the controller's Non state without a Close.)
                    MenuStateRegistry.ResetAll();

                    CheckMapTransition();
                }

                // Config menu bestiary states
                if (stateValue == STATE_MENU_LIBRARY_UI || stateValue == STATE_MENU_LIBRARY_INFO)
                    ConfigBestiaryStateHandler.HandleStateChange(stateValue);
                else if (ConfigBestiaryStateHandler.WasInConfigBestiary)
                    ConfigBestiaryStateHandler.HandleExit();
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

        // Map id of the last transition handled. -1 means "never ran": the first load after boot is
        // announced but skips the transition side effects below, which the field-ready path covers.
        // NOT just a speech guard: one door transition invokes CheckMapTransition 3-4 times (three
        // ChangeState values plus the FieldReady backup), and the side effects — MoveStateHelper,
        // ResetVehicleTypeMap, ScheduleDeferredEntityScan — must run exactly once per map.
        private static int _lastAnnouncedMapId = -1;

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

                bool isFirstRun = (_lastAnnouncedMapId == -1);
                bool mapChanged = (currentMapId != _lastAnnouncedMapId);
                _lastAnnouncedMapId = currentMapId;

                if (mapChanged)
                {
                    // Recorded so the game's own location banner doesn't repeat it (FadeMessageManager).
                    string announcement = string.Format(T("Entering {0}"), MapNameResolver.GetCurrentMapName());
                    LocationMessageTracker.SetLastMapTransition(announcement);
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }

                if (!isFirstRun && mapChanged)
                {
                    bool isWorldMap = GameConstants.IsWorldMap(currentMapId);
                    MoveStateHelper.OnMapTransition(isWorldMap);
                    FieldNavigationHelper.ResetVehicleTypeMap();

                    // Terrain data is per-map, so the routing caches are stale now.
                    FFV_ScreenReader.Field.Routing.VehicleRouteSearcher.InvalidateAll();

                    // Build the world map's attribute grid here rather than on the first
                    // keypress: this is already a loading screen, so a one-off scan of every
                    // cell costs the player nothing. Skipped for interiors, which use the
                    // game's own searcher and never read the grid.
                    if (isWorldMap)
                        BuildRoutingGrid(currentMapId);

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
        /// Pre-builds the terrain attribute grid the vehicle searcher runs on. Failure is
        /// not fatal — the grid is rebuilt lazily on first use if this could not run yet
        /// (the field controller may not exist at every transition state).
        /// </summary>
        private static void BuildRoutingGrid(int mapId)
        {
            try
            {
                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                var fieldMap = GameObjectCache.Get<Il2Cpp.FieldMap>();
                var fieldController = fieldMap?.fieldController;

                if (fieldController == null || playerController?.mapHandle == null)
                    return;

                FFV_ScreenReader.Field.Routing.VehicleRouteSearcher.EnsureGrid(
                    fieldController, playerController.mapHandle, mapId);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameState] Routing grid pre-build skipped: {ex.Message}");
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
