using System;
using System.Collections.Generic;
using Il2CppLast.Entity.Field;
using UnityEngine;
using Il2CppLast.Map;
using Il2Cpp;
using MelonLoader;

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

            // CHECK VEHICLE TYPE MAP FIRST - vehicles may not have distinctive ObjectType
            if (FieldNavigationHelper.VehicleTypeMap.TryGetValue(fieldEntity, out var vehicleInfo))
            {
                // Filter out non-vehicle types (TRANSPORT_NONE=0, TRANSPORT_PLAYER=1, TRANSPORT_SYMBOL=4, TRANSPORT_CONTENT=5)
                if (vehicleInfo.Type > 1 && vehicleInfo.Type != 4 && vehicleInfo.Type != 5)
                {
                    return new VehicleEntity
                    {
                        GameEntity = fieldEntity,
                        TransportationId = vehicleInfo.Type,
                        MessageId = vehicleInfo.MessageId
                    };
                }
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
                   objectType == MapConstants.ObjectType.OpenTrigger ||  // Door/trigger (filtered, use DoorTriggerEntity for specific cases)
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

        /// <summary>
        /// Checks if an entity name indicates a placeholder/generic entity that should be filtered.
        /// </summary>
        private static bool IsPlaceholderEntity(string entityName)
        {
            if (string.IsNullOrEmpty(entityName)) return true;
            if (entityName == "Unknown") return true;

            string normalized = NormalizeToHalfWidth(entityName);
            if (normalized.StartsWith("Default ") || normalized.StartsWith("Default_"))
                return true;
            if (entityName.StartsWith("汎用")) // Generic prefix in Japanese
                return true;

            return false;
        }

        /// <summary>
        /// Converts full-width ASCII characters to half-width for consistent comparison.
        /// </summary>
        private static string NormalizeToHalfWidth(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                // Convert full-width ASCII (U+FF01-U+FF5E) to half-width (U+0021-U+007E)
                if (c >= '\uFF01' && c <= '\uFF5E')
                {
                    result.Append((char)(c - 0xFEE0));
                }
                // Convert full-width space (U+3000) to regular space
                else if (c == '\u3000')
                {
                    result.Append(' ');
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
        
        /// <summary>
        /// Checks if a string contains Japanese characters (Hiragana, Katakana, or CJK Unified Ideographs).
        /// </summary>
        private static bool ContainsJapaneseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= '\u3040' && c <= '\u309F') ||  // Hiragana
                    (c >= '\u30A0' && c <= '\u30FF') ||  // Katakana
                    (c >= '\u4E00' && c <= '\u9FFF'))     // CJK Unified Ideographs
                    return true;
            }
            return false;
        }

        private static NavigableEntity CreateEntityByType(
            FieldEntity fieldEntity,
            MapConstants.ObjectType objectType)
        {
            string entityName = fieldEntity?.Property?.Name;

            switch (objectType)
            {
                case MapConstants.ObjectType.TreasureBox:
                    return new TreasureChestEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.NPC:
                case MapConstants.ObjectType.ShopNPC:
                    // Filter placeholder NPCs
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new NPCEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.GotoMap:
                    // Filter placeholder map exits
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new MapExitEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.SavePoint:
                    return new SavePointEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.ToLayer:
                case MapConstants.ObjectType.TelepoPoint:
                case MapConstants.ObjectType.TransportationEventAction:
                    // Filter placeholder events
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new EventEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.AnimEntity:
                    // AnimEntity with save-point name is a visual duplicate — filter it
                    if (entityName != null && entityName.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0)
                        return null;
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new EventEntity { GameEntity = fieldEntity };

                case MapConstants.ObjectType.Event:
                case MapConstants.ObjectType.RandomEvent:
                    string evtGoName = fieldEntity.gameObject?.name ?? "";
                    // Filter save-point-related Event duplicates (Japanese-named internal copy)
                    if (evtGoName.IndexOf("SavePoint", StringComparison.Ordinal) >= 0)
                        return null;
                    // Filter Japanese save point events (duplicate of SavePointEntity)
                    if (entityName != null && entityName.IndexOf("セーブポイント", StringComparison.Ordinal) >= 0)
                        return null;
                    // Filter placeholder events
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new EventEntity { GameEntity = fieldEntity };

                default:
                    // Filter any remaining placeholder entities
                    if (IsPlaceholderEntity(entityName))
                        return null;
                    return new EventEntity { GameEntity = fieldEntity };
            }
        }
    }
}