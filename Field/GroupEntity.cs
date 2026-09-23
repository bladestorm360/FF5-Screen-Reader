using System.Collections.Generic;
using System.Linq;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Core.Filters;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Field
{
    public class GroupEntity : NavigableEntity
    {
        private List<NavigableEntity> members = new List<NavigableEntity>();
        private IGroupingStrategy strategy;
        private EntityCategory? cachedCategory;
        
        public string GroupKey { get; }
        
        public IReadOnlyList<NavigableEntity> Members => members;
        
        public GroupEntity(string groupKey, IGroupingStrategy strategy, EntityCategory? category = null)
        {
            GroupKey = groupKey;
            this.strategy = strategy;
            cachedCategory = category;
        }
        
        public void AddMember(NavigableEntity entity)
        {
            if (entity != null && !members.Contains(entity))
            {
                members.Add(entity);
                
                if (!cachedCategory.HasValue)
                {
                    cachedCategory = entity.Category;
                }
            }
        }
        
        public void RemoveMember(FieldEntity fieldEntity)
        {
            members.RemoveAll(m => m.GameEntity == fieldEntity);
        }
        
        private NavigableEntity GetRepresentative()
        {
            if (members.Count == 0)
                return null;

            Vector3 playerPos = GetPlayerPosition();
            return strategy.SelectRepresentative(members, playerPos);
        }
        
        private Vector3 GetPlayerPosition()
        {
            return PlayerPositionHelper.GetWorldPosition();
        }
        
        public override FieldEntity GameEntity
        {
            get => GetRepresentative()?.GameEntity;
            set { }
        }
        
        public override Vector3 Position => GetRepresentative()?.Position ?? Vector3.zero;
        
        public override string Name => GetRepresentative()?.DisplayName ?? T("Unknown");
        
        public override EntityCategory Category => cachedCategory ?? EntityCategory.All;

        public override int Priority => GetRepresentative()?.Priority ?? 0;

        public override bool BlocksPathing => GetRepresentative()?.BlocksPathing ?? false;

        /// <summary>
        /// Group is alive as long as at least one member is alive. Per-member liveness
        /// is checked in EntityCache.Scan via the member's own GameEntity key.
        /// </summary>
        public override bool IsAlive => members.Any(m => m.IsAlive);

        protected override string GetDisplayName()
        {
            var rep = GetRepresentative();
            if (rep == null)
                return T("Unknown");
            
            string baseName = rep.DisplayName;

            if (members.Count > 1)
            {
                return $"{baseName} ({string.Format(T("{0} exits"), members.Count)})";
            }

            return baseName;
        }

        protected override string GetEntityTypeName()
        {
            var rep = GetRepresentative();
            if (rep == null)
                return T("Group");
            
            if (rep is MapExitEntity)
                return T("Map Exit");
            else if (rep is NPCEntity)
                return T("NPC");
            else if (rep is TreasureChestEntity)
                return T("Treasure Chest");
            else if (rep is SavePointEntity)
                return T("Save Point");
            else if (rep is EventEntity)
                return T("Event");
            else
                return T("Group");
        }
    }
}