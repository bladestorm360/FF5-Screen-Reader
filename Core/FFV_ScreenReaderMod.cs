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
        Vehicles = 5
    }

    public class FFV_ScreenReaderMod : MelonMod
    {
        private static TolkWrapper tolk;
        private InputManager inputManager;
        private EntityCache entityCache;
        private EntityNavigator entityNavigator;
        
        private const float ENTITY_SCAN_INTERVAL = 5f;
        
        private static readonly int CategoryCount = System.Enum.GetValues(typeof(EntityCategory)).Length;
        
        private bool filterByPathfinding = false;
        
        private bool filterMapExits = false;
        
        private int lastAnnouncedMapId = -1;
        
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
        /// </summary>
        private void CheckMapTransition()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                {
                    int currentMapId = userDataManager.CurrentMapId;
                    if (currentMapId != lastAnnouncedMapId && lastAnnouncedMapId != -1)
                    {
                        // Map has changed
                        lastAnnouncedMapId = currentMapId;

                        // Delay entity scan to allow new map to fully initialize
                        CoroutineManager.StartManaged(DelayedMapTransitionScan());
                    }
                    else if (lastAnnouncedMapId == -1)
                    {
                        // First run, just store the current map without announcing
                        lastAnnouncedMapId = currentMapId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error detecting map transition: {ex.Message}");
            }
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
            // Map announcement feature not yet implemented for FFV
            SpeakText("Map announcement not available");
        }

        internal void AnnounceAirshipOrCharacterStatus()
        {
            // Airship/character status not yet implemented for FFV
            SpeakText("Status not available");
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
    }
}
