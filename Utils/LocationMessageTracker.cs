using System;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Keeps the location banner from repeating the map announcement. On a map change the mod says
    /// "Entering Tule", then the game fades in its own "Tule" banner (FadeMessageManager.Play); a
    /// banner whose text is contained in the last transition announcement is skipped. Content-based,
    /// no timers. Ported from FF1 without its "no transition yet, looks like a place name" word-count
    /// heuristic, which misfires on languages written without spaces.
    /// </summary>
    public static class LocationMessageTracker
    {
        private static string lastMapTransitionMessage = "";

        /// <summary>Records the "Entering X" announcement. Called by GameStatePatches.CheckMapTransition.</summary>
        public static void SetLastMapTransition(string message)
        {
            lastMapTransitionMessage = message?.Trim() ?? "";
        }

        /// <summary>
        /// False when the banner only repeats the last map transition announcement. A transition
        /// suppresses at most one banner: after that it is forgotten, so a later banner for the same
        /// place (e.g. loading a save on the current map, where no "Entering X" is spoken) still reads.
        /// </summary>
        public static bool ShouldAnnounceFadeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            if (!string.IsNullOrEmpty(lastMapTransitionMessage)
                && lastMapTransitionMessage.Contains(message.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                lastMapTransitionMessage = "";
                return false;
            }
            return true;
        }
    }
}
