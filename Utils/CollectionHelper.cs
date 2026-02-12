using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Shared sorting utilities for entity and waypoint lists.
    /// Replaces duplicate SortByDistance implementations in EntityNavigator and WaypointNavigator.
    /// </summary>
    public static class CollectionHelper
    {
        /// <summary>
        /// Returns a new list sorted by distance from a reference position.
        /// Uses tuple-based sorting (no LINQ) for efficiency.
        /// </summary>
        public static List<T> SortByDistance<T>(List<T> items, Vector3 referencePos, Func<T, Vector3> getPosition)
        {
            var withDistances = new List<(T item, float distance)>(items.Count);
            foreach (var item in items)
            {
                withDistances.Add((item, Vector3.Distance(getPosition(item), referencePos)));
            }

            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));

            var result = new List<T>(withDistances.Count);
            foreach (var entry in withDistances)
            {
                result.Add(entry.item);
            }
            return result;
        }

        /// <summary>
        /// Sorts a list in-place by distance from a reference position.
        /// Returns the new index of the preserveItem, or -1 if not found.
        /// </summary>
        public static int SortByDistanceInPlace<T>(List<T> items, Vector3 referencePos, Func<T, Vector3> getPosition, T preserveItem)
        {
            var withDistances = new List<(T item, float distance)>(items.Count);
            foreach (var item in items)
            {
                withDistances.Add((item, Vector3.Distance(getPosition(item), referencePos)));
            }

            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));

            int preservedIndex = -1;
            for (int i = 0; i < withDistances.Count; i++)
            {
                items[i] = withDistances[i].item;
                if (preserveItem != null && EqualityComparer<T>.Default.Equals(withDistances[i].item, preserveItem))
                {
                    preservedIndex = i;
                }
            }

            return preservedIndex;
        }
    }
}
