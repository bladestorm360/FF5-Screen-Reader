using System.Collections.Generic;
using System.Linq;
using FFV_ScreenReader.Field;
using UnityEngine;

namespace FFV_ScreenReader.Core.Filters
{
    /// <summary>
    /// Merges the tiles of one vehicle trigger into a single list entry, at the tile nearest the
    /// player. A TransportationEventAction covers a pad of tiles (each catapult is 3×3, Zeza's
    /// fleet 16 tiles around the ships), each its own map object with the same name. In the map
    /// data every such name occurs as one place per map (FFPR/tools/mapdump), so the name
    /// identifies the place. Always on (user, 2026-10-03).
    /// </summary>
    public class VehiclePadGroupingStrategy : IGroupingStrategy
    {
        public string Name => "Vehicle Pad Grouping";

        public string GetGroupKey(NavigableEntity entity)
        {
            if (entity is EventEntity ev &&
                ev.EventType == Il2Cpp.MapConstants.ObjectType.TransportationEventAction)
            {
                string rawName = ev.GameEntity?.Property?.Name;
                if (!string.IsNullOrEmpty(rawName))
                    return $"VehiclePad_{rawName}";
            }
            return null;
        }

        public NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos)
        {
            if (members == null || members.Count == 0)
                return null;
            // Nearest tile that is still present
            return members
                .Where(m => m.IsAlive)
                .DefaultIfEmpty(members[0])
                .OrderBy(m => Vector3.Distance(m.Position, playerPos))
                .FirstOrDefault();
        }
    }
}
