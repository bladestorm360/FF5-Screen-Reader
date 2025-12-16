using System.Collections.Generic;
using System.Linq;
using FFV_ScreenReader.Field;
using UnityEngine;

namespace FFV_ScreenReader.Core.Filters
{
    public class MapExitGroupingStrategy : IGroupingStrategy
    {
        public string Name => "Map Exit Grouping";
        
        public string GetGroupKey(NavigableEntity entity)
        {
            if (entity is MapExitEntity exit)
            {
                int destinationMapId = exit.DestinationMapId;
                if (destinationMapId > 0)
                {
                    return $"MapExit_{destinationMapId}";
                }
            }

            return null;
        }
        
        public NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos)
        {
            if (members == null || members.Count == 0)
                return null;
            
            return members
                .OrderBy(m => Vector3.Distance(m.Position, playerPos))
                .FirstOrDefault();
        }
    }
}
