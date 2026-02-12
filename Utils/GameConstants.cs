using UnityEngine;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// FF5-specific game constants (non-audio).
    /// Audio constants are in SoundConstants.
    /// </summary>
    public static class GameConstants
    {
        // Tile geometry
        public const float TILE_SIZE = 16f;
        public const float TILE_SIZE_INVERSE = 0.0625f; // 1/16
        public const float MAP_EXIT_TOLERANCE = 12.0f;

        // Loop intervals (seconds) - used by AudioLoopManager
        public const float WALL_TONE_INTERVAL = 0.1f;
        public const float BEACON_INTERVAL = 2.0f;
        public const float LANDING_PING_INTERVAL = 0.1f;
        public const float SCENE_LOAD_SUPPRESSION = 1.0f;
        public const float VEHICLE_TRANSITION_SUPPRESSION = 0.2f;
        public const float INITIAL_LOOP_DELAY = 0.3f;

        // Audio constants - aliases for backward compatibility
        public const int SAMPLE_RATE = SoundConstants.SAMPLE_RATE;
        public const int WAV_HEADER_SIZE = SoundConstants.WAV_HEADER_SIZE;
        public const int BITS_PER_SAMPLE = SoundConstants.BITS_PER_SAMPLE;

        // Wall tone frequencies - aliases for backward compatibility
        public const int WALL_TONE_NORTH_FREQ = SoundConstants.WallToneFrequencies.NORTH;
        public const int WALL_TONE_SOUTH_FREQ = SoundConstants.WallToneFrequencies.SOUTH;
        public const int WALL_TONE_EAST_FREQ = SoundConstants.WallToneFrequencies.EAST;
        public const int WALL_TONE_WEST_FREQ = SoundConstants.WallToneFrequencies.WEST;

        // Wall tone volume - aliases for backward compatibility
        public const float WALL_TONE_BASE_VOLUME = SoundConstants.WallToneVolumeMultipliers.BASE_VOLUME;
        public const float WALL_TONE_NORTH_MULT = SoundConstants.WallToneVolumeMultipliers.NORTH;
        public const float WALL_TONE_SOUTH_MULT = SoundConstants.WallToneVolumeMultipliers.SOUTH;
        public const float WALL_TONE_EAST_MULT = SoundConstants.WallToneVolumeMultipliers.EAST;
        public const float WALL_TONE_WEST_MULT = SoundConstants.WallToneVolumeMultipliers.WEST;

        // Pan constants - aliases for backward compatibility
        public const float PAN_CENTER = SoundConstants.WallTonePan.NORTH;
        public const float PAN_RIGHT = SoundConstants.WallTonePan.EAST;
        public const float PAN_LEFT = SoundConstants.WallTonePan.WEST;

        // Landing ping frequencies - aliases for backward compatibility
        public const int LANDING_PING_NORTH_FREQ = SoundConstants.LandingPingFrequencies.NORTH;
        public const int LANDING_PING_SOUTH_FREQ = SoundConstants.LandingPingFrequencies.SOUTH;
        public const int LANDING_PING_EAST_FREQ = SoundConstants.LandingPingFrequencies.EAST;
        public const int LANDING_PING_WEST_FREQ = SoundConstants.LandingPingFrequencies.WEST;

        // Landing ping durations - aliases for backward compatibility
        public const int LANDING_PING_TOTAL_MS = SoundConstants.LandingPingTiming.TOTAL_MS;
        public const int LANDING_PING_PING_MS = SoundConstants.LandingPingTiming.PING_MS;

        // Audio beacon frequencies - aliases for backward compatibility
        public const int BEACON_NORTH_FREQ = SoundConstants.Beacon.FREQUENCY_NORTH;
        public const int BEACON_SOUTH_FREQ = SoundConstants.Beacon.FREQUENCY_SOUTH;

        // Pre-cached direction vectors for wall tone checks (one tile in each direction)
        public static readonly Vector3 DirNorth = new Vector3(0, TILE_SIZE, 0);
        public static readonly Vector3 DirSouth = new Vector3(0, -TILE_SIZE, 0);
        public static readonly Vector3 DirEast = new Vector3(TILE_SIZE, 0, 0);
        public static readonly Vector3 DirWest = new Vector3(-TILE_SIZE, 0, 0);

        // World map IDs in FF5 (0, 1, 2 for different world states)
        public const int WORLD_MAP_1 = 0;
        public const int WORLD_MAP_2 = 1;
        public const int WORLD_MAP_3 = 2;

        public static bool IsWorldMap(int mapId) => mapId == WORLD_MAP_1 || mapId == WORLD_MAP_2 || mapId == WORLD_MAP_3;
    }
}
