namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Captures the current navigation toggle state for save/restore during battle or dialogue.
    /// Replaces manual 5-boolean save/restore pattern.
    /// </summary>
    public struct NavigationStateSnapshot
    {
        public bool WallTones;
        public bool Footsteps;
        public bool AudioBeacons;
        public bool LandingPings;
        public bool PathfindingFilter;

        /// <summary>
        /// Captures the current state from AudioLoopManager and the mod's pathfinding filter.
        /// </summary>
        public static NavigationStateSnapshot Capture(AudioLoopManager audioLoopManager)
        {
            return new NavigationStateSnapshot
            {
                WallTones = audioLoopManager?.IsWallTonesEnabled ?? false,
                Footsteps = audioLoopManager?.IsFootstepsEnabled ?? false,
                AudioBeacons = audioLoopManager?.IsAudioBeaconsEnabled ?? false,
                LandingPings = audioLoopManager?.IsLandingPingsEnabled ?? false,
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
                mod.RestoreNavigationAfterBattle(WallTones, Footsteps, AudioBeacons, PathfindingFilter, LandingPings);
            }
            else
            {
                audioLoopManager?.RestoreNavigationAfterBattle(WallTones, Footsteps, AudioBeacons, PathfindingFilter, LandingPings);
            }
        }
    }
}
