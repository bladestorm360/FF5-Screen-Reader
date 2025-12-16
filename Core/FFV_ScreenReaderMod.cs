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

        // Menu cursor tracking for initial announcement
        private string lastCursorContext = "";  // Tracks cursor name + parent context
        private float cursorCheckCooldown = 0f;
        private const float CURSOR_CHECK_INTERVAL = 0.1f; // Check every 100ms

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFV Screen Reader Mod loaded!");
            
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;
            
            prefsCategory = MelonPreferences.CreateCategory("FFV_ScreenReader");
            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");
            prefMapExitFilter = prefsCategory.CreateEntry<bool>("MapExitFilter", false, "Map Exit Filter", "Filter multiple map exits to the same destination, showing only the closest one");
            
            filterByPathfinding = prefPathfindingFilter.Value;
            filterMapExits = prefMapExitFilter.Value;
            
            tolk = new TolkWrapper();
            tolk.Load();
            
            entityCache = new EntityCache(ENTITY_SCAN_INTERVAL);

            entityNavigator = new EntityNavigator(entityCache);
            entityNavigator.FilterByPathfinding = filterByPathfinding;
            entityNavigator.FilterMapExits = filterMapExits;

            inputManager = new InputManager(this);
        }

        public override void OnDeinitializeMelon()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;

            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }
        
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                LoggerInstance.Msg($"[ComponentCache] Scene loaded: {scene.name}");

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
                
                var fieldMap = UnityEngine.Object.FindObjectOfType<Il2Cpp.FieldMap>();
                if (fieldMap != null)
                {
                    GameObjectCache.Register(fieldMap);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldMap: {fieldMap.gameObject?.name}");
                    
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
        
        private System.Collections.IEnumerator DelayedInitialScan()
        {
            yield return new UnityEngine.WaitForSeconds(0.5f);
            
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed initial entity scan completed");
        }
        
        private System.Collections.IEnumerator DelayedMapTransitionScan()
        {
            yield return new UnityEngine.WaitForSeconds(0.5f);
            
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed map transition entity scan completed");
        }

        public override void OnUpdate()
        {
            entityCache.Update();

            CheckMapTransition();

            // Menu initialization feature disabled - was causing issues with
            // interrupting other announcements and reading wrong menu items
            // CheckForNewMenuCursor();

            inputManager.Update();
        }

        /// <summary>
        /// Check if a new menu cursor has become active and announce its current item.
        /// This handles announcing the first item when menus open.
        /// </summary>
        private void CheckForNewMenuCursor()
        {
            try
            {
                // Rate limit cursor checks
                cursorCheckCooldown -= Time.deltaTime;
                if (cursorCheckCooldown > 0f)
                {
                    return;
                }
                cursorCheckCooldown = CURSOR_CHECK_INTERVAL;

                // Skip if in battle (let battle patches handle it)
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities != null && enemyEntities.Length > 0)
                {
                    lastCursorContext = ""; // Reset tracking when entering battle
                    return;
                }

                // Find all active cursors
                var cursors = UnityEngine.Object.FindObjectsOfType<GameCursor>();
                if (cursors == null || cursors.Length == 0)
                {
                    if (!string.IsNullOrEmpty(lastCursorContext))
                    {
                        lastCursorContext = ""; // Reset when no cursors active
                    }
                    return;
                }

                // Find the first active and visible cursor
                GameCursor activeCursor = null;
                foreach (var cursor in cursors)
                {
                    if (cursor != null && cursor.gameObject != null &&
                        cursor.gameObject.activeInHierarchy)
                    {
                        activeCursor = cursor;
                        break;
                    }
                }

                if (activeCursor == null)
                {
                    if (!string.IsNullOrEmpty(lastCursorContext))
                    {
                        lastCursorContext = "";
                    }
                    return;
                }

                // Build a context string that includes cursor name AND parent hierarchy
                // This way we detect when the same cursor is used in a different menu
                string currentContext = GetCursorContext(activeCursor);

                if (currentContext != lastCursorContext)
                {
                    LoggerInstance.Msg($"[Menu Init] New cursor context detected: {currentContext}");
                    LoggerInstance.Msg($"[Menu Init] Previous context was: {lastCursorContext}");
                    lastCursorContext = currentContext;

                    // Wait a frame for UI to settle, then announce
                    CoroutineManager.StartManaged(DelayedMenuAnnouncement(activeCursor));
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in CheckForNewMenuCursor: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a context string for a cursor that includes its name and key parent names.
        /// This helps detect when the same cursor object is used in different menus.
        /// </summary>
        private string GetCursorContext(GameCursor cursor)
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(cursor.gameObject.name);

                // Walk up the hierarchy and collect significant parent names
                Transform current = cursor.transform.parent;
                int depth = 0;

                while (current != null && depth < 5)
                {
                    string parentName = current.name.ToLower();

                    // Include parent names that indicate menu context
                    if (parentName.Contains("menu") || parentName.Contains("window") ||
                        parentName.Contains("content") || parentName.Contains("select") ||
                        parentName.Contains("status") || parentName.Contains("equip") ||
                        parentName.Contains("item") || parentName.Contains("command") ||
                        parentName.Contains("party") || parentName.Contains("save") ||
                        parentName.Contains("load") || parentName.Contains("config"))
                    {
                        parts.Add(current.name);
                    }

                    current = current.parent;
                    depth++;
                }

                return string.Join("/", parts);
            }
            catch
            {
                return cursor.gameObject.name;
            }
        }

        private System.Collections.IEnumerator DelayedMenuAnnouncement(GameCursor cursor)
        {
            // Wait 2 frames for UI to fully update
            yield return null;
            yield return null;

            try
            {
                if (cursor == null || cursor.gameObject == null || !cursor.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                LoggerInstance.Msg($"[Menu Init] Announcing initial cursor position for: {cursor.gameObject.name}");

                // Use the same menu text discovery as cursor navigation
                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    cursor,
                    "MenuInit",
                    -1,
                    false
                );
                CoroutineManager.StartManaged(coroutine);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error in DelayedMenuAnnouncement: {ex.Message}");
            }
        }
        
        private void CheckMapTransition()
        {
            try
            {
                var userDataManager = UserDataManager.instance;
                if (userDataManager != null)
                {
                    int currentMapId = userDataManager.CurrentMapId;
                    if (currentMapId != lastAnnouncedMapId && lastAnnouncedMapId != -1)
                    {
                        //string mapName = MapNameResolver.GetCurrentMapName();
                        //SpeakText($"Entering {mapName}", interrupt: false);
                        lastAnnouncedMapId = currentMapId;
                        
                        CoroutineManager.StartManaged(DelayedMapTransitionScan());
                    }
                    else if (lastAnnouncedMapId == -1)
                    {
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

        private void AnnounceCurrentCharacterStatus()
        {
        }

        internal void AnnounceGilAmount()
        {
            try
            {
                var userDataManager = UserDataManager.instance;

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
        }
        
        internal void AnnounceAirshipOrCharacterStatus()
        {
        }

        private void AnnounceAirshipStatus()
        {
        }
        
        public static void SpeakText(string text, bool interrupt = true)
        {
            try
            {
                MelonLogger.Msg($"[SpeakText] Called with text: '{text}', interrupt: {interrupt}");
                
                if (tolk == null)
                {
                    MelonLogger.Warning("[SpeakText] Tolk wrapper is null!");
                    return;
                }
                
                MelonLogger.Msg($"[SpeakText] Calling tolk.Speak...");
                tolk.Speak(text, interrupt);
                MelonLogger.Msg($"[SpeakText] tolk.Speak completed");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SpeakText] Exception: {ex.Message}");
            }
        }
    }
}
