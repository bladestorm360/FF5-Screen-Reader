using FFV_ScreenReader.Utils;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;

namespace FFV_ScreenReader.Core.Filters
{
    public class FilterContext
    {
        public Vector3 PlayerPosition { get; set; }
        
        public FieldPlayerController PlayerController { get; set; }
        
        public IMapAccessor MapHandle { get; set; }
        
        public FieldPlayer FieldPlayer { get; set; }
        
        public FilterContext()
        {
            PlayerController = GameObjectCache.Get<FieldPlayerController>();

            if (PlayerController?.fieldPlayer != null)
            {
                FieldPlayer = PlayerController.fieldPlayer;
                PlayerPosition = FieldPlayer.transform.position;
                MapHandle = PlayerController.mapHandle;
            }
            else
            {
                PlayerPosition = Vector3.zero;
            }
        }
        
        public FilterContext(FieldPlayerController controller)
        {
            PlayerController = controller;

            if (controller?.fieldPlayer != null)
            {
                FieldPlayer = controller.fieldPlayer;
                PlayerPosition = FieldPlayer.transform.position;
                MapHandle = controller.mapHandle;
            }
            else
            {
                PlayerPosition = Vector3.zero;
            }
        }
    }
}