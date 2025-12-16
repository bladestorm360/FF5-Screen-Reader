using FFV_ScreenReader.Field;
using UnityEngine;

namespace FFV_ScreenReader.Core.Filters
{
    public class PathfindingFilter : IEntityFilter
    {
        private bool isEnabled = false;
        
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (value != isEnabled)
                {
                    isEnabled = value;
                    if (value)
                        OnEnabled();
                    else
                        OnDisabled();
                }
            }
        }
        
        public string Name => "Pathfinding Filter";
        
        public FilterTiming Timing => FilterTiming.OnCycle;
        
        public bool PassesFilter(NavigableEntity entity, FilterContext context)
        {
            if (!IsEntityValid(entity))
                return false;

            if (context.PlayerController?.fieldPlayer == null)
                return false;
            
            Vector3 playerPos = context.PlayerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            var pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                context.MapHandle,
                context.FieldPlayer
            );

            return pathInfo.Success;
        }
        
        public void OnEnabled()
        {
        }
        
        public void OnDisabled()
        {
        }
        
        private bool IsEntityValid(NavigableEntity entity)
        {
            if (entity?.GameEntity == null)
                return false;

            try
            {
                if (entity.GameEntity.gameObject == null || !entity.GameEntity.gameObject.activeInHierarchy)
                    return false;
                
                if (entity.GameEntity.transform == null)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
