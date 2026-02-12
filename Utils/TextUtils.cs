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
        private static readonly Regex IconMarkupRegex = new Regex(
            @"<[iI][cC]_[^>]+>",
            RegexOptions.Compiled);

        private static readonly Regex RichTextTagRegex = new Regex(
            @"<[^>]+>",
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
        /// Normalizes whitespace in text: replaces newlines with spaces,
        /// collapses multiple spaces into one, and trims.
        /// </summary>
        public static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace("\n", " ").Replace("\r", " ").Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text;
        }

        /// <summary>
        /// Strips all Unity rich text / XML-style tags from a string.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return RichTextTagRegex.Replace(text, string.Empty);
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

        /// <summary>
        /// Finds the "Content" transform under a ScrollView/Viewport hierarchy.
        /// </summary>
        public static Transform FindContentList(Transform root)
        {
            var content = FindTransformInChildren(root, "Content");
            if (content != null && content.parent != null &&
                (content.parent.name == "Viewport" || content.parent.parent?.name == "Scroll View"))
            {
                return content;
            }
            return null;
        }

        /// <summary>
        /// Safely gets text from a Text component, returning null if null/empty/whitespace.
        /// </summary>
        public static string GetTextSafe(UnityEngine.UI.Text textComponent)
        {
            if (textComponent == null)
                return null;

            try
            {
                string text = textComponent.text;
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                return text.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
