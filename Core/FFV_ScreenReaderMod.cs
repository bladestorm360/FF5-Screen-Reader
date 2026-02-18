using MelonLoader;
using HarmonyLib;
using FFV_ScreenReader.Utils;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Patches;
using FFV_ScreenReader.Menus;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Management;
using Il2CppLast.Entity.Field;
using Il2CppLast.Message;
using GameCursor = Il2CppLast.UI.Cursor;
using FieldTresureBox = Il2CppLast.Entity.Field.FieldTresureBox;

[assembly: MelonInfo(typeof(FFV_ScreenReader.Core.FFV_ScreenReaderMod), "FFV Screen Reader", "1.0.0", "Your Name")]
[assembly: MelonGame("SQUARE ENIX, Inc.", "FINAL FANTASY V")]

namespace FFV_ScreenReader.Core
{
    public enum EntityCategory
    {
        All = 0,
        Chests = 1,
        NPCs = 2,
        MapExits = 3,
        Events = 4,
        Vehicles = 5,
        Waypoints = 6
    }

    public class FFV_ScreenReaderMod : MelonMod
    {
        private static TolkWrapper tolk;
        private InputManager inputManager;
        private EntityCache entityCache;
        private EntityNavigator entityNavigator;
        private WaypointManager waypointManager;
        private WaypointNavigator waypointNavigator;
        private WaypointController waypointController;

        // Stored delegate for scene load subscription (must be same instance for += / -=)
        private UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,
            UnityEngine.SceneManagement.LoadSceneMode> _sceneLoadedDelegate;

        // Static instance for access from patches
        internal static FFV_ScreenReaderMod Instance { get; private set; }

        private static readonly int CategoryCount = System.Enum.GetValues(typeof(EntityCategory)).Length;

        private bool filterByPathfinding = false;

        private bool filterMapExits = false;

        // ToLayer (layer transition) filter toggle
        private bool filterToLayer = false;

        // Audio loop management delegated to AudioLoopManager
        private AudioLoopManager audioLoopManager;

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("FFV Screen Reader Mod loaded!");

            // Subscribe to scene load events for automatic component caching
            _sceneLoadedDelegate = (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,
                UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += _sceneLoadedDelegate;

            // Initialize preferences
            PreferencesManager.Initialize();

            // Load saved filter preferences
            filterByPathfinding = PreferencesManager.PathfindingFilterDefault;
            filterMapExits = PreferencesManager.MapExitFilterDefault;
            filterToLayer = PreferencesManager.ToLayerFilterDefault;

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();

            // Initialize external sound player for distinct audio feedback
            SoundPlayer.Initialize();

            // Initialize entity name translator (loads UserData/EntityNames.json)
            EntityTranslator.Initialize();

            // Initialize mod menu (F8 settings menu)
            ModMenu.Initialize();

            // Initialize entity cache and navigator (event-driven, no timer)
            entityCache = new EntityCache();

            entityNavigator = new EntityNavigator(entityCache);
            entityNavigator.FilterByPathfinding = filterByPathfinding;
            entityNavigator.FilterMapExits = filterMapExits;
            entityNavigator.FilterToLayer = filterToLayer;

            // Initialize audio loop manager
            audioLoopManager = new AudioLoopManager(entityCache, entityNavigator);
            audioLoopManager.InitializeFromPreferences();

            // Initialize waypoint system
            waypointManager = new WaypointManager();
            waypointNavigator = new WaypointNavigator(waypointManager);
            waypointController = new WaypointController(waypointManager, waypointNavigator);

            // Initialize SDL for input (non-fatal if SDL3.dll missing)
            InputManager.InitializeSDL();

            // Initialize input manager
            inputManager = new InputManager(this);

            // Apply manual Harmony patches for vehicle state, field ready, map transitions, and entity interactions
            var harmony = new HarmonyLib.Harmony("FFV_ScreenReader.ManualPatches");

            MovementSpeechPatches.OnFieldReady = OnFieldReadyCallback;
            MovementSpeechPatches.ApplyPatches(harmony);

            GameStatePatches.ApplyPatches(harmony);

            // Initialize fade detection
            GameStatePatches.InitializeFadeDetection();

            PopupPatches.ApplyPatches(harmony);

            // Patch save/load menu navigation (KEPT for test navigation)
            SaveLoadPatches.ApplyPatches(harmony);

            BattleCommandMessagePatches.ApplyPatches(harmony);
            BattleResultManualPatches.ApplyPatches(harmony);
            NamingPatches.ApplyPatches(harmony);

            TryPatchEntityInteractions(harmony);
        }

