namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Helper for SelectContent/SetCursor patch boilerplate.
    /// Consolidates common list validation and item retrieval patterns.
    /// </summary>
    public static class SelectContentHelper
    {
        /// <summary>
        /// Validates index bounds and retrieves item from an IL2CPP List.
        /// Returns null if list is null, empty, index out of bounds, or item is null.
        /// </summary>
        public static T TryGetItem<T>(Il2CppSystem.Collections.Generic.List<T> list, int index) where T : class
        {
            if (list == null || list.Count == 0)
                return null;
            if (index < 0 || index >= list.Count)
                return null;
            return list[index];
        }

        /// <summary>
        /// Validates that an instance and cursor are not null, then extracts index from cursor.
        /// Returns -1 if validation fails.
        /// </summary>
        public static int GetCursorIndex(object instance, Il2CppLast.UI.Cursor cursor)
        {
            if (instance == null || cursor == null)
                return -1;
            return cursor.Index;
        }

        /// <summary>
        /// Validates instance is not null and index is non-negative.
        /// </summary>
        public static bool ValidateBasic(object instance, int index)
        {
            return instance != null && index >= 0;
        }
    }
}
