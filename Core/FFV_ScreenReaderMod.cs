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
using static FFV_ScreenReader.Utils.ModTextTranslator;

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

            // Initialize mod text translator (loads embedded mod_text.json)
            ModTextTranslator.Initialize();

            // Subscribe to scene load events for automatic component caching
            _sceneLoadedDelegate = (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,
                UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += _sceneLoadedDelegate;

            // Initialize preferences
            PreferencesManager.Initialize();

            // Load saved filter preferences
            filterByPathfinding = PreferencesManager.PathfindingFilterEnabled;
            filterMapExits = PreferencesManager.MapExitFilterEnabled;
            filterToLayer = PreferencesManager.ToLayerFilterEnabled;

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();

            // Initialize external sound player for distinct audio feedback
            SoundPlayer.Initialize();

            // Initialize SDL3 gamepad + keyboard polling. Must come after SoundPlayer so
            // the [GamepadManager] log line sits next to other audio init logs.
            GamepadManager.Initialize();

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

            // Wire waypoint navigator into the audio beacon so the beacon can target waypoints.
            audioLoopManager.SetWaypointNavigator(waypointNavigator);

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
            BattleCommandStatePatches.ApplyPatches(harmony);
            BattleStartPatches.ApplyPatches(harmony);
            NamingPatches.ApplyPatches(harmony);

            // Config menu: title-screen Language dropdown focus + keyboard/gamepad remap assign-flow.
            ConfigMenuPatches.ApplyPatches(harmony);
            // Config list focus events (replace the game's per-frame SetFocus re-assertion).
            ConfigFocusEntryPatches.ApplyPatches(harmony);

            // The game's own F1 walk/run and F3 encounter toggles, from any input source.
            GameTogglePatches.ApplyPatches(harmony);

            TryPatchEntityInteractions(harmony);

            // SDL controller passthrough — postfix InputSystemManager.GetKeyDown/GetKey/...
            InputPassthroughPatches.ApplyPatches(harmony);

            // Initial-focus announcements: read the already-focused row on menu open / return.
            // Targets are private state-entry *Init methods, so they need manual patching.
            FieldItemReannouncePatches.ApplyPatches(harmony);
            FieldEquipReannouncePatches.ApplyPatches(harmony);
            FieldJobAbilityReannouncePatches.ApplyPatches(harmony);
            FieldStatusReannouncePatches.ApplyPatches(harmony);
            TitleListFocusPatches.ApplyPatches(harmony);
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

            // Shutdown SDL3 (closes gamepad handle, releases SDL).
            GamepadManager.Shutdown();

            // Shutdown sound player (destroys SDL audio streams + device, frees scratch buffer)
            SoundPlayer.Shutdown();

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
        /// Forces an entity rescan. Called from GameStatePatches on map transitions.
        /// </summary>
        public void ForceEntityRescan()
        {
            entityCache?.ForceScan();
        }

        /// <summary>Backtick: the player-requested rescan, with spoken confirmation.</summary>
        internal void ManualEntityRescan()
        {
            ForceEntityRescan();
            SpeakText(T("Entity scan complete"));
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
            // Input runs in every game state, including events/cutscenes, so mod mode and
            // the repeat key stay reachable while an NPC is talking. Field-only work is kept
            // out of events by InputManager.DetermineContext(), which forces KeyContext.Global
            // there — so entity/waypoint scans never fire mid-cutscene.
            inputManager.Update();

            // Footsteps use the shared audio gate (battle, event, dialogue, menus, overlays,
            // fades) — the event gating that used to come from an early return above.
            if (audioLoopManager != null && audioLoopManager.IsFootstepsEnabled
                && !AudioLoopManager.IsAudioSuppressed)
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

            // Delta scan first so chest/NPC state changes surface before we read CurrentEntity.
            entityNavigator.RefreshIfNeeded();

            entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText(T("No entities nearby"));
                return false;
            }

            if (entity.GameEntity == null || entity.GameEntity.transform == null)
                return false;

            playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                SpeakText(T("Not in field"));
                return false;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            // Explicit player action — allowed to chain past the game's search window.
            pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer,
                PathSearchMode.Full
            );

            return true;
        }

        internal void AnnounceCurrentEntity()
        {
            try
            {
                if (!TryGetEntityContext(out var entity, out var pathInfo, out var playerController))
                    return;

                NavigationTargetTracker.MarkEntity();
                SpeakText(pathInfo.Success ? pathInfo.Description : T("no path"));
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in AnnounceCurrentEntity: {ex.Message}");
            }
        }

        internal void CycleNext()
        {
            entityNavigator.RefreshIfNeeded();
            if (entityNavigator.CycleNext())
            {
                NavigationTargetTracker.MarkEntity();
                AnnounceEntityOnly();
            }
            else
            {
                SpeakText(entityNavigator.EntityCount == 0 ? T("No entities nearby") : T("No pathable entities found"));
            }
        }

        internal void CyclePrevious()
        {
            entityNavigator.RefreshIfNeeded();
            if (entityNavigator.CyclePrevious())
            {
                NavigationTargetTracker.MarkEntity();
                AnnounceEntityOnly();
            }
            else
            {
                SpeakText(entityNavigator.EntityCount == 0 ? T("No entities nearby") : T("No pathable entities found"));
            }
        }

        /// <summary>
        /// Backslash / P (and controller LT): re-target the beacon when beacons are on, otherwise
        /// speak the path to the current entity.
        /// </summary>
        internal void AnnounceOrRestartBeacon()
        {
            NavigationTargetTracker.MarkEntity();
            if (PreferencesManager.AudioBeaconsEnabled) RestartEntityBeacon();
            else AnnounceCurrentEntity();
        }

        internal void AnnounceEntityOnly()
        {
            try
            {
                string announcement = FormatCurrentEntity();
                if (announcement != null)
                    SpeakText(announcement);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in AnnounceEntityOnly: {ex.Message}");
            }
        }

        /// <summary>
        /// "Name (distance direction), N of M" for the selected entity — the cycling announcement.
        /// Returns null when there is nothing to announce (TryGetEntityContext has spoken why).
        /// </summary>
        private string FormatCurrentEntity()
        {
            if (!TryGetEntityContext(out var entity, out var pathInfo, out var playerController))
                return null;

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
                    formatted = formatted.Substring(0, parenStart + 1) + T("here") + formatted.Substring(parenEnd);
                }
            }

            // Count what the player can actually cycle to, not what exists. With the
            // pathfinding filter on, a 20-entity map with 4 reachable reads "1 of 4" and
            // the index tracks the cycling order. With no cycle-time filter enabled these
            // are the plain list values, so the unfiltered wording is unchanged.
            string countSuffix = $", {entityNavigator.FilteredIndex + 1} {T("of")} {entityNavigator.FilteredCount}";

            return pathInfo.Success ? $"{formatted}{countSuffix}" : $"{formatted}, {T("no path")}{countSuffix}";
        }

        internal void CycleNextCategory()
        {
            int nextCategory = ((int)entityNavigator.Category + 1) % CategoryCount;

            // Skip Waypoints category - it has dedicated hotkeys (comma, period, slash)
            if ((EntityCategory)nextCategory == EntityCategory.Waypoints)
                nextCategory = (nextCategory + 1) % CategoryCount;

            EntityCategory newCategory = (EntityCategory)nextCategory;

            entityNavigator.SetCategory(newCategory);

            NavigationTargetTracker.MarkEntity();
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

            NavigationTargetTracker.MarkEntity();
            AnnounceCategoryChange();
        }

        internal void TogglePathfindingFilter()
        {
            filterByPathfinding = !filterByPathfinding;

            entityNavigator.FilterByPathfinding = filterByPathfinding;

            PreferencesManager.SavePathfindingFilter(filterByPathfinding);

            string status = filterByPathfinding ? T("on") : T("off");
            SpeakText(string.Format(T("Pathfinding filter {0}"), status));
        }

        internal void ToggleMapExitFilter()
        {
            filterMapExits = !filterMapExits;

            entityNavigator.FilterMapExits = filterMapExits;
            entityNavigator.RebuildNavigationList();

            PreferencesManager.SaveMapExitFilter(filterMapExits);

            string status = filterMapExits ? T("on") : T("off");
            SpeakText(string.Format(T("Map exit filter {0}"), status));
        }

        internal void ToggleToLayerFilter()
        {
            filterToLayer = !filterToLayer;

            entityNavigator.FilterToLayer = filterToLayer;

            PreferencesManager.SaveToLayerFilter(filterToLayer);

            string status = filterToLayer ? T("on") : T("off");
            SpeakText(string.Format(T("Layer transition filter {0}"), status));
        }

        /// <summary>
        /// Shift+K: jump back to the All category.
        /// </summary>
        internal void ResetToAllCategory()
        {
            if (entityNavigator.Category == EntityCategory.All)
            {
                SpeakText(T("Already in All category"));
                return;
            }

            entityNavigator.SetCategory(EntityCategory.All);
            NavigationTargetTracker.MarkEntity();
            AnnounceCategoryChange();
        }

        /// <summary>
        /// "Category: X, first entity" — lands on the nearest entity the player can cycle to and
        /// announces it exactly as cycling would, with the same filtered "N of M". A category with
        /// nothing reachable announces its name alone.
        /// </summary>
        private void AnnounceCategoryChange()
        {
            string categoryText = string.Format(T("Category: {0}"), EntityNavigator.GetCategoryName(entityNavigator.Category));

            try
            {
                if (entityNavigator.SelectFirst())
                {
                    string entityText = FormatCurrentEntity();
                    if (entityText != null)
                    {
                        SpeakText($"{categoryText}, {entityText}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Error announcing category entity: {ex.Message}");
            }

            SpeakText(categoryText);
        }

        internal void TeleportInDirection(Vector2 offset)
        {
            try
            {
                var entity = entityNavigator.CurrentEntity;
                if (entity == null)
                {
                    SpeakText(T("No entity selected"));
                    return;
                }

                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController?.fieldPlayer == null)
                {
                    SpeakText(T("Player not available"));
                    return;
                }

                var player = playerController.fieldPlayer;

                Vector3 targetPos = entity.Position;
                Vector3 newPos = new Vector3(targetPos.x + offset.x, targetPos.y + offset.y, targetPos.z);

                player.transform.localPosition = newPos;

                string direction = GetDirectionName(offset);
                string name = (entity is MapExitEntity || entity is TreasureChestEntity || entity is GroupEntity)
                    ? entity.DisplayName : entity.Name;
                SpeakText(string.Format(T("Teleported {0} of {1}"), direction, name));
                LoggerInstance.Msg($"Teleported {direction} of {name} to position {newPos}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Error teleporting: {ex.Message}");
                SpeakText(T("Teleport failed"));
            }
        }

        private string GetDirectionName(Vector2 offset)
        {
            if (offset.y > 0) return T("north");
            if (offset.y < 0) return T("south");
            if (offset.x < 0) return T("west");
            if (offset.x > 0) return T("east");
            return T("unknown");
        }

        internal void AnnounceGilAmount()
        {
            try
            {
                // Gil is a field-and-menus reading. UserDataManager exists on the title
                // screen but holds no loaded save, so without this gate the key reads a
                // stale "0 gil" there — which is what a false input edge announced at boot.
                // Field menus keep the field scene loaded, so IsOnValidMap stays true in them.
                if (!InputManager.IsOnValidMap())
                {
                    SpeakText(T("Not on map"));
                    return;
                }

                if (ControllerRouter.IsInBattle)
                {
                    SpeakText(T("Unavailable in battle"));
                    return;
                }

                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager == null)
                {
                    SpeakText(T("Not on map"));
                    return;
                }

                int gil = userDataManager.OwendGil;
                SpeakText(string.Format(T("{0} gil"), gil.ToString("N0")));
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing gil amount: {ex.Message}");
                SpeakText(T("Error reading gil amount"));
            }
        }

        internal void AnnounceCurrentMap()
        {
            try
            {
                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController?.fieldPlayer == null)
                {
                    SpeakText(T("Not on map"));
                    return;
                }
                string mapName = FFV_ScreenReader.Field.MapNameResolver.GetCurrentMapName();
                SpeakText(mapName);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing current map: {ex.Message}");
                SpeakText(T("Error reading map name"));
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
                    SpeakText(T("Unavailable outside of battle"));
                    return;
                }

                string characterName = activeCharacter.Name;

                // Read HP/MP directly from character parameter
                if (activeCharacter.Parameter == null)
                {
                    SpeakText(string.Format(T("{0}, status information not available"), characterName));
                    return;
                }

                string statusMessage = characterName + CharacterStatusHelper.GetFullStatus(activeCharacter.Parameter);
                SpeakText(statusMessage);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing character status: {ex.Message}");
                SpeakText(T("Error reading character status"));
            }
        }

        /// <summary>
        /// Speak text through the screen reader. Rich-text tags (color, size, icon markup) are
        /// stripped centrally so no reader can leak "&lt;color=...&gt;" into speech.
        /// Thread-safe: TolkWrapper uses locking to prevent concurrent native calls.
        /// </summary>
        public static void SpeakText(string text, bool interrupt = true)
        {
            tolk?.Speak(TextUtils.StripRichTextTags(text), interrupt);
        }

        /// <summary>
        /// Speaks a mod dialog's result queued behind the dialog's own echo ("Yes", "Confirmed:
        /// X"), so both are heard. Replaces a 0.3 s WaitForSeconds that dated from the real-window
        /// dialogs and their NVDA focus announcement (Rule 3: no timers).
        /// </summary>
        public static void SpeakTextQueued(string text)
        {
            SpeakText(text, interrupt: false);
        }

        // Audio toggle and suppression operations delegated to AudioLoopManager
        internal void ToggleWallTones() => audioLoopManager?.ToggleWallTones();
        internal void ToggleFootsteps() => audioLoopManager?.ToggleFootsteps();
        internal void ToggleAudioBeacons() => audioLoopManager?.ToggleAudioBeacons();
        internal void ToggleLandingPings() => audioLoopManager?.ToggleLandingPings();

        internal void SuppressNavigationForBattle() => audioLoopManager?.SuppressNavigationForBattle();
        internal void RestoreNavigationAfterBattle(bool pathfindingFilter)
        {
            audioLoopManager?.RestoreNavigationAfterBattle(pathfindingFilter);
            filterByPathfinding = pathfindingFilter;
            if (entityNavigator != null) entityNavigator.FilterByPathfinding = pathfindingFilter;
        }
        internal void SuppressNavigationForDialogue() => audioLoopManager?.SuppressNavigationForDialogue();
        internal void RestoreNavigationAfterDialogue() => audioLoopManager?.RestoreNavigationAfterDialogue();

        public static void SuppressWallTonesForTransition() => AudioLoopManager.SuppressWallTonesForTransition();

        // Filter/audio toggle accessors (used by ModMenu, ControllerRouter, MovementSoundPatches,
        // WaypointController). Filter state is runtime (mirrored to prefs on toggle); audio toggles
        // and volumes/HP read directly from PreferencesManager — the single source of truth.
        public static bool PathfindingFilterEnabled => Instance?.filterByPathfinding ?? false;
        public static bool MapExitFilterEnabled => Instance?.filterMapExits ?? false;
        public static bool ToLayerFilterEnabled => Instance?.filterToLayer ?? false;
        public static bool WallTonesEnabled => PreferencesManager.WallTonesEnabled;
        public static bool FootstepsEnabled => PreferencesManager.FootstepsEnabled;
        public static bool AudioBeaconsEnabled => PreferencesManager.AudioBeaconsEnabled;
        public static bool LandingPingsEnabled => PreferencesManager.LandingPingsEnabled;
        public static bool ExpCounterEnabled => PreferencesManager.ExpCounterEnabled;
        public static bool StickClickNormalizationEnabled => PreferencesManager.StickClickNormalizationEnabled;
        public static bool EnemyLettersEnabled => PreferencesManager.EnemyLettersEnabled;

        public static void ToggleExpCounter()
        {
            bool newValue = !ExpCounterEnabled;
            PreferencesManager.SaveExpCounter(newValue);
        }

        public static void ToggleAnnounceOnBeaconRestart()
        {
            bool newValue = !PreferencesManager.AnnounceOnBeaconRestartEnabled;
            PreferencesManager.SaveAnnounceOnBeaconRestart(newValue);
        }

        public static void ToggleMenuPositionAnnouncements()
        {
            bool newValue = !PreferencesManager.MenuPositionAnnouncementsEnabled;
            PreferencesManager.SaveMenuPositionAnnouncements(newValue);
        }

        public static void ToggleAutoDetail()
        {
            bool newValue = !PreferencesManager.AutoDetailEnabled;
            PreferencesManager.SaveAutoDetail(newValue);
            SpeakText(newValue ? T("Auto Detail on") : T("Auto Detail off"));
        }

        public static void ToggleStickClickNormalization()
        {
            bool newValue = !StickClickNormalizationEnabled;
            PreferencesManager.SaveStickClickNormalization(newValue);
            SpeakText(newValue ? T("Stick Click Normalization on") : T("Stick Click Normalization off"));
        }

        public static void ToggleEnemyLetters()
        {
            bool newValue = !EnemyLettersEnabled;
            PreferencesManager.SaveEnemyLetters(newValue);
            SpeakText(newValue ? T("Enemy Letters on") : T("Enemy Letters off"));
        }

        /// <summary>
        /// Silences current screen-reader output. Used by controller routing to
        /// stop ongoing announcements when the player presses a face button.
        /// </summary>
        public static void InterruptSpeech()
        {
            tolk?.Silence();
        }

        /// <summary>
        /// Forces the audio beacon to ping next loop iteration. Called by pathfinding
        /// commands so the player gets immediate feedback after pressing the pathfind key.
        /// </summary>
        internal void RestartBeacon()
        {
            audioLoopManager?.RestartBeacon();
        }

        /// <summary>
        /// Re-target the beacon to the current entity. When the "Beacon Destination
        /// Announcement" toggle is on, also re-speaks the current destination (same as
        /// announcing the selected entity).
        /// </summary>
        internal void RestartEntityBeacon()
        {
            RestartBeacon();
            if (PreferencesManager.AnnounceOnBeaconRestartEnabled)
                AnnounceCurrentEntity();
        }

        /// <summary>
        /// Patches entity interaction methods for immediate entity refresh.
        /// Triggers rescan when treasure chests are opened or dialogue ends.
        /// </summary>
        private void TryPatchEntityInteractions(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch MessageWindowManager.Close() - resets DialogueTracker. Entity state
                // changes are now picked up by the delta scan on the next cycle.
                Type messageManagerType = typeof(MessageWindowManager);
                var closeMethod = messageManagerType.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance);
                var closePostfix = typeof(EntityInteractionPatches).GetMethod("MessageWindow_Close_Postfix", BindingFlags.Public | BindingFlags.Static);

                if (closeMethod != null && closePostfix != null)
                {
                    harmony.Patch(closeMethod, postfix: new HarmonyMethod(closePostfix));
                    LoggerInstance.Msg("Patched MessageWindowManager.Close for dialogue tracker reset");
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
    /// Postfix patch for dialogue close. Entity state updates (chest opened, NPC
    /// despawned, NPC spawned by event) are now handled by the delta scan on the next
    /// navigation input — no eager refresh needed here.
    /// </summary>
    public static class EntityInteractionPatches
    {
        public static void MessageWindow_Close_Postfix()
        {
            // Reset dialogue tracker (clears page data + restores navigation)
            FFV_ScreenReader.Patches.DialogueTracker.Reset();
        }
    }
}