        private void UnsubscribeSceneHandler()
        {
            if (_sceneLoadedDelegate != null)
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= _sceneLoadedDelegate;
        }

        public override void OnDeinitializeMelon()
        {
            // Unsubscribe from scene load events
            UnsubscribeSceneHandler();

            // Stop audio loops
            audioLoopManager?.StopAllLoops();

            // Shutdown sound player (closes SDL audio streams, frees unmanaged memory)
            SoundPlayer.Shutdown();

            // Shutdown SDL input
            InputManager.ShutdownSDL();

            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }

        /// <summary>
        /// Called when the field is ready (via MainGame.set_FieldReady hook).
        /// Triggers entity scan so entities are available immediately when user presses navigation keys.
        /// </summary>
        private void OnFieldReadyCallback()
        {
            try
            {
                GameObjectCache.Refresh<Il2CppLast.Map.FieldPlayerController>();
                LoggerInstance.Msg("[FieldReady] Refreshed FieldPlayerController");

                // Skip entity scan if in Event state — when the event ends,
                // set_FieldReady will fire again and trigger the scan.
                if (GameStatePatches.IsInEventState)
                {
                    LoggerInstance.Msg("[FieldReady] In Event state — skipping entity scan");
                    return;
                }

                LoggerInstance.Msg("[FieldReady] Triggering initial entity scan");
                entityCache.ForceScan();
                LoggerInstance.Msg($"[FieldReady] Entity scan complete, found {entityCache.Entities.Count} entities");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[FieldReady] Error during entity scan: {ex.Message}");
            }
        }

        /// <summary>
        /// Schedules an entity refresh after a 1-frame delay.
        /// Called by interaction hooks (treasure chest, dialogue end) to update entity states.
        /// </summary>
        internal void ScheduleEntityRefresh()
        {
            CoroutineManager.StartManaged(EntityRefreshCoroutine());
        }

        private IEnumerator EntityRefreshCoroutine()
        {
            // Wait one frame for game state to fully update
            yield return null;

            // Skip scan if in Event state — entities may be in flux
            if (GameStatePatches.IsInEventState)
            {
                LoggerInstance.Msg("[EntityRefresh] In Event state — skipping entity scan");
                yield break;
            }

            // Rescan entities to pick up state changes (e.g., chest opened)
            entityCache.ForceScan();
            LoggerInstance.Msg("[EntityRefresh] Rescanned entities after interaction");
        }

        /// <summary>
        /// Forces an entity rescan. Called from GameStatePatches on map transitions.
        /// </summary>
        public void ForceEntityRescan()
        {
            entityCache?.ForceScan();
        }

        /// <summary>
        /// Schedules an entity scan for next frame, after scene load settles.
        /// </summary>
        internal void ScheduleDeferredEntityScan()
        {
            CoroutineManager.StartManaged(DeferredEntityScanCoroutine());
        }

        private IEnumerator DeferredEntityScanCoroutine()
        {
            yield return null; // wait one frame for scene to settle
            if (GameStatePatches.IsInEventState)
            {
                LoggerInstance.Msg("[EntityRefresh] In Event state — skipping deferred scan");
                yield break;
            }
            entityCache.ForceScan();
            LoggerInstance.Msg("[EntityRefresh] Deferred entity scan completed");
        }

        /// <summary>
        /// Check if the current map is a world map.
        /// World map IDs in FF5: 0, 1, 2 for different world states.
        /// </summary>
        public bool IsCurrentMapWorldMap()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                {
                    return GameConstants.IsWorldMap(userDataManager.CurrentMapId);
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Error checking world map: {ex.Message}");
            }
            return false;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                LoggerInstance.Msg($"[ComponentCache] Scene loaded: {scene.name}");

                // Stop audio loops during scene transition and suppress briefly
                audioLoopManager?.OnSceneTransition();

                // Reset movement state for new map
                MovementSoundPatches.ResetState();

