using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Core.Filters;
using FFV_ScreenReader.Utils;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Core
{
    public class EntityNavigator
    {
        private readonly EntityCache cache;
        private List<NavigableEntity> navigationList = new List<NavigableEntity>();
        private NavigableEntity selectedEntity;
        
        private List<IEntityFilter> entityFilters = new List<IEntityFilter>();

        private CategoryFilter categoryFilter;
        private PathfindingFilter pathfindingFilter;
        private ToLayerFilter toLayerFilter;
        private MapExitGroupingStrategy mapExitGroupingStrategy;
        private bool filterMapExits = false;
        
        public bool FilterByPathfinding
        {
            get => pathfindingFilter.IsEnabled;
            set => pathfindingFilter.IsEnabled = value;
        }
        
        public bool FilterMapExits
        {
            get => filterMapExits;
            set
            {
                if (filterMapExits != value)
                {
                    filterMapExits = value;

                    if (value)
                    {
                        cache.EnableGroupingStrategy(mapExitGroupingStrategy);
                    }
                    else
                    {
                        cache.DisableGroupingStrategy(mapExitGroupingStrategy);
                    }
                }
            }
        }
        
        public bool FilterToLayer
        {
            get => toLayerFilter.IsEnabled;
            set
            {
                if (toLayerFilter.IsEnabled != value)
                {
                    toLayerFilter.IsEnabled = value;
                    RebuildNavigationList();
                }
            }
        }

        public EntityCategory Category => categoryFilter.TargetCategory;
        
        public NavigableEntity CurrentEntity => selectedEntity;
        
        public int CurrentIndex => selectedEntity != null ? navigationList.IndexOf(selectedEntity) : -1;

        public int EntityCount => navigationList.Count;

        /// <summary>
        /// True when a filter is active that only takes effect during cycling. Those filters
        /// never remove anything from navigationList, so the raw Count over-reports what the
        /// player can actually reach by cycling.
        /// </summary>
        private bool AnyOnCycleFilterEnabled()
        {
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnCycle || filter.Timing == FilterTiming.All))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Entities the player can actually cycle to, and where the selection sits among
        /// them — so with the pathfinding filter on, a 20-entity map with 4 reachable
        /// announces "1 of 4" and cycling steps 1,2,3,4 in agreement with it.
        ///
        /// With no OnCycle filter enabled these are exactly EntityCount / CurrentIndex, so
        /// the unfiltered announcement is unchanged.
        /// </summary>
        public int FilteredCount => EnsureFilteredView().Count;

        public int FilteredIndex
        {
            get
            {
                if (selectedEntity == null) return -1;
                return EnsureFilteredView().IndexOf(selectedEntity);
            }
        }

        // Recomputing the filtered view means running every OnCycle filter over every
        // entity — for the pathfinding filter that is a reachability test each. Cache it
        // against the position it was computed for: the player stands still while cycling,
        // so one computation serves an entire cycling burst.
        private readonly List<NavigableEntity> filteredView = new List<NavigableEntity>();
        private Vector3 filteredViewPos = new Vector3(float.NaN, float.NaN, float.NaN);
        private int filteredViewListVersion = -1;
        private bool filteredViewFiltersOn;
        private int navigationListVersion;

        /// <summary>Invalidates the cached filtered view. Call when the entity list changes.</summary>
        private void InvalidateFilteredView() => navigationListVersion++;

        private List<NavigableEntity> EnsureFilteredView()
        {
            bool filtersOn = AnyOnCycleFilterEnabled();

            if (!filtersOn)
            {
                // No OnCycle filter: the filtered view IS the navigation list. Mirror it
                // rather than running filters that would all pass anyway.
                if (filteredViewListVersion != navigationListVersion || filteredViewFiltersOn)
                {
                    filteredView.Clear();
                    filteredView.AddRange(navigationList);
                    filteredViewListVersion = navigationListVersion;
                    filteredViewFiltersOn = false;
                    filteredViewPos = new Vector3(float.NaN, float.NaN, float.NaN);
                }
                return filteredView;
            }

            Vector3 playerPos = GetPlayerPosition();

            bool sameCell =
                !float.IsNaN(filteredViewPos.x) &&
                Mathf.Abs(playerPos.x - filteredViewPos.x) < GameConstants.TILE_SIZE * 0.5f &&
                Mathf.Abs(playerPos.y - filteredViewPos.y) < GameConstants.TILE_SIZE * 0.5f;

            if (filteredViewFiltersOn && sameCell && filteredViewListVersion == navigationListVersion)
                return filteredView;

            var context = new FilterContext();
            filteredView.Clear();

            for (int i = 0; i < navigationList.Count; i++)
            {
                if (PassesOnCycleFilters(navigationList[i], context))
                    filteredView.Add(navigationList[i]);
            }

            filteredViewPos = playerPos;
            filteredViewListVersion = navigationListVersion;
            filteredViewFiltersOn = true;

            return filteredView;
        }

        
        public EntityNavigator(EntityCache cache)
        {
            this.cache = cache;
            
            categoryFilter = new CategoryFilter();
            pathfindingFilter = new PathfindingFilter();
            toLayerFilter = new ToLayerFilter();
            mapExitGroupingStrategy = new MapExitGroupingStrategy();

            entityFilters.Add(categoryFilter);
            entityFilters.Add(pathfindingFilter);
            entityFilters.Add(toLayerFilter);
            
            cache.OnEntityAdded += HandleEntityAdded;
            cache.OnEntityRemoved += HandleEntityRemoved;
            
            RebuildNavigationList();
        }
        
        public void SetCategory(EntityCategory category)
        {
            categoryFilter.TargetCategory = category;
            RebuildNavigationList();
        }

        /// <summary>
        /// Runs the delta scan: checks for map transitions and updates the entity cache
        /// against the live FieldEntity list. Cache events (OnEntityAdded/OnEntityRemoved)
        /// will incrementally update navigationList via HandleEntityAdded/HandleEntityRemoved.
        /// Call from navigation entry points to keep state current without eager-push hooks.
        /// </summary>
        public void RefreshIfNeeded()
        {
            // Scan() already handles map changes (it re-enumerates and updates lastScannedMapId),
            // so the prior EnsureCorrectMap() call was a redundant second full scan on map change.
            cache.Scan();
        }
        
        private void HandleEntityAdded(NavigableEntity entity)
        {
            var context = new FilterContext();
            
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnAdd || filter.Timing == FilterTiming.All) &&
                    !filter.PassesFilter(entity, context))
                {
                    return;
                }
            }
            
            InsertSorted(entity);
            
            if (selectedEntity == null)
            {
                selectedEntity = entity;
            }
        }
        
        private void HandleEntityRemoved(NavigableEntity entity)
        {
            navigationList.Remove(entity);
            InvalidateFilteredView();

            if (selectedEntity == entity)
            {
                selectedEntity = navigationList.Count > 0 ? navigationList[0] : null;
            }
        }

        public void RebuildNavigationList()
        {
            navigationList.Clear();
            InvalidateFilteredView();
            
            var context = new FilterContext();
            
            var enabledOnAddFilters = new List<IEntityFilter>();
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnAdd || filter.Timing == FilterTiming.All))
                {
                    enabledOnAddFilters.Add(filter);
                }
            }
            
            var filtered = new List<NavigableEntity>();
            var uniqueEntities = cache.Entities.Values.Distinct().ToList();

            foreach (var entity in uniqueEntities)
            {
                // Skip destroyed entities (Unity throws on destroyed GameObjects)
                try
                {
                    if (entity?.GameEntity == null || entity.GameEntity.gameObject == null ||
                        !entity.GameEntity.gameObject.activeInHierarchy)
                        continue;
                }
                catch { continue; }

                bool passesAll = true;

                foreach (var filter in enabledOnAddFilters)
                {
                    if (!filter.PassesFilter(entity, context))
                    {
                        passesAll = false;
                        break;
                    }
                }

                if (passesAll)
                {
                    filtered.Add(entity);
                }
            }
            
            navigationList = SortByDistance(filtered);
            
            if (selectedEntity != null && !navigationList.Contains(selectedEntity))
            {
                selectedEntity = navigationList.Count > 0 ? navigationList[0] : null;
            }
        }
        
        private void InsertSorted(NavigableEntity entity)
        {
            Vector3 playerPos = GetPlayerPosition();
            float distance = Vector3.Distance(entity.Position, playerPos);
            
            int index = 0;
            for (int i = 0; i < navigationList.Count; i++)
            {
                float existingDist = Vector3.Distance(navigationList[i].Position, playerPos);
                if (distance < existingDist)
                {
                    index = i;
                    break;
                }
                index = i + 1;
            }

            navigationList.Insert(index, entity);
            InvalidateFilteredView();
        }
        
        private List<NavigableEntity> SortByDistance(List<NavigableEntity> entities)
        {
            return Utils.CollectionHelper.SortByDistance(entities, GetPlayerPosition(), e => e.Position);
        }

        private int ReSortNavigationList()
        {
            if (navigationList.Count == 0)
                return -1;

            return Utils.CollectionHelper.SortByDistanceInPlace(navigationList, GetPlayerPosition(), e => e.Position, selectedEntity);
        }
        
        public bool CycleNext()
        {
            if (navigationList.Count == 0)
                return false;
            
            int currentIdx = ReSortNavigationList();
            
            var context = new FilterContext();
            
            int attempts = 0;
            while (attempts < navigationList.Count)
            {
                currentIdx = (currentIdx + 1) % navigationList.Count;
                attempts++;

                var candidate = navigationList[currentIdx];
                
                if (PassesOnCycleFilters(candidate, context))
                {
                    selectedEntity = candidate;
                    return true;
                }
            }
            
            return false;
        }
        
        public bool CyclePrevious()
        {
            if (navigationList.Count == 0)
                return false;
            
            int currentIdx = ReSortNavigationList();
            
            var context = new FilterContext();
            
            int attempts = 0;
            while (attempts < navigationList.Count)
            {
                currentIdx--;
                if (currentIdx < 0)
                    currentIdx = navigationList.Count - 1;
                attempts++;

                var candidate = navigationList[currentIdx];
                
                if (PassesOnCycleFilters(candidate, context))
                {
                    selectedEntity = candidate;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Selects the nearest entity the player can cycle to (passes the OnCycle filters), so a
        /// category change lands on what cycling would reach first. False when there is none.
        /// </summary>
        public bool SelectFirst()
        {
            if (navigationList.Count == 0)
                return false;

            ReSortNavigationList();

            var context = new FilterContext();
            foreach (var candidate in navigationList)
            {
                if (PassesOnCycleFilters(candidate, context))
                {
                    selectedEntity = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool PassesOnCycleFilters(NavigableEntity entity, FilterContext context)
        {
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnCycle || filter.Timing == FilterTiming.All) &&
                    !filter.PassesFilter(entity, context))
                {
                    return false;
                }
            }
            return true;
        }
        
        private Vector3 GetPlayerPosition()
        {
            return Utils.PlayerPositionHelper.GetWorldPosition();
        }
        
        public static string GetCategoryName(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.All:
                    return T("All");
                case EntityCategory.Chests:
                    return T("Chests");
                case EntityCategory.NPCs:
                    return T("NPCs");
                case EntityCategory.MapExits:
                    return T("Map Exits");
                case EntityCategory.Events:
                    return T("Events");
                case EntityCategory.Vehicles:
                    return T("Vehicles");
                case EntityCategory.Waypoints:
                    return T("Waypoints");
                default:
                    return T("Unknown");
            }
        }
    }
}