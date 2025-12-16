using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Core.Filters;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;

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
        
        public EntityCategory Category => categoryFilter.TargetCategory;
        
        public NavigableEntity CurrentEntity => selectedEntity;
        
        public int CurrentIndex => selectedEntity != null ? navigationList.IndexOf(selectedEntity) : -1;
        
        public int EntityCount => navigationList.Count;
        
        public EntityCategory CurrentCategory => Category;
        
        public EntityNavigator(EntityCache cache)
        {
            this.cache = cache;
            
            categoryFilter = new CategoryFilter();
            pathfindingFilter = new PathfindingFilter();
            mapExitGroupingStrategy = new MapExitGroupingStrategy();
            
            entityFilters.Add(categoryFilter);
            entityFilters.Add(pathfindingFilter);
            
            cache.OnEntityAdded += HandleEntityAdded;
            cache.OnEntityRemoved += HandleEntityRemoved;
            
            RebuildNavigationList();
        }
        
        public void SetCategory(EntityCategory category)
        {
            categoryFilter.TargetCategory = category;
            RebuildNavigationList();
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
            
            if (selectedEntity == entity)
            {
                selectedEntity = navigationList.Count > 0 ? navigationList[0] : null;
            }
        }
        
        public void RebuildNavigationList()
        {
            navigationList.Clear();
            
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
        }
        
        private List<NavigableEntity> SortByDistance(List<NavigableEntity> entities)
        {
            Vector3 playerPos = GetPlayerPosition();
            
            var withDistances = new List<(NavigableEntity entity, float distance)>(entities.Count);
            foreach (var entity in entities)
            {
                withDistances.Add((entity, Vector3.Distance(entity.Position, playerPos)));
            }
            
            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            var result = new List<NavigableEntity>(withDistances.Count);
            foreach (var item in withDistances)
            {
                result.Add(item.entity);
            }
            return result;
        }
        
        private int ReSortNavigationList()
        {
            if (navigationList.Count == 0)
                return -1;

            Vector3 playerPos = GetPlayerPosition();
            
            var withDistances = new List<(NavigableEntity entity, float distance)>(navigationList.Count);
            foreach (var entity in navigationList)
            {
                withDistances.Add((entity, Vector3.Distance(entity.Position, playerPos)));
            }
            
            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            int selectedIdx = -1;
            for (int i = 0; i < withDistances.Count; i++)
            {
                navigationList[i] = withDistances[i].entity;
                if (selectedEntity != null && withDistances[i].entity == selectedEntity)
                {
                    selectedIdx = i;
                }
            }

            return selectedIdx;
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
            var playerController = Utils.GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.position;
        }
        
        public static string GetCategoryName(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.All:
                    return "All";
                case EntityCategory.Chests:
                    return "Chests";
                case EntityCategory.NPCs:
                    return "NPCs";
                case EntityCategory.MapExits:
                    return "Map Exits";
                case EntityCategory.Events:
                    return "Events";
                case EntityCategory.Vehicles:
                    return "Vehicles";
                default:
                    return "Unknown";
            }
        }
    }
}