                // Try to find and cache FieldPlayerController
                var playerController = UnityEngine.Object.FindObjectOfType<Il2CppLast.Map.FieldPlayerController>();
                if (playerController != null)
                {
                    GameObjectCache.Register(playerController);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldPlayerController: {playerController.gameObject?.name}");

                    // Reset battle state when returning to field from battle
                    if (BattleState.IsInBattle)
                    {
                        BattleState.Reset();
                    }
                }
                else
                {
                    LoggerInstance.Msg("[ComponentCache] No FieldPlayerController found in scene");
                }

                // Try to find and cache FieldMap
                var fieldMap = UnityEngine.Object.FindObjectOfType<Il2Cpp.FieldMap>();
                if (fieldMap != null)
                {
                    GameObjectCache.Register(fieldMap);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldMap: {fieldMap.gameObject?.name}");
                }
                else
                {
                    LoggerInstance.Msg("[ComponentCache] No FieldMap found in scene");
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"[ComponentCache] Error in OnSceneLoaded: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            // Silence all mod activity during events (and grace period) —
            // eliminates IL2CPP overhead that can interfere with trigger deactivation.
            // Dialogue reading is unaffected (driven by Harmony hooks, not OnUpdate).
            if (GameStatePatches.IsInEventState) return;

            inputManager.Update();

            if (audioLoopManager != null && audioLoopManager.IsFootstepsEnabled
                && !BattleState.IsInBattle && !DialogueTracker.IsInDialogue)
            {
                var player = GetFieldPlayer();
                if (player?.transform != null)
                    MovementSoundPatches.CheckFootstep(player.transform.localPosition);
            }
        }

        /// <summary>
        /// Shared preamble for entity announcements. Returns false if announcement should be aborted
        /// (already speaks error message to user in that case).
        /// </summary>
        private bool TryGetEntityContext(out Field.NavigableEntity entity, out Field.PathInfo pathInfo, out Il2CppLast.Map.FieldPlayerController playerController)
        {
            entity = null;
            pathInfo = null;
            playerController = null;

            entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText("No entities nearby");
                return false;
            }

            if (entity.GameEntity == null || entity.GameEntity.transform == null)
                return false;

            playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                SpeakText("Not in field");
                return false;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            return true;
        }

        internal void AnnounceCurrentEntity()
        {
            try
            {
                if (!TryGetEntityContext(out var entity, out var pathInfo, out var playerController))
                    return;

                SpeakText(pathInfo.Success ? pathInfo.Description : "no path");
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in AnnounceCurrentEntity: {ex.Message}");
            }
        }

        internal void CycleNext()
        {
            if (entityNavigator.CycleNext())
            {
                AnnounceEntityOnly();
            }
            else
            {
                SpeakText(entityNavigator.EntityCount == 0 ? "No entities nearby" : "No pathable entities found");
            }
        }

        internal void CyclePrevious()
        {
            if (entityNavigator.CyclePrevious())
            {
                AnnounceEntityOnly();
            }
            else
            {
                SpeakText(entityNavigator.EntityCount == 0 ? "No entities nearby" : "No pathable entities found");
            }
        }

        internal void AnnounceEntityOnly()
        {
            try
            {
                if (!TryGetEntityContext(out var entity, out var pathInfo, out var playerController))
                    return;

                Vector3 playerPos = playerController.fieldPlayer.transform.position;
                string formatted = entity.FormatDescription(playerPos);

                // If player is on the entity's tile, replace distance/direction with "here"
                float distance = Vector3.Distance(playerPos, entity.Position);
                if (distance / 16f < 0.1f)
                {
                    int parenEnd = formatted.LastIndexOf(')');
                    int parenStart = parenEnd >= 0 ? formatted.LastIndexOf('(', parenEnd) : -1;
                    if (parenStart >= 0 && parenEnd > parenStart)
                    {
                        formatted = formatted.Substring(0, parenStart + 1) + "here" + formatted.Substring(parenEnd);
                    }
                }

                string countSuffix = $", {entityNavigator.CurrentIndex + 1} of {entityNavigator.EntityCount}";
                string announcement = pathInfo.Success ? $"{formatted}{countSuffix}" : $"{formatted}, no path{countSuffix}";
                SpeakText(announcement);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in AnnounceEntityOnly: {ex.Message}");
            }
        }

        internal void CycleNextCategory()
        {
            int nextCategory = ((int)entityNavigator.Category + 1) % CategoryCount;

            // Skip Waypoints category - it has dedicated hotkeys (comma, period, slash)
            if ((EntityCategory)nextCategory == EntityCategory.Waypoints)
                nextCategory = (nextCategory + 1) % CategoryCount;

            EntityCategory newCategory = (EntityCategory)nextCategory;

            entityNavigator.SetCategory(newCategory);

            AnnounceCategoryChange();
        }

