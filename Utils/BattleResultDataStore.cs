using System.Collections.Generic;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Centralized static store for battle result data.
    /// Written by BattleResultPatches, read by BattleResultNavigator.
    ///
    /// The victory screen is a sequence of pages (totals, then one level-up page per
    /// character, then abilities, then items). Each page is stored as its own grid in
    /// <see cref="Pages"/>, in the order the game showed it, so the navigator can page
    /// through them individually instead of flattening everything into one list.
    /// </summary>
    public static class BattleResultDataStore
    {
        /// <summary>
        /// One reviewable result page: a titled grid of rows x columns.
        /// A page with no columns is a flat list — only the row headers carry text.
        /// </summary>
        public class ResultPage
        {
            public string Title;
            public string[] RowHeaders;
            public string[] ColHeaders;
            public string[,] Cells;

            /// <summary>
            /// Optional per-row spoken summary, used instead of joining every cell to its
            /// column header. Lets a page phrase itself the way the game draws it
            /// ("HP: 44 &gt; 53 (9)") rather than reading three column names in one breath.
            /// Null means fall back to the generic join.
            /// </summary>
            public string[] RowSummaries;
        }

        private static readonly List<ResultPage> pages = new List<ResultPage>();

        /// <summary>
        /// Result pages in the order the game presented them.
        /// </summary>
        public static IReadOnlyList<ResultPage> Pages => pages;

        /// <summary>
        /// Whether any result data is available (used to determine BattleResult context).
        /// </summary>
        public static bool HasData => pages.Count > 0;

        /// <summary>
        /// Appends a grid page. Ignores pages with no rows so the navigator never lands
        /// on an empty screen.
        /// </summary>
        public static void AddPage(string title, string[] rowHeaders, string[] colHeaders, string[,] cells,
                                   string[] rowSummaries = null)
        {
            if (rowHeaders == null || rowHeaders.Length == 0) return;

            pages.Add(new ResultPage
            {
                Title = title,
                RowHeaders = rowHeaders,
                ColHeaders = colHeaders ?? new string[0],
                Cells = cells,
                RowSummaries = rowSummaries
            });
        }

        /// <summary>
        /// Appends a flat, column-less page built from a list of lines.
        /// </summary>
        public static void AddListPage(string title, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            AddPage(title, lines.ToArray(), new string[0], new string[lines.Count, 0]);
        }

        /// <summary>
        /// Clears all stored data. Call when battle results are dismissed.
        /// </summary>
        public static void Clear()
        {
            pages.Clear();
        }
    }
}
