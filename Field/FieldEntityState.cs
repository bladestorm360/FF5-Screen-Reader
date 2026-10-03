using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using ObjectType = Il2Cpp.MapConstants.ObjectType;

namespace FFV_ScreenReader.Field
{
    /// <summary>
    /// Presence and purpose checks for field entities, shared by entity creation, the liveness
    /// prune and the pathfinding filter.
    /// </summary>
    internal static class FieldEntityState
    {
        // FieldController.ChangeTransportationSwitchEntity caches with FieldEntity.CacheActive(4)
        private const int VehicleSwitchCacheBit = 1 << 4;

        // PropertyEvent.ActionId in the map data (Tiled action_id): 0 = nothing happens
        private const int ActionNone = 0;

        /// <summary>
        /// Active in the scene, or hidden by the game only because the player is not in the
        /// vehicle the entity is for.
        /// </summary>
        public static bool IsPresent(FieldEntity entity) => IsActive(entity) || IsHiddenByVehicle(entity);

        public static bool IsActive(FieldEntity entity)
        {
            try
            {
                var go = entity?.gameObject;
                return go != null && go.activeInHierarchy;
            }
            catch { return false; } // destroyed entity
        }

        /// <summary>
        /// FieldController.ChangeTransportationSwitchEntity runs for every entity on each
        /// SetEventEntityGroup and ChangeTransportation. For an entity whose
        /// Property.TargetTransportationIdList does not hold the current transportation it calls
        /// RestoreCacheActive(4), CacheActive(4) (bit 4 of cacheActiveEnable / cacheActiveFlag
        /// records the object's own active state) and Hide(0), which is GameObject.SetActive(false).
        /// So vehicle-only triggers (the catapult pad, the Rift by black chocobo) are inactive
        /// whenever the player is on foot or in another vehicle, and an activeInHierarchy check
        /// never listed them. True when the entity is inactive for that reason alone.
        /// </summary>
        public static bool IsHiddenByVehicle(FieldEntity entity)
        {
            try
            {
                var go = entity?.gameObject;
                if (go == null || go.activeSelf)
                    return false;
                var parent = entity.transform.parent;
                if (parent != null && !parent.gameObject.activeInHierarchy)
                    return false;
                var ids = entity.Property?.TargetTransportationIdList;
                if (ids == null || ids.Count == 0)
                    return false;
                return (entity.cacheActiveEnable & VehicleSwitchCacheBit) != 0
                    && (entity.cacheActiveFlag & VehicleSwitchCacheBit) != 0;
            }
            catch { return false; } // destroyed entity or IL2CPP access failure
        }

        /// <summary>
        /// An event, map object or vehicle trigger that does nothing when the player checks or
        /// touches it: no action, no script, no message. These are scenery and cutscene markers
        /// (door collisions, bed parts, speech bubbles, castle sprites on the world map) and are
        /// not listed.
        /// Anything with an action, a script or a message stays.
        /// </summary>
        public static bool IsScenery(FieldEntity entity)
        {
            try
            {
                var property = entity?.Property;
                if (property == null)
                    return false;
                switch ((ObjectType)property.ObjectType)
                {
                    case ObjectType.Event:
                    case ObjectType.Entity:
                    case ObjectType.AnimEntity:
                    case ObjectType.TransportationEventAction:
                    case ObjectType.RandomEvent:
                        break;
                    default:
                        return false;
                }
                if (property.TryCast<PropertyTransportation>() != null)
                    return false; // a vehicle's own map object
                var ev = property.TryCast<PropertyEvent>();
                if (ev == null || ev.ActionId != ActionNone || ev.ScriptId != 0)
                    return false;
                return string.IsNullOrEmpty(ev.TryCast<Il2Cpp.PropertyTalk>()?.MessageKey);
            }
            catch { return false; } // IL2CPP cast can fail on a destroyed entity
        }
    }
}
