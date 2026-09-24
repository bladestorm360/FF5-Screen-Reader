using System;
using MelonLoader;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Centralized preferences management for the FFV Screen Reader mod.
    /// All MelonPreferences entries live here with public getters and setter methods.
    /// </summary>
    public static class PreferencesManager
    {
        private static MelonPreferences_Category prefsCategory;

        // Toggle preferences
        private static MelonPreferences_Entry<bool> prefPathfindingFilter;
        private static MelonPreferences_Entry<bool> prefMapExitFilter;
        private static MelonPreferences_Entry<bool> prefToLayerFilter;
        private static MelonPreferences_Entry<bool> prefWallTones;
        private static MelonPreferences_Entry<bool> prefFootsteps;
        private static MelonPreferences_Entry<bool> prefAudioBeacons;
        private static MelonPreferences_Entry<bool> prefLandingPings;
        private static MelonPreferences_Entry<bool> prefExpCounter;
        private static MelonPreferences_Entry<bool> prefStickClickNormalization;
        private static MelonPreferences_Entry<bool> prefAnnounceOnBeaconRestart;
        private static MelonPreferences_Entry<bool> prefMenuPositionAnnouncements;
        private static MelonPreferences_Entry<bool> prefAutoDetail;
        private static MelonPreferences_Entry<bool> prefEnemyLetters;

        // Volume preferences (0-100, default 50)
        private static MelonPreferences_Entry<int> prefWallBumpVolume;
        private static MelonPreferences_Entry<int> prefFootstepVolume;
        private static MelonPreferences_Entry<int> prefWallToneVolume;
        private static MelonPreferences_Entry<int> prefBeaconVolume;
        private static MelonPreferences_Entry<int> prefLandingPingVolume;
        private static MelonPreferences_Entry<int> prefExpCounterVolume;

        // Enemy HP display mode (0=Numbers, 1=Percentage, 2=Hidden)
        private static MelonPreferences_Entry<int> prefEnemyHPDisplay;

        /// <summary>
        /// Initialize all preferences. Call once during OnInitializeMelon.
        /// </summary>
        public static void Initialize()
        {
            prefsCategory = MelonPreferences.CreateCategory("FFV_ScreenReader");

            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");
            prefMapExitFilter = prefsCategory.CreateEntry<bool>("MapExitFilter", false, "Map Exit Filter", "Filter multiple map exits to the same destination, showing only the closest one");
            prefToLayerFilter = prefsCategory.CreateEntry<bool>("ToLayerFilter", false, "Layer Transition Filter", "Hide layer transition entities from navigation list");
            prefWallTones = prefsCategory.CreateEntry<bool>("WallTones", false, "Wall Tones", "Play directional tones when approaching walls");
            prefFootsteps = prefsCategory.CreateEntry<bool>("Footsteps", false, "Footsteps", "Play click sound on each tile movement");
            prefAudioBeacons = prefsCategory.CreateEntry<bool>("AudioBeacons", false, "Audio Beacons", "Play periodic pings toward the selected entity");
            prefLandingPings = prefsCategory.CreateEntry<bool>("LandingPings", false, "Landing Pings", "Play directional pings indicating nearby landable tiles when on ship");
            prefExpCounter = prefsCategory.CreateEntry<bool>("ExpCounter", true, "EXP Counter Sound", "Play rapid beeping while EXP bar animates on battle results");
            prefStickClickNormalization = prefsCategory.CreateEntry<bool>("StickClickNormalization", false, "Stick Click Normalization", "When on, R3/L3 pass through to the game (encounter toggle / dash); mod functions move to Mod Mode (Back/Select + R3/L3).");
            prefAnnounceOnBeaconRestart = prefsCategory.CreateEntry<bool>("AnnounceOnBeaconRestart", false, "Beacon Destination Announcement", "Re-speak the current destination when the beacon is restarted");
            prefMenuPositionAnnouncements = prefsCategory.CreateEntry<bool>("MenuPositionAnnouncements", true, "Menu Position Announcements", "Append the cursor's position in a list when navigating menus, e.g. (3 of 12)");
            prefAutoDetail = prefsCategory.CreateEntry<bool>("AutoDetail", true, "Auto Detail", "Automatically announce descriptions/stats on focus for items, magic, equipment, and shops (same as the on-demand details key)");
            prefEnemyLetters = prefsCategory.CreateEntry<bool>("EnemyLetters", false, "Enemy Letters", "Append A, B, C to battle targets that share a name, so duplicates can be told apart (useful when Enemy HP Display is Hidden)");

            prefWallBumpVolume = prefsCategory.CreateEntry<int>("WallBumpVolume", 50, "Wall Bump Volume", "Volume for wall bump sounds (0-100)");
            prefFootstepVolume = prefsCategory.CreateEntry<int>("FootstepVolume", 50, "Footstep Volume", "Volume for footstep sounds (0-100)");
            prefWallToneVolume = prefsCategory.CreateEntry<int>("WallToneVolume", 50, "Wall Tone Volume", "Volume for wall proximity tones (0-100)");
            prefBeaconVolume = prefsCategory.CreateEntry<int>("BeaconVolume", 50, "Beacon Volume", "Volume for audio beacon pings (0-100)");
            prefLandingPingVolume = prefsCategory.CreateEntry<int>("LandingPingVolume", 50, "Landing Ping Volume", "Volume for landing ping tones (0-100)");
            prefExpCounterVolume = prefsCategory.CreateEntry<int>("ExpCounterVolume", 50, "EXP Counter Volume", "Volume for EXP counter beep (0-100)");

            prefEnemyHPDisplay = prefsCategory.CreateEntry<int>("EnemyHPDisplay", 0, "Enemy HP Display", "0=Numbers, 1=Percentage, 2=Hidden");
            // No multi-hit damage setting: FF5's calc results never carry a hit count (see
            // docs/debug.md, "Correction (2026-09-23, session 2)"), so damage is always the total.
        }

        #region Toggle Getters (saved preference values — single source of truth)

        public static bool PathfindingFilterEnabled => prefPathfindingFilter?.Value ?? false;
        public static bool MapExitFilterEnabled => prefMapExitFilter?.Value ?? false;
        public static bool ToLayerFilterEnabled => prefToLayerFilter?.Value ?? false;
        public static bool WallTonesEnabled => prefWallTones?.Value ?? false;
        public static bool FootstepsEnabled => prefFootsteps?.Value ?? false;
        public static bool AudioBeaconsEnabled => prefAudioBeacons?.Value ?? false;
        public static bool LandingPingsEnabled => prefLandingPings?.Value ?? false;
        public static bool ExpCounterEnabled => prefExpCounter?.Value ?? true;
        public static bool StickClickNormalizationEnabled => prefStickClickNormalization?.Value ?? false;
        public static bool AnnounceOnBeaconRestartEnabled => prefAnnounceOnBeaconRestart?.Value ?? false;
        public static bool MenuPositionAnnouncementsEnabled => prefMenuPositionAnnouncements?.Value ?? true;
        public static bool AutoDetailEnabled => prefAutoDetail?.Value ?? true;
        public static bool EnemyLettersEnabled => prefEnemyLetters?.Value ?? false;

        #endregion

        #region Volume Getters

        public static int WallBumpVolume => prefWallBumpVolume?.Value ?? 50;
        public static int FootstepVolume => prefFootstepVolume?.Value ?? 50;
        public static int WallToneVolume => prefWallToneVolume?.Value ?? 50;
        public static int BeaconVolume => prefBeaconVolume?.Value ?? 50;
        public static int LandingPingVolume => prefLandingPingVolume?.Value ?? 50;
        public static int ExpCounterVolume => prefExpCounterVolume?.Value ?? 50;
        public static int EnemyHPDisplay => prefEnemyHPDisplay?.Value ?? 0;

        #endregion

        #region Setters (with clamping + auto-save)

        /// <summary>
        /// Clamps and stores an int preference, then persists. Shared by all int setters.
        /// </summary>
        private static void SetIntPreference(MelonPreferences_Entry<int> pref, int value, int min, int max)
        {
            if (pref != null)
            {
                pref.Value = Math.Clamp(value, min, max);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetWallBumpVolume(int value) => SetIntPreference(prefWallBumpVolume, value, 0, 100);
        public static void SetFootstepVolume(int value) => SetIntPreference(prefFootstepVolume, value, 0, 100);
        public static void SetWallToneVolume(int value) => SetIntPreference(prefWallToneVolume, value, 0, 100);
        public static void SetBeaconVolume(int value) => SetIntPreference(prefBeaconVolume, value, 0, 100);
        public static void SetLandingPingVolume(int value) => SetIntPreference(prefLandingPingVolume, value, 0, 100);
        public static void SetExpCounterVolume(int value) => SetIntPreference(prefExpCounterVolume, value, 0, 100);
        public static void SetEnemyHPDisplay(int value) => SetIntPreference(prefEnemyHPDisplay, value, 0, 2);

        #endregion

        #region Toggle Persistence Helpers

        /// <summary>
        /// Saves a boolean toggle to its preference entry.
        /// </summary>
        public static void SavePathfindingFilter(bool value)
        {
            if (prefPathfindingFilter != null) { prefPathfindingFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveMapExitFilter(bool value)
        {
            if (prefMapExitFilter != null) { prefMapExitFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveToLayerFilter(bool value)
        {
            if (prefToLayerFilter != null) { prefToLayerFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveWallTones(bool value)
        {
            if (prefWallTones != null) { prefWallTones.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveFootsteps(bool value)
        {
            if (prefFootsteps != null) { prefFootsteps.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveAudioBeacons(bool value)
        {
            if (prefAudioBeacons != null) { prefAudioBeacons.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveLandingPings(bool value)
        {
            if (prefLandingPings != null) { prefLandingPings.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveExpCounter(bool value)
        {
            if (prefExpCounter != null) { prefExpCounter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveStickClickNormalization(bool value)
        {
            if (prefStickClickNormalization != null) { prefStickClickNormalization.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveAnnounceOnBeaconRestart(bool value)
        {
            if (prefAnnounceOnBeaconRestart != null) { prefAnnounceOnBeaconRestart.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveEnemyLetters(bool value)
        {
            if (prefEnemyLetters != null) { prefEnemyLetters.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveMenuPositionAnnouncements(bool value)
        {
            if (prefMenuPositionAnnouncements != null) { prefMenuPositionAnnouncements.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveAutoDetail(bool value)
        {
            if (prefAutoDetail != null) { prefAutoDetail.Value = value; prefsCategory?.SaveToFile(false); }
        }

        #endregion
    }
}
