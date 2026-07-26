namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Captures navigation state for save/restore during battle or dialogue.
    ///
    /// This used to carry five booleans, four of which (wall tones, footsteps, audio beacons,
    /// landing pings) were passed to RestoreNavigationAfterBattle and then ignored — that
    /// enabled state moved to PreferencesManager, and the loops re-arm themselves from the
    /// preference once suppression clears. Only the pathfinding filter is genuinely per-battle
    /// state that has to be carried across.
    /// </summary>
    public struct NavigationStateSnapshot
    {
        public bool PathfindingFilter;

        /// <summary>
        /// Captures the current state from the mod's pathfinding filter.
        /// </summary>
        public static NavigationStateSnapshot Capture(AudioLoopManager audioLoopManager)
        {
            return new NavigationStateSnapshot
            {
                PathfindingFilter = FFV_ScreenReaderMod.PathfindingFilterEnabled
            };
        }

        /// <summary>
        /// Restores the captured state via the mod (updates both AudioLoopManager and filter state).
        /// </summary>
        public void RestoreTo(AudioLoopManager audioLoopManager)
        {
            var mod = FFV_ScreenReaderMod.Instance;
            if (mod != null)
            {
                mod.RestoreNavigationAfterBattle(PathfindingFilter);
            }
            else
            {
                audioLoopManager?.RestoreNavigationAfterBattle(PathfindingFilter);
            }
        }
    }
}
