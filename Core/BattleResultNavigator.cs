using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Virtual navigable window for reviewing battle result data (no window focus stealing).
    /// Game input is suppressed via ControllerRouter.SuppressGameInput + InputPassthroughPatches
    /// while IsOpen. Keys are read through GamepadManager (SDL3 + GetAsyncKeyState), alongside
    /// direct SDL controller button/stick reads.
    ///
    /// The victory screen is a sequence of pages, and so is this navigator: PageUp/PageDown
    /// (or L1/R1) moves between the pages BattleResultDataStore collected, so each character's
    /// level-up page can be read on its own rather than merged with the others.
    /// </summary>
    public static class BattleResultNavigator
    {
        public static bool IsOpen { get; private set; }

        // Navigation state
        private static int currentPage;
        private static int currentRow;
        private static int currentCol;

        // Set by Open(), cleared by the first HandleInput() after it. GamepadManager's
        // IsButtonPressed is pure edge detection and does NOT consult
        // ControllerRouter.consumedButtons, so the button that opened this window is still
        // reading as "pressed" when HandleInput runs later in the SAME frame. Circle opens it
        // from mod mode and Circle also closes it, so without this the window would open and
        // shut on one press. Owning that edge is not deduplication — the press has already
        // been acted on.
        private static bool swallowInputThisFrame;

        private static BattleResultDataStore.ResultPage Page
        {
            get
            {
                var pages = BattleResultDataStore.Pages;
                if (currentPage < 0 || currentPage >= pages.Count) return null;
                return pages[currentPage];
            }
        }

        /// <summary>
        /// Opens the navigator on the page currently on screen — the most recently added,
        /// since pages are appended as their phase fires.
        /// </summary>
        public static void Open()
        {
            if (IsOpen) return;

            if (!BattleResultDataStore.HasData)
            {
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
                return;
            }

            IsOpen = true;
            swallowInputThisFrame = true;
            currentPage = BattleResultDataStore.Pages.Count - 1;
            currentRow = 0;
            currentCol = 0;

            // Announce title after delay
            CoroutineManager.StartManaged(AnnounceOpenDelayed());
        }

        /// <summary>
        /// Closes the navigator. Game input resumes automatically —
        /// ControllerRouter.SuppressGameInput becomes false once IsOpen is cleared.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            swallowInputThisFrame = false;
            currentPage = 0;
            currentRow = 0;
            currentCol = 0;
        }

        /// <summary>
        /// Handles input when the navigator is open.
        /// Returns true if input was consumed.
        /// Accepts both keyboard (GetAsyncKeyState via WindowsFocusHelper) and
        /// SDL controller (GamepadManager). The router skips its own routing when
        /// IsOpen, so gamepad polling here is the sole consumer of those buttons.
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // The press that opened this window is still live this frame — consume it.
            if (swallowInputThisFrame)
            {
                swallowInputThisFrame = false;
                return true;
            }

            // Close: Escape OR B (EAST) OR Start
            if (GamepadManager.IsKeyCodePressed(KeyCode.Escape)
                || GamepadManager.IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_EAST)
                || GamepadManager.IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_START))
            {
                Close();
                return true;
            }

            // Page navigation: PageUp/PageDown OR shoulder buttons
            if (GamepadManager.IsKeyCodePressed(KeyCode.PageUp)
                || GamepadManager.IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER))
            {
                NavigatePage(-1);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.PageDown)
                || GamepadManager.IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER))
            {
                NavigatePage(1);
                return true;
            }

            // Row navigation: arrow keys OR D-pad OR left stick
            if (GamepadManager.IsKeyCodePressed(KeyCode.UpArrow)
                || GamepadManager.DpadUpPressed
                || GamepadManager.LeftStickUpPressed)
            {
                NavigateRow(-1);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.DownArrow)
                || GamepadManager.DpadDownPressed
                || GamepadManager.LeftStickDownPressed)
            {
                NavigateRow(1);
                return true;
            }

            // Column navigation
            if (GamepadManager.IsKeyCodePressed(KeyCode.LeftArrow)
                || GamepadManager.DpadLeftPressed
                || GamepadManager.LeftStickLeftPressed)
            {
                NavigateCol(-1);
                return true;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.RightArrow)
                || GamepadManager.DpadRightPressed
                || GamepadManager.LeftStickRightPressed)
            {
                NavigateCol(1);
                return true;
            }

            // Read full row: Enter/Home OR A (SOUTH)
            if (GamepadManager.IsKeyCodePressed(KeyCode.Return)
                || GamepadManager.IsKeyCodePressed(KeyCode.Home)
                || GamepadManager.IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_SOUTH))
            {
                AnnounceFullRow();
                return true;
            }

            return true; // Consume all input while open
        }

        #region Navigation

        private static void NavigatePage(int delta)
        {
            int count = BattleResultDataStore.Pages.Count;
            if (count <= 1) return;

            currentPage += delta;
            if (currentPage < 0) currentPage = count - 1;
            if (currentPage >= count) currentPage = 0;

            currentRow = 0;
            currentCol = 0;

            FFV_ScreenReaderMod.SpeakText(BuildPageHeaderText(), interrupt: true);
        }

        private static void NavigateRow(int delta)
        {
            var page = Page;
            if (page == null || page.RowHeaders.Length == 0) return;

            currentRow += delta;
            if (currentRow < 0) currentRow = page.RowHeaders.Length - 1;
            if (currentRow >= page.RowHeaders.Length) currentRow = 0;

            FFV_ScreenReaderMod.SpeakText(BuildFullRowText(currentRow), interrupt: true);
        }

        private static void NavigateCol(int delta)
        {
            var page = Page;
            if (page == null || page.ColHeaders.Length == 0) return;

            currentCol += delta;
            if (currentCol < 0) currentCol = page.ColHeaders.Length - 1;
            if (currentCol >= page.ColHeaders.Length) currentCol = 0;

            string header = page.ColHeaders[currentCol];
            string value = page.Cells[currentRow, currentCol];
            FFV_ScreenReaderMod.SpeakText($"{header}: {value}", interrupt: true);
        }

        private static void AnnounceFullRow()
        {
            var page = Page;
            if (page == null || page.RowHeaders.Length == 0) return;
            FFV_ScreenReaderMod.SpeakText(BuildFullRowText(currentRow), interrupt: true);
        }

        /// <summary>
        /// Builds the line spoken when a page is entered: title, position in the sequence
        /// (omitted when there is only one page), then the first row.
        /// </summary>
        private static string BuildPageHeaderText()
        {
            var page = Page;
            if (page == null) return LocalizationHelper.GetModString("no_data");

            int count = BattleResultDataStore.Pages.Count;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(page.Title))
                parts.Add(page.Title);
            if (count > 1)
                parts.Add(string.Format(T("Page {0} of {1}"), currentPage + 1, count));
            if (page.RowHeaders.Length > 0)
                parts.Add(BuildFullRowText(0));

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Builds a full row summary. Pages that supply their own phrasing (the level-up
        /// stat pages, "HP: 44 &gt; 53 (9)") use it verbatim; everything else falls back to
        /// joining each cell to its column header.
        /// </summary>
        private static string BuildFullRowText(int row)
        {
            var page = Page;
            if (page == null) return "";

            if (page.RowSummaries != null && row < page.RowSummaries.Length)
                return page.RowSummaries[row];

            var parts = new List<string>();
            parts.Add(page.RowHeaders[row]);

            for (int c = 0; c < page.ColHeaders.Length; c++)
            {
                // Skip columns this row has no value for ("-"), so the summary doesn't
                // read "- ABP" for a Freelancer in a mixed party, or "- Change" for the
                // job row that has no numeric change. Arrowing onto the column still
                // reports the dash, which is informative when asked for deliberately.
                if (page.Cells[row, c] == "-") continue;

                parts.Add($"{page.Cells[row, c]} {page.ColHeaders[c]}");
            }

            return string.Join(", ", parts);
        }

        private static IEnumerator AnnounceOpenDelayed()
        {
            yield return null;
            yield return null;

            if (IsOpen)
                FFV_ScreenReaderMod.SpeakText(BuildPageHeaderText(), interrupt: false);
        }

        #endregion
    }
}
