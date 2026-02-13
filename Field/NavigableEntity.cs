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
        
        public virtual string Name
        {
            get
            {
                string rawName = GameEntity?.Property?.Name ?? "Unknown";
                return Utils.EntityTranslator.Translate(rawName);
            }
        }
        
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
            return FFV_ScreenReader.Utils.DirectionHelper.GetCompassDirection(from, to);
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
            return $"{status} {GetEntityTypeName()}";
        }

        protected override string GetEntityTypeName()
        {
            return "Treasure Chest";
        }

        public override string FormatDescription(Vector3 playerPos)
        {
            float distance = Vector3.Distance(playerPos, Position);
            string direction = GetDirection(playerPos, Position);
            string status = IsOpened ? "Opened" : "Unopened";
            return $"{status} {GetEntityTypeName()} ({FormatSteps(distance)} {direction})";
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

        public string DestinationName => MapNameResolver.GetMapExitName(
            GameEntity?.Property?.TryCast<PropertyGotoMap>()
        );

        public override EntityCategory Category => EntityCategory.MapExits;

        public override int Priority => 1;

        public override bool BlocksPathing => true;

        protected override string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(DestinationName))
            {
                return DestinationName;
            }
            return GetEntityTypeName();
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

        public override string FormatDescription(Vector3 playerPos)
        {
            float distance = Vector3.Distance(playerPos, Position);
            string direction = GetDirection(playerPos, Position);
            return $"{GetEntityTypeName()} ({FormatSteps(distance)} {direction})";
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

    /// <summary>
    /// Represents a navigable vehicle entity (ship, airship, chocobo, etc.).
    /// </summary>
    public class VehicleEntity : NavigableEntity
    {
        public int TransportationId { get; set; }
        public string MessageId { get; set; }

        public override EntityCategory Category => EntityCategory.Vehicles;
        public override int Priority => 10;
        public override bool BlocksPathing => false;

        protected override string GetDisplayName() => GetVehicleName(TransportationId, MessageId);
        protected override string GetEntityTypeName() => "Vehicle";

        /// <summary>
        /// Gets a human-readable vehicle name for the given transportation ID.
        /// </summary>
        public static string GetVehicleName(int id, string messageId = null)
        {
            // Try MessageId first for localized name
            if (!string.IsNullOrEmpty(messageId))
            {
                try
                {
                    var msg = Il2CppLast.Management.MessageManager.Instance?.GetMessage(messageId);
                    if (!string.IsNullOrEmpty(msg))
                        return msg;
                }
                catch { }
            }

            // FF5-specific vehicle names based on TransportationType enum
            switch (id)
            {
                case 2: return "Ship";
                case 3: return "Airship";
                case 6: return "Submarine";
                case 7: return "Wind Drake";
                case 9: return "Chocobo";
                case 10: return "Black Chocobo";
                default: return $"Vehicle {id}";
            }
        }
    }
}
