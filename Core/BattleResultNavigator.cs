using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Focus-stealing navigable window for reviewing battle result data.
    /// Follows the same pattern as TextInputWindow: steals focus, captures keys via
    /// InputManager, and restores focus on close.
    /// </summary>
    public static class BattleResultNavigator
    {
        public static bool IsOpen { get; private set; }

        // Grid data
        private static string[] rowHeaders;
        private static string[] colHeaders;
        private static string[,] cells;
        private static string title;

        // Navigation state
        private static int currentRow;
        private static int currentCol;

        // Tracked keys
        private static readonly ModKey[] TrackedKeys = {
            ModKey.Escape, ModKey.UpArrow, ModKey.DownArrow,
            ModKey.LeftArrow, ModKey.RightArrow, ModKey.Return, ModKey.Home
        };

        /// <summary>
        /// Opens the navigator with the current result data.
        /// Checks which data is available and builds the appropriate grid.
        /// </summary>
        public static void Open()
        {
            if (IsOpen) return;

            if (!BattleResultDataStore.HasData)
            {
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
                return;
            }

            // Build grid from available data (prefer stats if available, else points)
            if (BattleResultDataStore.HasStatsData)
                BuildStatsGrid();
            else if (BattleResultDataStore.HasPointsData)
                BuildPointsGrid();
            else
            {
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
                return;
            }

            if (rowHeaders == null || rowHeaders.Length == 0)
            {
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
                return;
            }

            IsOpen = true;
            currentRow = 0;
            currentCol = 0;

            // Initialize key states
            InputManager.InitializeKeyStates(TrackedKeys);

            // Steal focus
            InputManager.StealFocus("FFV_BattleResults");

            // Announce title after delay
            CoroutineManager.StartManaged(AnnounceOpenDelayed());
        }

        /// <summary>
        /// Closes the navigator and restores game focus.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            rowHeaders = null;
            colHeaders = null;
            cells = null;
            title = null;

            InputManager.RestoreFocus();
        }

        /// <summary>
        /// Handles input when the navigator is open.
        /// Returns true if input was consumed.
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Poll tracked keys
            InputManager.Poll(TrackedKeys);

            if (InputManager.IsKeyDown(ModKey.Escape))
            {
                Close();
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.UpArrow))
            {
                NavigateRow(-1);
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.DownArrow))
            {
                NavigateRow(1);
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.LeftArrow))
            {
                NavigateCol(-1);
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.RightArrow))
            {
                NavigateCol(1);
                return true;
            }

            if (InputManager.IsKeyDown(ModKey.Return) || InputManager.IsKeyDown(ModKey.Home))
            {
                AnnounceFullRow();
                return true;
            }

            return true; // Consume all input while open
        }

        #region Grid Builders

        private static void BuildPointsGrid()
        {
            var data = BattleResultDataStore.PointsData;
            title = LocalizationHelper.GetModString("battle_results");

            // Columns: EXP, Next (EXP to next level), ABP — matches game screen order
            colHeaders = new[] { "EXP", "Next", "ABP" };
            rowHeaders = new string[data.Count];
            cells = new string[data.Count, 3];

            for (int i = 0; i < data.Count; i++)
            {
                var c = data[i];
                rowHeaders[i] = c.Name;
                cells[i, 0] = c.Exp.ToString("N0");
                cells[i, 1] = c.NextExp > 0 ? c.NextExp.ToString("N0") : "-";
                cells[i, 2] = c.Abp > 0 ? c.Abp.ToString() : "-";
            }
        }

        private static void BuildStatsGrid()
        {
            var data = BattleResultDataStore.StatsData;
            if (data.Count == 0) return;

            string beforeStr = LocalizationHelper.GetModString("before");
            string afterStr = LocalizationHelper.GetModString("after");
            string changeStr = LocalizationHelper.GetModString("change");
            colHeaders = new[] { beforeStr, afterStr, changeStr };

            title = LocalizationHelper.GetModString("battle_results");

            var allRows = new List<string>();
            var allCells = new List<string[]>();

            foreach (var charData in data)
            {
                foreach (var stat in charData.Stats)
                {
                    allRows.Add($"{charData.Name}: {stat.Category}");
                    string diffStr = stat.Diff > 0 ? $"+{stat.Diff}" : stat.Diff.ToString();
                    allCells.Add(new[] { stat.Before, stat.After, diffStr });
                }
            }

            rowHeaders = allRows.ToArray();
            cells = new string[allRows.Count, 3];
            for (int i = 0; i < allRows.Count; i++)
            {
                cells[i, 0] = allCells[i][0];
                cells[i, 1] = allCells[i][1];
                cells[i, 2] = allCells[i][2];
            }
        }

        #endregion

        #region Navigation

        private static void NavigateRow(int delta)
        {
            if (rowHeaders == null || rowHeaders.Length == 0) return;

            currentRow += delta;
            if (currentRow < 0) currentRow = rowHeaders.Length - 1;
            if (currentRow >= rowHeaders.Length) currentRow = 0;

            FFV_ScreenReaderMod.SpeakText(BuildFullRowText(currentRow), interrupt: true);
        }

        private static void NavigateCol(int delta)
        {
            if (colHeaders == null || colHeaders.Length == 0) return;

            currentCol += delta;
            if (currentCol < 0) currentCol = colHeaders.Length - 1;
            if (currentCol >= colHeaders.Length) currentCol = 0;

            string header = colHeaders[currentCol];
            string value = cells[currentRow, currentCol];
            FFV_ScreenReaderMod.SpeakText($"{header}: {value}", interrupt: true);
        }

        private static void AnnounceFullRow()
        {
            if (rowHeaders == null || rowHeaders.Length == 0) return;
            FFV_ScreenReaderMod.SpeakText(BuildFullRowText(currentRow), interrupt: true);
        }

        /// <summary>
        /// Builds a full row summary: "Name, Value1 Header1, Value2 Header2, ..."
        /// </summary>
        private static string BuildFullRowText(int row)
        {
            var parts = new List<string>();
            parts.Add(rowHeaders[row]);

            for (int c = 0; c < colHeaders.Length; c++)
            {
                parts.Add($"{cells[row, c]} {colHeaders[c]}");
            }

            return string.Join(", ", parts);
        }

        private static IEnumerator AnnounceOpenDelayed()
        {
            yield return null;
            yield return null;

            if (IsOpen && title != null)
            {
                // Announce title, then full first row
                string firstRow = rowHeaders != null && rowHeaders.Length > 0 ? BuildFullRowText(0) : "";
                FFV_ScreenReaderMod.SpeakText($"{title}: {firstRow}", interrupt: false);
            }
        }

        #endregion
    }
}