        internal void CyclePreviousCategory()
        {
            int prevCategory = (int)entityNavigator.Category - 1;
            if (prevCategory < 0)
                prevCategory = CategoryCount - 1;

            // Skip Waypoints category - it has dedicated hotkeys (comma, period, slash)
            if ((EntityCategory)prevCategory == EntityCategory.Waypoints)
            {
                prevCategory--;
                if (prevCategory < 0)
                    prevCategory = CategoryCount - 1;
            }

            EntityCategory newCategory = (EntityCategory)prevCategory;

            entityNavigator.SetCategory(newCategory);

            AnnounceCategoryChange();
        }

        internal void TogglePathfindingFilter()
        {
            filterByPathfinding = !filterByPathfinding;

            entityNavigator.FilterByPathfinding = filterByPathfinding;

            PreferencesManager.SavePathfindingFilter(filterByPathfinding);

            string status = filterByPathfinding ? "on" : "off";
            SpeakText($"Pathfinding filter {status}");
        }

        internal void ToggleMapExitFilter()
        {
            filterMapExits = !filterMapExits;

            entityNavigator.FilterMapExits = filterMapExits;
            entityNavigator.RebuildNavigationList();

            PreferencesManager.SaveMapExitFilter(filterMapExits);

            string status = filterMapExits ? "on" : "off";
            SpeakText($"Map exit filter {status}");
        }

        internal void ToggleToLayerFilter()
        {
            filterToLayer = !filterToLayer;

            entityNavigator.FilterToLayer = filterToLayer;

            PreferencesManager.SaveToLayerFilter(filterToLayer);

            string status = filterToLayer ? "on" : "off";
            SpeakText($"Layer transition filter {status}");
        }

        private void AnnounceCategoryChange()
        {
            string categoryName = EntityNavigator.GetCategoryName(entityNavigator.Category);
            int entityCount = entityNavigator.EntityCount;

            string announcement = $"Category: {categoryName}, {entityCount} {(entityCount == 1 ? "entity" : "entities")}";
            SpeakText(announcement);
        }

