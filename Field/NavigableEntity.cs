using System;
using System.Collections.Generic;
using Il2CppLast.Entity.Field;
using UnityEngine;
using FFV_ScreenReader.Core;
using Il2CppLast.Map;
using Il2Cpp;

namespace FFV_ScreenReader.Field
{
    public abstract class NavigableEntity
    {
        public virtual FieldEntity GameEntity { get; set; }
        
        public virtual Vector3 Position => GameEntity?.transform?.position ?? Vector3.zero;
        
        public virtual string Name => GameEntity?.Property?.Name ?? "Unknown";
        
        public abstract EntityCategory Category { get; }
        
        public abstract int Priority { get; }
        
        public abstract bool BlocksPathing { get; }
        
        public virtual bool IsInteractive => true;
        
        protected abstract string GetDisplayName();
        
        protected abstract string GetEntityTypeName();
        
        public virtual string FormatDescription(Vector3 playerPos)
        {
            float distance = Vector3.Distance(playerPos, Position);
            string direction = GetDirection(playerPos, Position);
            return $"{GetDisplayName()} ({FormatSteps(distance)} {direction}) - {GetEntityTypeName()}";
        }
        
        protected string GetDirection(Vector3 from, Vector3 to)
        {
            Vector3 diff = to - from;
            float angle = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;
            
            if (angle < 0) angle += 360;
            
            if (angle >= 337.5 || angle < 22.5) return "North";
            else if (angle >= 22.5 && angle < 67.5) return "Northeast";
            else if (angle >= 67.5 && angle < 112.5) return "East";
            else if (angle >= 112.5 && angle < 157.5) return "Southeast";
            else if (angle >= 157.5 && angle < 202.5) return "South";
            else if (angle >= 202.5 && angle < 247.5) return "Southwest";
            else if (angle >= 247.5 && angle < 292.5) return "West";
            else if (angle >= 292.5 && angle < 337.5) return "Northwest";
            else return "Unknown";
        }
        
        protected string FormatSteps(float distance)
        {
            float steps = distance / 16f;
            string stepLabel = Math.Abs(steps - 1f) < 0.1f ? "step" : "steps";
            return $"{steps:F1} {stepLabel}";
        }
    }
    
    public class TreasureChestEntity : NavigableEntity
    {
        public bool IsOpened => GameEntity?.TryCast<FieldTresureBox>()?.isOpen ?? false;

        public override EntityCategory Category => EntityCategory.Chests;

        public override int Priority => 3;

        public override bool BlocksPathing => true;
        
        public override bool IsInteractive => !IsOpened;

        protected override string GetDisplayName()
        {
            string status = IsOpened ? "Opened" : "Unopened";
            return $"{status} {Name}";
        }

        protected override string GetEntityTypeName()
        {
            return "Treasure Chest";
        }
    }
    
    public class NPCEntity : NavigableEntity
    {
        public string AssetName => GameEntity?.Property?.TryCast<PropertyNpc>()?.AssetName ?? "";
        
        public bool IsShop => GameEntity?.Property?.TryCast<PropertyNpc>()?.ProductGroupId > 0;
        
        public FieldEntityConstants.MoveType MovementType =>
            GameEntity?.Property?.TryCast<PropertyNpc>()?.MoveType ?? FieldEntityConstants.MoveType.None;

        public override EntityCategory Category => EntityCategory.NPCs;

        public override int Priority => 4;

        public override bool BlocksPathing => true;

        protected override string GetDisplayName()
        {
            var details = new List<string>();
            
            if (IsShop)
            {
                details.Add("shop");
            }
            
            if (MovementType == FieldEntityConstants.MoveType.None)
            {
                details.Add("stationary");
            }
            else if (MovementType == FieldEntityConstants.MoveType.Stamp)
            {
                details.Add("wandering");
            }
            else if (MovementType == FieldEntityConstants.MoveType.Area ||
                     MovementType == FieldEntityConstants.MoveType.Route)
            {
                details.Add("patrolling");
            }

            string detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
            return $"{Name}{detailStr}";
        }

        protected override string GetEntityTypeName()
        {
            return "NPC";
        }
    }
    
    public class MapExitEntity : NavigableEntity
    {
        public int DestinationMapId => GameEntity?.Property?.TryCast<PropertyGotoMap>()?.MapId ?? -1;
        
        public override EntityCategory Category => EntityCategory.MapExits;

        public override int Priority => 1;

        public override bool BlocksPathing => true;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Map Exit";
        }
    }
    
    public class SavePointEntity : NavigableEntity
    {
        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 2;

        public override bool BlocksPathing => false;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Save Point";
        }
    }
    
    public class DoorTriggerEntity : NavigableEntity
    {
        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 6;

        public override bool BlocksPathing => false;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Door/Trigger";
        }
    }
    
    public class EventEntity : NavigableEntity
    {
        public MapConstants.ObjectType EventType =>
            GameEntity?.Property != null
                ? (MapConstants.ObjectType)GameEntity.Property.ObjectType
                : MapConstants.ObjectType.PointIn;

        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 8;

        public override bool BlocksPathing => EventType == MapConstants.ObjectType.TelepoPoint;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return GetEventTypeNameStatic(EventType);
        }

        public static string GetEventTypeNameStatic(MapConstants.ObjectType type)
        {
            switch (type)
            {
                case MapConstants.ObjectType.TelepoPoint:
                    return "Teleport";
                case MapConstants.ObjectType.Event:
                case MapConstants.ObjectType.RandomEvent:
                    return "Event";
                default:
                    return type.ToString();
            }
        }
    }
}
