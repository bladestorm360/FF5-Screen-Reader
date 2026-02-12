using UnityEngine;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Centralized player position retrieval.
    /// Replaces duplicate GetPlayerPosition() implementations across EntityNavigator,
    /// EntityCache, WaypointNavigator, and GroupEntity.
    /// </summary>
    public static class PlayerPositionHelper
    {
        /// <summary>
        /// Gets the player's world position (transform.position).
        /// Used by EntityNavigator, EntityCache, GroupEntity for distance sorting.
        /// </summary>
        public static Vector3 GetWorldPosition()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer?.transform == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.position;
        }

        /// <summary>
        /// Gets the player's local position (transform.localPosition).
        /// Used by WaypointNavigator for waypoint coordinate space.
        /// </summary>
        public static Vector3 GetLocalPosition()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer?.transform == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.localPosition;
        }
    }
}
