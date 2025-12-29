using System;
using System.Collections.Generic;
using Il2CppLast.Entity.Field;
using UnityEngine;
using Il2CppLast.Map;
using Il2Cpp;

namespace FFV_ScreenReader.Field
{
    public static class EntityFactory
    {
        public static NavigableEntity CreateFromFieldEntity(FieldEntity fieldEntity, Vector3 playerPos)
        {
            if (fieldEntity == null || fieldEntity.transform == null)
                return null;
            
            try
            {
                if (fieldEntity.gameObject == null || !fieldEntity.gameObject.activeInHierarchy)
                    return null;
            }
            catch
            {
                return null;
            }
            
            MapConstants.ObjectType objectType = MapConstants.ObjectType.PointIn;
            if (fieldEntity.Property != null)
            {
                objectType = (MapConstants.ObjectType)fieldEntity.Property.ObjectType;
            }
            
            if (IsNonInteractiveType(objectType))
                return null;
            
            NavigableEntity entity = CreateEntityByType(fieldEntity, objectType);

            return entity;
        }

        private static bool IsNonInteractiveType(MapConstants.ObjectType objectType)
        {
            return objectType == MapConstants.ObjectType.PointIn ||
                   objectType == MapConstants.ObjectType.CollisionEntity ||
                   objectType == MapConstants.ObjectType.EffectEntity ||
                   objectType == MapConstants.ObjectType.ScreenEffect ||
                   objectType == MapConstants.ObjectType.TileAnimation ||
                   objectType == MapConstants.ObjectType.MoveArea ||
                   objectType == MapConstants.ObjectType.Polyline ||
                   objectType == MapConstants.ObjectType.ChangeOffset ||
                   objectType == MapConstants.ObjectType.IgnoreRoute ||
                   objectType == MapConstants.ObjectType.NonEncountArea ||
                   objectType == MapConstants.ObjectType.MapRange ||
                   objectType == MapConstants.ObjectType.DamageFloorGimmickArea ||
                   objectType == MapConstants.ObjectType.SlidingFloorGimmickArea ||
                   objectType == MapConstants.ObjectType.TimeSwitchingGimmickArea;
        }
        
        private static NavigableEntity CreateEntityByType(
            FieldEntity fieldEntity,
            MapConstants.ObjectType objectType)
        {
            switch (objectType)
            {
                case MapConstants.ObjectType.TreasureBox:
                    return new TreasureChestEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.NPC:
                case MapConstants.ObjectType.ShopNPC:
                    return new NPCEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.GotoMap:
                    return new MapExitEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.SavePoint:
                    return new SavePointEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.OpenTrigger:
                    return new DoorTriggerEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.TelepoPoint:
                case MapConstants.ObjectType.Event:
                case MapConstants.ObjectType.RandomEvent:
                case MapConstants.ObjectType.TransportationEventAction:
                default:
                    return new EventEntity { GameEntity = fieldEntity };
            }
        }
    }
}