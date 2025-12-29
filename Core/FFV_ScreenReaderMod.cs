using MelonLoader;
using FFV_ScreenReader.Utils;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Menus;
using UnityEngine;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Management;
using Il2CppSystem.Collections.Generic;
using Il2CppLast.Entity.Field;
using GameCursor = Il2CppLast.UI.Cursor;

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

        private const float ENTITY_SCAN_INTERVAL = 5f;
        
        private static readonly int CategoryCount = System.Enum.GetValues(typeof(EntityCategory)).Length;
        
        private bool filterByPathfinding = false;
        
        private bool filterMapExits = false;

        private int lastAnnouncedMapId = -1;

        private bool isOnWorldMap = false;
        
        private static MelonPreferences_Category prefsCategory;
        private static MelonPreferences_Entry<bool> prefPathfindingFilter;
        private static MelonPreferences_Entry<bool> prefMapExitFilter;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFV Screen Reader Mod loaded!");

            // Subscribe to scene load events for automatic component caching
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;

            // Initialize preferences
            prefsCategory = MelonPreferences.CreateCategory("FFV_ScreenReader");
            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");
            prefMapExitFilter = prefsCategory.CreateEntry<bool>("MapExitFilter", false, "Map Exit Filter", "Filter multiple map exits to the same destination, showing only the closest one");

            // Load saved preferences
            filterByPathfinding = prefPathfindingFilter.Value;
            filterMapExits = prefMapExitFilter.Value;

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();

            // Initialize entity cache and navigator
            entityCache = new EntityCache(ENTITY_SCAN_INTERVAL);

            entityNavigator = new EntityNavigator(entityCache);
            entityNavigator.FilterByPathfinding = filterByPathfinding;
            entityNavigator.FilterMapExits = filterMapExits;

            // Initialize waypoint system
            waypointManager = new WaypointManager();
            waypointNavigator = new WaypointNavigator(waypointManager);

            // Initialize input manager
            inputManager = new InputManager(this);
        }

        public override void OnDeinitializeMelon()
        {
            // Unsubscribe from scene load events
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;

            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }

        /// <summary>
        /// Called when a new scene is loaded.
        /// Automatically caches commonly-used Unity components to avoid expensive FindObjectOfType calls.
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                LoggerInstance.Msg($"[ComponentCache] Scene loaded: {scene.name}");

                // Try to find and cache FieldPlayerController
                var playerController = UnityEngine.Object.FindObjectOfType<Il2CppLast.Map.FieldPlayerController>();
                if (playerController != null)
                {
                    GameObjectCache.Register(playerController);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldPlayerController: {playerController.gameObject?.name}");
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

                    // Delay entity scan to allow scene to fully initialize
                    CoroutineManager.StartManaged(DelayedInitialScan());
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

        /// <summary>
        /// Coroutine that delays entity scanning to allow scene to fully initialize.
        /// </summary>
        private System.Collections.IEnumerator DelayedInitialScan()
        {
            // Wait 0.5 seconds for scene to fully initialize and entities to spawn
            yield return new UnityEngine.WaitForSeconds(0.5f);

            // Scan for entities - EntityNavigator will be updated via OnEntityAdded events
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed initial entity scan completed");
        }

        /// <summary>
        /// Coroutine that delays entity scanning after map transition to allow entities to spawn.
        /// </summary>
        private System.Collections.IEnumerator DelayedMapTransitionScan()
        {
            // Wait 0.5 seconds for new map entities to spawn
            yield return new UnityEngine.WaitForSeconds(0.5f);

            // Scan for entities - EntityNavigator will be updated via OnEntityAdded/OnEntityRemoved events
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed map transition entity scan completed");
        }

        public override void OnUpdate()
        {
            // Update entity cache (handles periodic rescanning)
            entityCache.Update();

            // Check for map transitions
            CheckMapTransition();

            // Handle all input
            inputManager.Update();
        }

        /// <summary>
        /// Checks for map transitions and announces the new map name.
        /// Also manages state monitoring coroutine for world map contexts.
        /// </summary>
        private void CheckMapTransition()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                {
                    int currentMapId = userDataManager.CurrentMapId;

                    // Check if we've entered or left the world map
                    bool nowOnWorldMap = IsWorldMap(currentMapId);
                    if (nowOnWorldMap != isOnWorldMap)
                    {
                        isOnWorldMap = nowOnWorldMap;

                        if (isOnWorldMap)
                        {
                            // Entered world map - start state monitoring
                            Patches.MoveStateMonitor.StartStateMonitoring();
                        }
                        else
                        {
                            // Left world map - stop state monitoring
                            Patches.MoveStateMonitor.StopStateMonitoring();
                        }
                    }

                    if (currentMapId != lastAnnouncedMapId && lastAnnouncedMapId != -1)
                    {
                        // Map has changed - ANNOUNCE IT
                        lastAnnouncedMapId = currentMapId;

                        // Get map name and announce
                        string mapName = FFV_ScreenReader.Field.MapNameResolver.GetCurrentMapName();
                        SpeakText($"Entering {mapName}", interrupt: false);

                        // Delay entity scan to allow new map to fully initialize
                        CoroutineManager.StartManaged(DelayedMapTransitionScan());
                    }
                    else if (lastAnnouncedMapId == -1)
                    {
                        // First run, just store the current map without announcing
                        lastAnnouncedMapId = currentMapId;

                        // Start monitoring if we're already on world map
                        if (isOnWorldMap)
                        {
                            Patches.MoveStateMonitor.StartStateMonitoring();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error detecting map transition: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if map ID represents a world map (where ships/vehicles are available)
        /// Can be expanded to include other maps with ship access as game progresses
        /// </summary>
        private bool IsWorldMap(int mapId)
        {
            // World map IDs - these are examples and may need adjustment based on actual game data
            // Common world map IDs in FF5: 0, 1, 2 for different world states
            // TODO: Verify actual world map IDs through testing and update as needed
            return mapId == 0 || mapId == 1 || mapId == 2;
        }

        internal void AnnounceCurrentEntity()
        {
            try
            {
                var entity = entityNavigator.CurrentEntity;
                if (entity == null)
                {
                    SpeakText("No entities nearby");
                    return;
                }

                if (entity.GameEntity == null || entity.GameEntity.transform == null)
                {
                    // Entity destroyed (likely scene change)
                    return;
                }

                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
                {
                    SpeakText("Not in field");
                    return;
                }
                
                Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
                Vector3 targetPos = entity.GameEntity.transform.localPosition;

                var pathInfo = FieldNavigationHelper.FindPathTo(
                    playerPos,
                    targetPos,
                    playerController.mapHandle,
                    playerController.fieldPlayer
                );

                string announcement;
                if (pathInfo.Success)
                {
                    announcement = $"{pathInfo.Description}";
                }
                else
                {
                    announcement = "no path";
                }

                SpeakText(announcement);
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
                if (entityNavigator.EntityCount == 0)
                {
                    SpeakText("No entities nearby");
                }
                else
                {
                    SpeakText("No pathable entities found");
                }
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
                if (entityNavigator.EntityCount == 0)
                {
                    SpeakText("No entities nearby");
                }
                else
                {
                    SpeakText("No pathable entities found");
                }
            }
        }

        internal void AnnounceEntityOnly()
        {
            try
            {
                var entity = entityNavigator.CurrentEntity;
                if (entity == null)
                {
                    SpeakText("No entities nearby");
                    return;
                }

                if (entity.GameEntity == null || entity.GameEntity.transform == null) return;

                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
                {
                    SpeakText("Not in field");
                    return;
                }
                
                Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
                Vector3 targetPos = entity.GameEntity.transform.localPosition;

                string formatted = entity.FormatDescription(playerController.fieldPlayer.transform.position);
                
                var pathInfo = FieldNavigationHelper.FindPathTo(
                    playerPos,
                    targetPos,
                    playerController.mapHandle,
                    playerController.fieldPlayer
                );
                
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
            int nextCategory = ((int)entityNavigator.CurrentCategory + 1) % CategoryCount;
            EntityCategory newCategory = (EntityCategory)nextCategory;
            
            entityNavigator.SetCategory(newCategory);
            
            AnnounceCategoryChange();
        }

        internal void CyclePreviousCategory()
        {
            int prevCategory = (int)entityNavigator.CurrentCategory - 1;
            if (prevCategory < 0)
                prevCategory = CategoryCount - 1;

            EntityCategory newCategory = (EntityCategory)prevCategory;
            
            entityNavigator.SetCategory(newCategory);
            
            AnnounceCategoryChange();
        }

        internal void ResetToAllCategory()
        {
            if (entityNavigator.CurrentCategory == EntityCategory.All)
            {
                SpeakText("Already in All category");
                return;
            }
            
            entityNavigator.SetCategory(EntityCategory.All);
            
            AnnounceCategoryChange();
        }

        internal void TogglePathfindingFilter()
        {
            filterByPathfinding = !filterByPathfinding;
            
            entityNavigator.FilterByPathfinding = filterByPathfinding;
            
            prefPathfindingFilter.Value = filterByPathfinding;
            prefsCategory.SaveToFile(false);

            string status = filterByPathfinding ? "on" : "off";
            SpeakText($"Pathfinding filter {status}");
        }

        internal void ToggleMapExitFilter()
        {
            filterMapExits = !filterMapExits;

            entityNavigator.FilterMapExits = filterMapExits;
            entityNavigator.RebuildNavigationList();

            prefMapExitFilter.Value = filterMapExits;
            prefsCategory.SaveToFile(false);

            string status = filterMapExits ? "on" : "off";
            SpeakText($"Map exit filter {status}");
        }

        private void AnnounceCategoryChange()
        {
            string categoryName = EntityNavigator.GetCategoryName(entityNavigator.CurrentCategory);
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
            SpeakText($"Teleported {direction} of {entity.Name}");
            LoggerInstance.Msg($"Teleported {direction} of {entity.Name} to position {newPos}");
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
                    SpeakText("User data not available");
                    return;
                }

                int gil = userDataManager.OwendGil;
                string gilMessage = $"{gil:N0} gil";

                SpeakText(gilMessage);
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
                string mapName = FFV_ScreenReader.Field.MapNameResolver.GetCurrentMapName();
                SpeakText(mapName);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing current map: {ex.Message}");
                SpeakText("Error reading map name");
            }
        }

        internal void AnnounceAirshipOrCharacterStatus()
        {
            // Check if we're on the airship by finding an active airship controller with input enabled
            var allControllers = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Map.FieldPlayerController>();
            Il2CppLast.Map.FieldPlayerKeyAirshipController activeAirshipController = null;

            foreach (var controller in allControllers)
            {
                if (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
                {
                    var airshipController = controller.TryCast<Il2CppLast.Map.FieldPlayerKeyAirshipController>();
                    if (airshipController != null && airshipController.InputEnable)
                    {
                        activeAirshipController = airshipController;
                        break;
                    }
                }
            }

            if (activeAirshipController != null)
            {
                // TODO: Implement airship status announcement (can be done later)
                SpeakText("Airship status not yet implemented");
            }
            else
            {
                // Fall back to battle character status
                AnnounceCurrentCharacterStatus();
            }
        }

        private void AnnounceCurrentCharacterStatus()
        {
            try
            {
                // Get the currently active character from the battle patch
                var activeCharacter = FFV_ScreenReader.Patches.ActiveBattleCharacterTracker.CurrentActiveCharacter;

                if (activeCharacter == null)
                {
                    SpeakText("Not in battle or no active character");
                    return;
                }

                string characterName = activeCharacter.Name;

                // Read HP/MP directly from character parameter
                if (activeCharacter.Parameter == null)
                {
                    SpeakText($"{characterName}, status information not available");
                    return;
                }

                var param = activeCharacter.Parameter;
                var statusParts = new System.Collections.Generic.List<string>();
                statusParts.Add(characterName);

                // Add HP
                int currentHP = param.CurrentHP;
                int maxHP = param.ConfirmedMaxHp();
                statusParts.Add($"HP {currentHP} of {maxHP}");

                // Add MP
                int currentMP = param.CurrentMP;
                int maxMP = param.ConfirmedMaxMp();
                statusParts.Add($"MP {currentMP} of {maxMP}");

                // Add status conditions
                var conditionList = param.ConfirmedConditionList();
                if (conditionList != null && conditionList.Count > 0)
                {
                    var conditionNames = new System.Collections.Generic.List<string>();
                    foreach (var condition in conditionList)
                    {
                        if (condition != null)
                        {
                            // Get the condition name from the message ID
                            string conditionMesId = condition.MesIdName;
                            if (!string.IsNullOrEmpty(conditionMesId) && conditionMesId != "None")
                            {
                                var messageManager = Il2CppLast.Management.MessageManager.Instance;
                                if (messageManager != null)
                                {
                                    string conditionName = messageManager.GetMessage(conditionMesId);
                                    if (!string.IsNullOrEmpty(conditionName))
                                    {
                                        conditionNames.Add(conditionName);
                                    }
                                }
                            }
                        }
                    }

                    if (conditionNames.Count > 0)
                    {
                        statusParts.Add(string.Join(", ", conditionNames));
                    }
                }

                string statusMessage = string.Join(", ", statusParts);
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
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">Whether to interrupt current speech (true for user actions, false for game events)</param>
        public static void SpeakText(string text, bool interrupt = true)
        {
            tolk?.Speak(text, interrupt);
        }

        /// <summary>
        /// Pathfind to the currently selected entity and announce directions
        /// </summary>
        public void PathfindToCurrentEntity()
        {
            var currentEntity = entityNavigator.CurrentEntity;
            if (currentEntity == null)
            {
                SpeakText("No entity selected");
                return;
            }

            AnnounceCurrentEntity();
        }

        #region Waypoint Methods

        /// <summary>
        /// Gets the current map ID as a string for waypoint storage
        /// </summary>
        private string GetCurrentMapIdString()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                {
                    return userDataManager.CurrentMapId.ToString();
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error getting map ID: {ex.Message}");
            }
            return "unknown";
        }

        /// <summary>
        /// Cycles to the next waypoint and announces it
        /// </summary>
        internal void CycleNextWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CycleNext();
            if (waypoint == null)
            {
                SpeakText("No waypoints");
                return;
            }

            SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        /// <summary>
        /// Cycles to the previous waypoint and announces it
        /// </summary>
        internal void CyclePreviousWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CyclePrevious();
            if (waypoint == null)
            {
                SpeakText("No waypoints");
                return;
            }

            SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        /// <summary>
        /// Cycles to the next waypoint category
        /// </summary>
        internal void CycleNextWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CycleNextCategory(mapId);
            SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        /// <summary>
        /// Cycles to the previous waypoint category
        /// </summary>
        internal void CyclePreviousWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CyclePreviousCategory(mapId);
            SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        /// <summary>
        /// Pathfinds to the currently selected waypoint
        /// </summary>
        internal void PathfindToCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                SpeakText("No waypoint selected");
                return;
            }

            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;

            var pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                waypoint.Position,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            if (pathInfo.Success)
            {
                SpeakText($"Path to {waypoint.WaypointName}: {pathInfo.Description}");
            }
            else
            {
                // Still announce distance and direction even without path
                string description = waypoint.FormatDescription(playerPos);
                SpeakText($"No path to {waypoint.WaypointName}. {description}");
            }
        }

        /// <summary>
        /// Adds a new waypoint at the player's current position
        /// </summary>
        internal void AddNewWaypoint()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            string mapId = GetCurrentMapIdString();

            // Determine category - use current filter unless it's "All"
            var category = waypointNavigator.CurrentCategory;
            if (category == Field.WaypointCategory.All)
            {
                category = Field.WaypointCategory.Miscellaneous;
            }

            // Generate auto-name
            string name = waypointManager.GetNextWaypointName(mapId);

            var waypoint = waypointManager.AddWaypoint(name, playerPos, mapId, category);
            waypointNavigator.RefreshList(mapId);

            string categoryName = Field.WaypointEntity.GetCategoryDisplayName(category);
            SpeakText($"Added {name} as {categoryName}");
        }

        /// <summary>
        /// Removes the currently selected waypoint
        /// </summary>
        internal void RemoveCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                SpeakText("No waypoint selected");
                return;
            }

            string name = waypoint.WaypointName;
            waypointManager.RemoveWaypoint(waypoint.WaypointId);

            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);
            waypointNavigator.ClearSelection();

            SpeakText($"Removed {name}");
        }

        /// <summary>
        /// Clears all waypoints for the current map (with double-press confirmation)
        /// </summary>
        internal void ClearAllWaypointsForMap()
        {
            string mapId = GetCurrentMapIdString();

            if (waypointManager.ClearMapWaypoints(mapId, out int count))
            {
                waypointNavigator.RefreshList(mapId);
                waypointNavigator.ClearSelection();

                if (count > 0)
                {
                    SpeakText($"Cleared {count} waypoints from map");
                }
                else
                {
                    SpeakText("No waypoints to clear");
                }
            }
            else
            {
                SpeakText($"Press again within 2 seconds to clear {count} waypoints");
            }
        }

        #endregion
    }
}