        internal void TeleportInDirection(Vector2 offset)
        {
            var entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText("No entity selected");
                return;
            }

            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Player not available");
                return;
            }

            var player = playerController.fieldPlayer;

            Vector3 targetPos = entity.Position;
            Vector3 newPos = new Vector3(targetPos.x + offset.x, targetPos.y + offset.y, targetPos.z);

            player.transform.localPosition = newPos;

            string direction = GetDirectionName(offset);
            string name = (entity is MapExitEntity || entity is TreasureChestEntity || entity is GroupEntity)
                ? entity.DisplayName : entity.Name;
            SpeakText($"Teleported {direction} of {name}");
            LoggerInstance.Msg($"Teleported {direction} of {name} to position {newPos}");
        }

        private string GetDirectionName(Vector2 offset)
        {
            if (offset.y > 0) return "north";
            if (offset.y < 0) return "south";
            if (offset.x < 0) return "west";
            if (offset.x > 0) return "east";
            return "unknown";
        }

        internal void AnnounceGilAmount()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager == null)
                {
                    SpeakText("Not on map");
                    return;
                }

                int gil = userDataManager.OwendGil;
                SpeakText($"{gil:N0} gil");
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing gil amount: {ex.Message}");
                SpeakText("Error reading gil amount");
            }
        }

        internal void AnnounceCurrentMap()
        {
            try
            {
                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController?.fieldPlayer == null)
                {
                    SpeakText("Not on map");
                    return;
                }
                string mapName = FFV_ScreenReader.Field.MapNameResolver.GetCurrentMapName();
                SpeakText(mapName);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing current map: {ex.Message}");
                SpeakText("Error reading map name");
            }
        }

        internal void AnnounceActiveCharacterStatus()
        {
            try
            {
                // Get the currently active character from the battle patch
                var activeCharacter = FFV_ScreenReader.Patches.ActiveBattleCharacterTracker.CurrentActiveCharacter;

                if (activeCharacter == null)
                {
                    SpeakText("Unavailable outside of battle");
                    return;
                }

                string characterName = activeCharacter.Name;

                // Read HP/MP directly from character parameter
                if (activeCharacter.Parameter == null)
                {
                    SpeakText($"{characterName}, status information not available");
                    return;
                }

                string statusMessage = characterName + CharacterStatusHelper.GetFullStatus(activeCharacter.Parameter);
                SpeakText(statusMessage);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing character status: {ex.Message}");
                SpeakText("Error reading character status");
            }
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// Thread-safe: TolkWrapper uses locking to prevent concurrent native calls.
        /// </summary>
        public static void SpeakText(string text, bool interrupt = true)
        {
            tolk?.Speak(text, interrupt);
        }

        /// <summary>
        /// Speaks text after a delay to avoid window focus announcements interrupting.
        /// </summary>
        public static void SpeakTextDelayed(string text, float delay = 0.3f)
        {
            CoroutineManager.StartManaged(DelayedSpeech(text, delay));
        }

        private static IEnumerator DelayedSpeech(string text, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Instance != null)
                SpeakText(text, interrupt: true);
        }

        // Audio toggle and suppression operations delegated to AudioLoopManager
        internal void ToggleWallTones() => audioLoopManager?.ToggleWallTones();
        internal void ToggleFootsteps() => audioLoopManager?.ToggleFootsteps();
        internal void ToggleAudioBeacons() => audioLoopManager?.ToggleAudioBeacons();
        internal void ToggleLandingPings() => audioLoopManager?.ToggleLandingPings();

        internal void SuppressNavigationForBattle() => audioLoopManager?.SuppressNavigationForBattle();
        internal void RestoreNavigationAfterBattle(bool wallTones, bool footsteps, bool audioBeacons, bool pathfindingFilter, bool landingPings = false)
        {
            audioLoopManager?.RestoreNavigationAfterBattle(wallTones, footsteps, audioBeacons, pathfindingFilter, landingPings);
            filterByPathfinding = pathfindingFilter;
            if (entityNavigator != null) entityNavigator.FilterByPathfinding = pathfindingFilter;
        }
        internal void SuppressNavigationForDialogue() => audioLoopManager?.SuppressNavigationForDialogue();
        internal void RestoreNavigationAfterDialogue() => audioLoopManager?.RestoreNavigationAfterDialogue();

        public static void SuppressWallTonesForTransition() => AudioLoopManager.SuppressWallTonesForTransition();

        // Public static accessors delegated to PreferencesManager
        public static int WallBumpVolume => PreferencesManager.WallBumpVolume;
        public static int FootstepVolume => PreferencesManager.FootstepVolume;
        public static int WallToneVolume => PreferencesManager.WallToneVolume;
        public static int BeaconVolume => PreferencesManager.BeaconVolume;
        public static int LandingPingVolume => PreferencesManager.LandingPingVolume;
        public static int ExpCounterVolume => PreferencesManager.ExpCounterVolume;
        public static int EnemyHPDisplay => PreferencesManager.EnemyHPDisplay;

        // Public static accessors for filter and audio toggle settings (used by ModMenu, BattleState)
        public static bool PathfindingFilterEnabled => Instance?.filterByPathfinding ?? false;
        public static bool MapExitFilterEnabled => Instance?.filterMapExits ?? false;
        public static bool ToLayerFilterEnabled => Instance?.filterToLayer ?? false;
        public static bool WallTonesEnabled => AudioLoopManager.Instance?.IsWallTonesEnabled ?? false;
        public static bool FootstepsEnabled => AudioLoopManager.Instance?.IsFootstepsEnabled ?? false;
        public static bool AudioBeaconsEnabled => AudioLoopManager.Instance?.IsAudioBeaconsEnabled ?? false;
        public static bool LandingPingsEnabled => AudioLoopManager.Instance?.IsLandingPingsEnabled ?? false;
        public static bool ExpCounterEnabled => PreferencesManager.ExpCounterDefault;

        // Public static setters delegated to PreferencesManager
        public static void SetWallBumpVolume(int value) => PreferencesManager.SetWallBumpVolume(value);
        public static void SetFootstepVolume(int value) => PreferencesManager.SetFootstepVolume(value);
        public static void SetWallToneVolume(int value) => PreferencesManager.SetWallToneVolume(value);
        public static void SetBeaconVolume(int value) => PreferencesManager.SetBeaconVolume(value);
        public static void SetLandingPingVolume(int value) => PreferencesManager.SetLandingPingVolume(value);
        public static void SetExpCounterVolume(int value) => PreferencesManager.SetExpCounterVolume(value);
        public static void SetEnemyHPDisplay(int value) => PreferencesManager.SetEnemyHPDisplay(value);

        public static void ToggleExpCounter()
        {
            bool newValue = !ExpCounterEnabled;
            PreferencesManager.SaveExpCounter(newValue);
        }

        /// <summary>
        /// Patches entity interaction methods for immediate entity refresh.
        /// Triggers rescan when treasure chests are opened or dialogue ends.
        /// </summary>
        private void TryPatchEntityInteractions(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch FieldTresureBox.Open() - triggers entity refresh when chest is opened
                Type treasureBoxType = typeof(FieldTresureBox);
                var openMethod = treasureBoxType.GetMethod("Open", BindingFlags.Public | BindingFlags.Instance);
                var openPostfix = typeof(EntityInteractionPatches).GetMethod("TreasureBox_Open_Postfix", BindingFlags.Public | BindingFlags.Static);

                if (openMethod != null && openPostfix != null)
                {
                    harmony.Patch(openMethod, postfix: new HarmonyMethod(openPostfix));
                    LoggerInstance.Msg("Patched FieldTresureBox.Open for entity refresh");
                }
                else
                {
                    LoggerInstance.Warning($"FieldTresureBox.Open patch failed. Method: {openMethod != null}, Postfix: {openPostfix != null}");
                }

                // Patch MessageWindowManager.Close() - triggers entity refresh when dialogue ends
                Type messageManagerType = typeof(MessageWindowManager);
                var closeMethod = messageManagerType.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance);
                var closePostfix = typeof(EntityInteractionPatches).GetMethod("MessageWindow_Close_Postfix", BindingFlags.Public | BindingFlags.Static);

                if (closeMethod != null && closePostfix != null)
                {
                    harmony.Patch(closeMethod, postfix: new HarmonyMethod(closePostfix));
                    LoggerInstance.Msg("Patched MessageWindowManager.Close for entity refresh");
                }
                else
                {
                    LoggerInstance.Warning($"MessageWindowManager.Close patch failed. Method: {closeMethod != null}, Postfix: {closePostfix != null}");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error patching entity interactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the FieldPlayer from the FieldPlayerController.
        /// </summary>
        private FieldPlayer GetFieldPlayer()
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                if (playerController?.fieldPlayer != null)
                    return playerController.fieldPlayer;

                playerController = GameObjectCache.Refresh<FieldPlayerController>();
                return playerController?.fieldPlayer;
            }
            catch
            {
                return null;
            }
        }

        // Waypoint operations delegated to WaypointController
        internal void CycleNextWaypoint() => waypointController.CycleNextWaypoint();
        internal void CyclePreviousWaypoint() => waypointController.CyclePreviousWaypoint();
        internal void CycleNextWaypointCategory() => waypointController.CycleNextWaypointCategory();
        internal void CyclePreviousWaypointCategory() => waypointController.CyclePreviousWaypointCategory();
        internal void PathfindToCurrentWaypoint() => waypointController.PathfindToCurrentWaypoint();
        internal void AddNewWaypointWithNaming() => waypointController.AddNewWaypointWithNaming();
        internal void AddNewWaypoint() => waypointController.AddNewWaypoint();
        internal void RenameCurrentWaypoint() => waypointController.RenameCurrentWaypoint();
        internal void RemoveCurrentWaypoint() => waypointController.RemoveCurrentWaypoint();
        internal void ClearAllWaypointsForMap() => waypointController.ClearAllWaypointsForMap();
    }

    /// <summary>
    /// Postfix patches for entity interaction hooks.
    /// Triggers entity refresh when treasure chests are opened or dialogue ends.
    /// </summary>
    public static class EntityInteractionPatches
    {
        public static void TreasureBox_Open_Postfix()
        {
            FFV_ScreenReaderMod.Instance?.ScheduleEntityRefresh();
        }

        public static void MessageWindow_Close_Postfix()
        {
            if (!GameStatePatches.IsInEventState)
                FFV_ScreenReaderMod.Instance?.ScheduleEntityRefresh();

            // Reset dialogue tracker (clears page data + restores navigation)
            FFV_ScreenReader.Patches.DialogueTracker.Reset();
        }
    }
}
