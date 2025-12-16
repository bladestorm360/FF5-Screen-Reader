using System.Collections.Generic;
using FFV_ScreenReader.Field;
using UnityEngine;

namespace FFV_ScreenReader.Core.Filters
{
    public interface IGroupingStrategy
    {
        string Name { get; }
        
        string GetGroupKey(NavigableEntity entity);
        
        NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos);
    }
}
