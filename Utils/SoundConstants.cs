namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Audio constants for tone generation and playback.
    /// </summary>
    public static class SoundConstants
    {
        /// <summary>Sample rate for all generated audio (Hz).</summary>
        public const int SAMPLE_RATE = 22050;

        /// <summary>WAV file header size in bytes.</summary>
        public const int WAV_HEADER_SIZE = 44;

        /// <summary>Pre-allocated buffer size per channel (bytes).</summary>
        public const int CHANNEL_BUFFER_SIZE = 32768;

        /// <summary>Bits per audio sample.</summary>
        public const int BITS_PER_SAMPLE = 16;

        /// <summary>
        /// Wall tone frequencies per direction (Hz).
        /// Higher frequencies for vertical, lower for horizontal.
        /// </summary>
        public static class WallToneFrequencies
        {
            public const int NORTH = 330;
            public const int SOUTH = 110;
            public const int EAST = 220;
            public const int WEST = 200;
        }

        /// <summary>
        /// Fletcher-Munson equal-loudness volume multipliers per direction.
        /// Applied to BASE_VOLUME to compensate for perceived loudness differences
        /// at different frequencies.
        /// </summary>
        public static class WallToneVolumeMultipliers
        {
            public const float BASE_VOLUME = 0.132f;
            public const float NORTH = 1.00f;
            public const float SOUTH = 0.70f;
            public const float EAST = 0.85f;
            public const float WEST = 0.85f;
        }

        /// <summary>
        /// Pan positions per direction (0.0=left, 0.5=center, 1.0=right).
        /// </summary>
        public static class WallTonePan
        {
            public const float NORTH = 0.5f;
            public const float SOUTH = 0.5f;
            public const float EAST = 1.0f;
            public const float WEST = 0.0f;
        }

        /// <summary>
        /// Wall bump sound parameters.
        /// </summary>
        public static class WallBump
        {
            public const int FREQUENCY = 27;
            public const int DURATION_MS = 60;
            public const float VOLUME = 0.759f;
        }

        /// <summary>
        /// Footstep click sound parameters.
        /// </summary>
        public static class Footstep
        {
            public const int FREQUENCY = 500;
            public const int DURATION_MS = 25;
            public const float VOLUME = 0.237f;
        }

        /// <summary>
        /// Audio beacon parameters.
        /// </summary>
        public static class Beacon
        {
            public const int FREQUENCY_NORTH = 400;
            public const int FREQUENCY_SOUTH = 280;
            public const int DURATION_MS = 60;
            public const float MIN_VOLUME = 0.11f;
            public const float MAX_VOLUME = 0.55f;
        }

        /// <summary>
        /// Landing ping frequencies per direction (Hz).
        /// </summary>
        public static class LandingPingFrequencies
        {
            public const int NORTH = 660;
            public const int SOUTH = 440;
            public const int EAST = 550;
            public const int WEST = 520;
        }

        /// <summary>
        /// Landing ping volume multipliers (separate from wall tones).
        /// </summary>
        public static class LandingPingVolumeMultipliers
        {
            public const float BASE_VOLUME = 0.132f;
        }

        /// <summary>
        /// Landing ping timing parameters.
        /// </summary>
        public static class LandingPingTiming
        {
            public const int TOTAL_MS = 250;
            public const int PING_MS = 80;
        }

        /// <summary>
        /// Wall tone timing parameters.
        /// </summary>
        public static class WallToneTiming
        {
            public const int ONE_SHOT_DURATION_MS = 150;
            public const int SUSTAIN_DURATION_MS = 200;
        }

        /// <summary>
        /// EXP counter beep parameters (battle results rolling animation).
        /// </summary>
        public static class ExpCounter
        {
            public const int FREQUENCY = 2000;
            public const int BEEP_MS = 50;
            public const int SILENCE_MS = 50;
            public const float VOLUME = 0.15f;
        }

    }
}
