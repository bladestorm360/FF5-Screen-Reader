using UnityEngine;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Unified 8-point compass direction calculation.
    /// Replaces duplicate implementations in NavigableEntity.GetDirection
    /// and FieldNavigationHelper.GetCardinalDirectionName.
    /// Uses Atan2 for consistent 45-degree sector boundaries.
    /// </summary>
    public static class DirectionHelper
    {
        /// <summary>
        /// Gets the compass direction name from one point to another.
        /// </summary>
        public static string GetCompassDirection(Vector3 from, Vector3 to)
        {
            Vector3 diff = to - from;
            return GetCompassDirectionFromVector(diff);
        }

        /// <summary>
        /// Gets the localized compass direction name from a direction vector (does not need to be
        /// normalized). Speech-only: never compare the result against a literal.
        /// </summary>
        public static string GetCompassDirectionFromVector(Vector3 dir)
        {
            if (Mathf.Approximately(dir.x, 0f) && Mathf.Approximately(dir.y, 0f))
                return T("Unknown");

            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            if (angle < 0) angle += 360;

            if (angle >= 337.5f || angle < 22.5f) return T("North");
            if (angle < 67.5f) return T("Northeast");
            if (angle < 112.5f) return T("East");
            if (angle < 157.5f) return T("Southeast");
            if (angle < 202.5f) return T("South");
            if (angle < 247.5f) return T("Southwest");
            if (angle < 292.5f) return T("West");
            if (angle < 337.5f) return T("Northwest");
            return T("Unknown");
        }
    }
}
