using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Utility methods for text processing and UI element lookup.
    /// </summary>
    public static class TextUtils
    {
        // Compiled regex for stripping icon markup (e.g., <ic_Drag>, <IC_DRAG>)
        private static readonly Regex IconMarkupRegex = new Regex(
            @"<[iI][cC]_[^>]+>",
            RegexOptions.Compiled);

        /// <summary>
        /// Removes icon markup tags from text (e.g., &lt;ic_Drag&gt;, &lt;IC_DRAG&gt;).
        /// </summary>
        public static string StripIconMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return IconMarkupRegex.Replace(text, "").Trim();
        }

        /// <summary>
        /// Recursively searches for a child Transform with the specified name.
        /// </summary>
        public static Transform FindTransformInChildren(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;

                var found = FindTransformInChildren(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a Text component with the specified GameObject name.
        /// </summary>
        public static UnityEngine.UI.Text FindTextInChildren(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    var text = child.GetComponent<UnityEngine.UI.Text>();
                    if (text != null)
                        return text;
                }

                var found = FindTextInChildren(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Checks if any Text component exists whose GameObject name contains the specified substring.
        /// More efficient than GetComponentsInChildren as it stops on first match.
        /// </summary>
        public static bool HasTextWithNameContaining(Transform parent, string nameContains)
        {
            if (parent == null)
                return false;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name != null && child.name.Contains(nameContains))
                {
                    var text = child.GetComponent<UnityEngine.UI.Text>();
                    if (text != null)
                        return true;
                }

                // Recurse into children
                if (HasTextWithNameContaining(child, nameContains))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Iterates through all Text components in children and invokes the callback for each one.
        /// </summary>
        public static void ForEachTextInChildren(Transform parent, Action<UnityEngine.UI.Text> callback, bool includeInactive = true)
        {
            if (parent == null || callback == null)
                return;

            ForEachTextInChildrenInternal(parent, callback, includeInactive);
        }

        private static void ForEachTextInChildrenInternal(Transform parent, Action<UnityEngine.UI.Text> callback, bool includeInactive)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                var text = child.GetComponent<UnityEngine.UI.Text>();
                if (text != null)
                    callback(text);

                ForEachTextInChildrenInternal(child, callback, includeInactive);
            }
        }

        /// <summary>
        /// Finds the first Text component in children that passes the predicate.
        /// </summary>
        public static UnityEngine.UI.Text FindFirstText(Transform parent, Func<UnityEngine.UI.Text, bool> predicate, bool includeInactive = true)
        {
            if (parent == null || predicate == null)
                return null;

            return FindFirstTextInternal(parent, predicate, includeInactive);
        }

        private static UnityEngine.UI.Text FindFirstTextInternal(Transform parent, Func<UnityEngine.UI.Text, bool> predicate, bool includeInactive)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                var text = child.GetComponent<UnityEngine.UI.Text>();
                if (text != null && predicate(text))
                    return text;

                var found = FindFirstTextInternal(child, predicate, includeInactive);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
