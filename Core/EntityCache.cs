using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Core.Filters;
using Il2CppLast.Entity.Field;
using Il2CppLast.Management;
using Il2CppLast.Map;
using MelonLoader;

namespace FFV_ScreenReader.Core
{
    public class EntityCache
    {
        private Dictionary<FieldEntity, NavigableEntity> entityMap = new Dictionary<FieldEntity, NavigableEntity>();
        private List<IGroupingStrategy> enabledStrategies = new List<IGroupingStrategy>();
        private Dictionary<string, GroupEntity> groupsByKey = new Dictionary<string, GroupEntity>();
        private int lastScannedMapId = -1;

        public event Action<NavigableEntity> OnEntityAdded;

        public event Action<NavigableEntity> OnEntityRemoved;

        public IReadOnlyDictionary<FieldEntity, NavigableEntity> Entities => entityMap;

        public EntityCache()
        {
        }
        
        public void EnableGroupingStrategy(IGroupingStrategy strategy)
        {
            if (enabledStrategies.Contains(strategy))
                return;

            enabledStrategies.Add(strategy);
            
            RegroupEntitiesForStrategy(strategy);
        }
        
        private void RegroupEntitiesForStrategy(IGroupingStrategy strategy)
        {
            var individualsToGroup = entityMap
                .Where(kvp => !(kvp.Value is GroupEntity))
                .Select(kvp => new { FieldEntity = kvp.Key, NavEntity = kvp.Value })
                .Where(item => strategy.GetGroupKey(item.NavEntity) != null)
                .ToList();
            
            var grouped = individualsToGroup
                .GroupBy(item => strategy.GetGroupKey(item.NavEntity))
                .Where(g => g.Key != null)
                .ToList();

            foreach (var group in grouped)
            {
                var firstMember = group.First();
                EntityCategory groupCategory = firstMember.NavEntity.Category;
                
                foreach (var item in group)
                {
                    OnEntityRemoved?.Invoke(item.NavEntity);
                }
                
                var groupEntity = new GroupEntity(group.Key, strategy, groupCategory);
                
                groupsByKey[group.Key] = groupEntity;
                
                foreach (var item in group)
                {
                    groupEntity.AddMember(item.NavEntity);
                    entityMap[item.FieldEntity] = groupEntity;
                }
                
                OnEntityAdded?.Invoke(groupEntity);
            }
        }
        
        public void DisableGroupingStrategy(IGroupingStrategy strategy)
        {
            if (!enabledStrategies.Contains(strategy))
                return;

            enabledStrategies.Remove(strategy);
            
            DissolveGroupsForStrategy(strategy);
        }
        
        private void DissolveGroupsForStrategy(IGroupingStrategy strategy)
        {
            var groups = entityMap.Values
                .OfType<GroupEntity>()
                .Where(g => IsGroupFromStrategy(g, strategy))
                .Distinct()
                .ToList();

            foreach (var group in groups)
            {
                groupsByKey.Remove(group.GroupKey);
                
                OnEntityRemoved?.Invoke(group);
                
                foreach (var member in group.Members.ToList())
                {
                    var fieldEntity = member.GameEntity;
                    if (fieldEntity != null && entityMap.ContainsKey(fieldEntity))
                    {
                        entityMap[fieldEntity] = member;
                        OnEntityAdded?.Invoke(member);
                    }
                }
            }
        }
        
        private bool IsGroupFromStrategy(GroupEntity group, IGroupingStrategy strategy)
        {
            if (group.Members.Count == 0)
                return false;
            
            var firstMember = group.Members[0];
            string groupKey = strategy.GetGroupKey(firstMember);

            return groupKey != null && groupKey == group.GroupKey;
        }
        
