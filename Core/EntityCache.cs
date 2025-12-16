using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Core.Filters;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;

namespace FFV_ScreenReader.Core
{
    public class EntityCache
    {
        private readonly float scanInterval;
        private float lastScanTime = 0f;
        private Dictionary<FieldEntity, NavigableEntity> entityMap = new Dictionary<FieldEntity, NavigableEntity>();
        private List<IGroupingStrategy> enabledStrategies = new List<IGroupingStrategy>();
        private Dictionary<string, GroupEntity> groupsByKey = new Dictionary<string, GroupEntity>();

        public event Action<NavigableEntity> OnEntityAdded;
        
        public event Action<NavigableEntity> OnEntityRemoved;
        
        public IReadOnlyDictionary<FieldEntity, NavigableEntity> Entities => entityMap;
        
        public EntityCache(float scanInterval = 0.1f)
        {
            this.scanInterval = scanInterval;
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
        
        public void Update()
        {
            if (Time.time - lastScanTime >= scanInterval)
            {
                lastScanTime = Time.time;
                Scan();
            }
        }
        
        public void Scan()
        {
            var currentFieldEntities = FieldNavigationHelper.GetAllFieldEntities();
            
            var currentSet = new HashSet<FieldEntity>(currentFieldEntities);
            
            var toRemove = new List<FieldEntity>();
            foreach (var kvp in entityMap)
            {
                if (!currentSet.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var fieldEntity in toRemove)
            {
                HandleEntityRemoval(fieldEntity);
            }
            
            Vector3 playerPos = GetPlayerPosition();

            foreach (var fieldEntity in currentFieldEntities)
            {
                if (!entityMap.ContainsKey(fieldEntity))
                {
                    var navEntity = EntityFactory.CreateFromFieldEntity(fieldEntity, playerPos);
                    
                    if (navEntity != null)
                    {
                        HandleEntityAddition(fieldEntity, navEntity);
                    }
                }
            }
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
            lastScanTime = Time.time;
            Scan();
        }
        
        private Vector3 GetPlayerPosition()
        {
            var playerController = Utils.GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.position;
        }
    }
}
