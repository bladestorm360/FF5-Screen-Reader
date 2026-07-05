using System;
using System.IO;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Generates PCM audio tones as WAV byte arrays.
    /// All output is 16-bit at SoundConstants.SAMPLE_RATE.
    /// </summary>
    public static class ToneGenerator
    {
        /// <summary>
        /// Generates a mono "thud" tone with soft quadratic attack, noise mix, and decay.
        /// Used for wall bump sounds.
        /// </summary>
        public static byte[] GenerateThudTone(int frequency, int durationMs, float volume)
        {
            int samples = (SoundConstants.SAMPLE_RATE * durationMs) / 1000;
            int attackSamples = samples / 4;
            var random = new Random(42);
            int dataSize = samples * 2; // 16-bit mono

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 1, dataSize);

                double filteredNoise = 0;
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / SoundConstants.SAMPLE_RATE;
                    double attackLinear = Math.Min(1.0, (double)i / attackSamples);
                    double attack = attackLinear * attackLinear;
                    double decay = (double)(samples - i) / samples;
                    double envelope = attack * decay;

                    double sine = Math.Sin(2 * Math.PI * frequency * t);
                    double rawNoise = (random.NextDouble() * 2 - 1);
                    filteredNoise = filteredNoise * 0.9 + rawNoise * 0.1;
                    double noise = filteredNoise * 0.3 * attack;
                    double value = (sine * 0.7 + noise) * volume * envelope;

                    writer.Write((short)(value * 32767));
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a mono filtered-noise click tone with exponential decay.
        /// Used for footstep sounds.
        /// </summary>
        public static byte[] GenerateClickTone(int frequency, int durationMs, float volume)
        {
            int samples = (SoundConstants.SAMPLE_RATE * durationMs) / 1000;
            var random = new Random(42);
            int dataSize = samples * 2; // 16-bit mono

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 1, dataSize);

                for (int i = 0; i < samples; i++)
                {
                    double decay = Math.Exp(-10.0 * i / samples);
                    double noise = (random.NextDouble() * 2 - 1) * volume * decay;
                    writer.Write((short)(noise * 32767));
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a 16-bit stereo sine tone with constant-power panning.
        /// When sustain is false: applies attack+decay envelope (for one-shot sounds).
        /// When sustain is true: cycle-aligned flat amplitude for seamless hardware looping.
        /// </summary>
        public static byte[] GenerateStereoTone(int frequency, int durationMs, float volume, float pan, bool sustain = false)
        {
            int samples;
            if (sustain)
            {
                // Cycle-aligned for seamless looping
                double samplesPerCycle = (double)SoundConstants.SAMPLE_RATE / frequency;
                int targetSamples = (SoundConstants.SAMPLE_RATE * durationMs) / 1000;
                int numCycles = (int)Math.Round(targetSamples / samplesPerCycle);
                if (numCycles < 1) numCycles = 1;
                samples = (int)Math.Round(numCycles * samplesPerCycle);
            }
            else
            {
                samples = (SoundConstants.SAMPLE_RATE * durationMs) / 1000;
            }

            int dataSize = samples * 4; // stereo 16-bit

            double panAngle = pan * Math.PI / 2;
            float leftVol = volume * (float)Math.Cos(panAngle);
            float rightVol = volume * (float)Math.Sin(panAngle);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, dataSize);

                int attackSamples = samples / 10;
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / SoundConstants.SAMPLE_RATE;
                    double sineValue;

                    if (sustain)
                    {
                        sineValue = Math.Sin(2 * Math.PI * frequency * t);
                    }
                    else
                    {
                        double attack = Math.Min(1.0, (double)i / attackSamples);
                        double decay = (double)(samples - i) / samples;
                        sineValue = Math.Sin(2 * Math.PI * frequency * t) * attack * decay;
                    }

                    writer.Write((short)(sineValue * leftVol * 32767));
                    writer.Write((short)(sineValue * rightVol * 32767));
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a 16-bit stereo WAV containing a short ping followed by silence.
        /// When hardware-looped, the silence gap creates a pulsing effect.
        /// Uses cycle-aligned ping duration for clean sound at loop boundary.
        /// </summary>
        public static byte[] GenerateLandingPing(int frequency, int totalDurationMs, int pingDurationMs, float volume, float pan)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;
            int totalSamples = (sampleRate * totalDurationMs) / 1000;
            int pingSamples = (sampleRate * pingDurationMs) / 1000;

            double samplesPerCycle = (double)sampleRate / frequency;
            int numCycles = (int)Math.Round(pingSamples / samplesPerCycle);
            if (numCycles < 1) numCycles = 1;
            pingSamples = (int)Math.Round(numCycles * samplesPerCycle);

            if (totalSamples <= pingSamples)
                totalSamples = pingSamples + (sampleRate * 50) / 1000;

            int dataSize = totalSamples * 4;

            double panAngle = pan * Math.PI / 2;
            float leftVol = volume * (float)Math.Cos(panAngle);
            float rightVol = volume * (float)Math.Sin(panAngle);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, dataSize);

                int attackSamples = pingSamples / 8;
                int decaySamples = pingSamples / 4;
                int decayStart = pingSamples - decaySamples;

                for (int i = 0; i < totalSamples; i++)
                {
                    if (i < pingSamples)
                    {
                        double t = (double)i / sampleRate;
                        double envelope = 1.0;

                        if (i < attackSamples)
                            envelope = (double)i / attackSamples;
                        else if (i >= decayStart)
                            envelope = (double)(pingSamples - i) / decaySamples;

                        double sineValue = Math.Sin(2 * Math.PI * frequency * t) * envelope;

                        writer.Write((short)(sineValue * leftVol * 32767));
                        writer.Write((short)(sineValue * rightVol * 32767));
                    }
                    else
                    {
                        writer.Write((short)0);
                        writer.Write((short)0);
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Converts a mono 16-bit WAV to stereo by duplicating each sample to both channels.
        /// </summary>
        public static byte[] MonoToStereo(byte[] monoWav)
        {
            if (monoWav == null || monoWav.Length < SoundConstants.WAV_HEADER_SIZE) return monoWav;

            using (var reader = new BinaryReader(new MemoryStream(monoWav)))
            {
                reader.ReadBytes(4);  // "RIFF"
                reader.ReadInt32();   // file size
                reader.ReadBytes(4);  // "WAVE"
                reader.ReadBytes(4);  // "fmt "
                int fmtSize = reader.ReadInt32();
                reader.ReadInt16();   // audio format
                int channels = reader.ReadInt16();
                reader.ReadInt32();   // sample rate (always SAMPLE_RATE for our generated tones)
                reader.ReadInt32();   // byte rate
                reader.ReadInt16();   // block align
                reader.ReadInt16();   // bits per sample

                if (fmtSize > 16)
                    reader.ReadBytes(fmtSize - 16);

                reader.ReadBytes(4);  // "data"
                int dataSize = reader.ReadInt32();

                if (channels == 2) return monoWav;

                byte[] monoData = reader.ReadBytes(dataSize);
                int stereoDataSize = dataSize * 2;

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    WriteWavHeader(writer, 2, stereoDataSize);

                    // Copy 16-bit samples (2 bytes each) to both channels
                    for (int i = 0; i < monoData.Length; i += 2)
                    {
                        writer.Write(monoData[i]);      // Left low byte
                        writer.Write(monoData[i + 1]);  // Left high byte
                        writer.Write(monoData[i]);      // Right low byte
                        writer.Write(monoData[i + 1]);  // Right high byte
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Writes a standard 44-byte WAV file header for 16-bit PCM audio.
        /// </summary>
        private static void WriteWavHeader(BinaryWriter writer, int numChannels, int dataSize)
        {
            int blockAlign = numChannels * 2; // 16-bit = 2 bytes per sample
            int byteRate = SoundConstants.SAMPLE_RATE * blockAlign;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                          // fmt chunk size
            writer.Write((short)1);                    // PCM
            writer.Write((short)numChannels);
            writer.Write(SoundConstants.SAMPLE_RATE);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)16);                   // bits per sample

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
        }
    }
}