        public void Scan()
        {
            var sw = Stopwatch.StartNew();
            int currentMapId = GetCurrentMapId();

            var currentFieldEntities = FieldNavigationHelper.GetAllFieldEntities();

            var currentSet = new HashSet<FieldEntity>(currentFieldEntities);

            // REMOVE phase 1: entities no longer in the world
            var toRemove = new List<FieldEntity>();
            foreach (var kvp in entityMap)
            {
                if (!currentSet.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            // REMOVE phase 2: entities whose backing GameObject was deactivated
            // (opened chest sprites, NPCs despawned by events). For GroupEntity values,
            // check the specific member that maps to this FieldEntity key.
            foreach (var kvp in entityMap)
            {
                if (toRemove.Contains(kvp.Key)) continue;

                var value = kvp.Value;
                bool dead;
                if (value is GroupEntity group)
                {
                    var member = group.Members.FirstOrDefault(m => m.GameEntity == kvp.Key);
                    dead = member != null && !member.IsAlive;
                }
                else
                {
                    dead = !value.IsAlive;
                }
                if (dead) toRemove.Add(kvp.Key);
            }

            foreach (var fieldEntity in toRemove)
            {
                HandleEntityRemoval(fieldEntity);
            }

            Vector3 playerPos = GetPlayerPosition();

            int addedCount = 0;
            foreach (var fieldEntity in currentFieldEntities)
            {
                if (!entityMap.ContainsKey(fieldEntity))
                {
                    var navEntity = EntityFactory.CreateFromFieldEntity(fieldEntity, playerPos);

                    if (navEntity != null)
                    {
                        HandleEntityAddition(fieldEntity, navEntity);
                        addedCount++;
                    }
                }
            }

            lastScannedMapId = currentMapId;
            sw.Stop();
            MelonLogger.Msg($"[EntityCache] Scan: {currentFieldEntities.Count} field entities, +{addedCount} new, -{toRemove.Count} stale, took {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Soft fallback: if a navigation entry point detects the current map differs from
        /// the last scanned map, force a fresh scan. Backstop for any scripted transition
        /// that bypasses CheckMapTransition's hard rescan path.
        /// </summary>
        public void EnsureCorrectMap()
        {
            try
            {
                int currentMapId = GetCurrentMapId();
                if (currentMapId > 0 && currentMapId != lastScannedMapId)
                    Scan();
            }
            catch { } // Map ID read may fail during transitions
        }

        private int GetCurrentMapId()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager != null)
                    return userDataManager.CurrentMapId;
            }
            catch { } // UserDataManager may not be initialized
            return -1;
        }
        
        private void HandleEntityAddition(FieldEntity fieldEntity, NavigableEntity navEntity)
        {
            GroupEntity group = FindOrCreateGroup(navEntity);

            if (group != null)
            {
                bool isNewGroup = group.Members.Count == 0;
                group.AddMember(navEntity);
                entityMap[fieldEntity] = group;
                
                if (isNewGroup)
                {
                    OnEntityAdded?.Invoke(group);
                }
            }
            else
            {
                entityMap[fieldEntity] = navEntity;
                OnEntityAdded?.Invoke(navEntity);
            }
        }
        
        private void HandleEntityRemoval(FieldEntity fieldEntity)
        {
            if (!entityMap.TryGetValue(fieldEntity, out var entity))
                return;

            if (entity is GroupEntity group)
            {
                group.RemoveMember(fieldEntity);

                if (group.Members.Count == 0)
                {
                    groupsByKey.Remove(group.GroupKey);
                    OnEntityRemoved?.Invoke(group);
                }
            }
            else
            {
                OnEntityRemoved?.Invoke(entity);
            }

            entityMap.Remove(fieldEntity);
        }
        
        private GroupEntity FindOrCreateGroup(NavigableEntity navEntity)
        {
            foreach (var strategy in enabledStrategies)
            {
                string groupKey = strategy.GetGroupKey(navEntity);
                if (groupKey != null)
                {
                    if (groupsByKey.TryGetValue(groupKey, out var existingGroup))
                        return existingGroup;
                    
                    var newGroup = new GroupEntity(groupKey, strategy, navEntity.Category);
                    groupsByKey[groupKey] = newGroup;
                    return newGroup;
                }
            }

            return null;
        }
        
        public void ForceScan()
        {
            Scan();
        }

        public List<Vector3> GetMapExitPositions()
        {
            var positions = new List<Vector3>();
            foreach (var entity in entityMap.Values)
            {
                if (entity.Category == EntityCategory.MapExits)
                    positions.Add(entity.Position);
            }
            return positions;
        }
        
        private Vector3 GetPlayerPosition()
        {
            return Utils.PlayerPositionHelper.GetWorldPosition();
        }
    }
}
