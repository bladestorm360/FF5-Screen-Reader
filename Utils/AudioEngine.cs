using System;
using System.Runtime.InteropServices;
using MelonLoader;
using FFV_ScreenReader.Core;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// SDL3-backed audio output. One logical playback device with several
    /// SDL_AudioStreams bound to it; SDL mixes every bound stream automatically.
    /// The mod feeds raw S16LE / stereo / 22050 Hz PCM, so src == dst spec and no
    /// resampling happens. Per-stream gain (SDL_SetAudioStreamGain) handles user
    /// volume and the headroom that the old code baked into a pre-mixed buffer.
    ///
    /// Looping (wall tones) is driven by the caller topping up each stream's queue
    /// from the ~100ms audio coroutine — no native audio-thread callback, so all
    /// submission stays on the main thread.
    /// </summary>
    public static class AudioEngine
    {
        /// <summary>
        /// Logical output streams, all bound to the single device. The four wall-tone
        /// directions are separate streams so SDL sums them (replacing manual mixing).
        /// </summary>
        public enum Stream
        {
            Footstep,
            WallBump,
            Beacon,
            WallNorth,
            WallSouth,
            WallEast,
            WallWest,
            LandingNorth,
            LandingSouth,
            LandingEast,
            LandingWest,
            Counter,
        }

        private const int StreamCount = 12;

        private static uint device;
        private static readonly IntPtr[] streams = new IntPtr[StreamCount];
        private static IntPtr beaconScratch = IntPtr.Zero;
        private static bool initialized;

        public static bool IsInitialized => initialized;

        /// <summary>
        /// Opens the audio device and creates+binds all streams. Degrades gracefully
        /// (audio disabled, mod keeps running) if SDL3.dll is missing or the device
        /// can't be opened.
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;

            try
            {
                // Subsystem-scoped — independent of GamepadManager's SDL_INIT_GAMEPAD. If SDL3.dll is
                // absent, the first SDL call throws DllNotFoundException (caught below) and audio stays
                // disabled — there is no non-SDL playback path.
                if (!SDL3.SDL_InitSubSystem(SDL3.SDL_INIT_AUDIO))
                {
                    MelonLogger.Error($"[SDL Audio] SDL_InitSubSystem(AUDIO) failed: {SdlError()} — audio DISABLED (no fallback)");
                    return;
                }

                var spec = new SDL3.SDL_AudioSpec
                {
                    format = SDL3.SDL_AUDIO_S16LE,
                    channels = 2,
                    freq = SoundConstants.SAMPLE_RATE,
                };

                device = SDL3.SDL_OpenAudioDevice(SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, ref spec);
                if (device == 0)
                {
                    MelonLogger.Error($"[SDL Audio] SDL_OpenAudioDevice failed: {SdlError()} — audio DISABLED (no fallback)");
                    SDL3.SDL_QuitSubSystem(SDL3.SDL_INIT_AUDIO);
                    return;
                }
                for (int i = 0; i < StreamCount; i++)
                {
                    IntPtr s = SDL3.SDL_CreateAudioStream(ref spec, ref spec);
                    if (s == IntPtr.Zero)
                    {
                        MelonLogger.Error($"[SDL Audio] SDL_CreateAudioStream failed for {(Stream)i}: {SdlError()}");
                    }
                    else if (!SDL3.SDL_BindAudioStream(device, s))
                    {
                        MelonLogger.Error($"[SDL Audio] SDL_BindAudioStream failed for {(Stream)i}: {SdlError()}");
                        SDL3.SDL_DestroyAudioStream(s);
                        s = IntPtr.Zero;
                    }
                    streams[i] = s;
                }

                // A freshly opened device starts paused; resume is a harmless no-op if it is
                // already running. Skipping this is a silent-no-audio failure.
                if (!SDL3.SDL_ResumeAudioDevice(device))
                    MelonLogger.Warning($"[SDL Audio] SDL_ResumeAudioDevice failed: {SdlError()}");

                beaconScratch = Marshal.AllocHGlobal(SoundConstants.CHANNEL_BUFFER_SIZE);

                initialized = true;
            }
            catch (DllNotFoundException)
            {
                MelonLogger.Error("[SDL Audio] ✗ SDL3.dll NOT FOUND — audio DISABLED (no fallback; nothing will play)");
                initialized = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SDL Audio] ✗ Initialization failed: {ex.Message} — audio DISABLED");
                initialized = false;
            }
        }

        // SDL error-string helpers (used by the failure logs above).
        private static string SdlError()
        {
            string e = PtrToStr(SDL3.SDL_GetError());
            return string.IsNullOrEmpty(e) ? "(no SDL error)" : e;
        }

        private static string PtrToStr(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

        /// <summary>
        /// Tears down streams and the device, frees the scratch buffer, and quits ONLY the
        /// audio subsystem (never blanket SDL_Quit — gamepad may still be live).
        /// </summary>
        public static void Shutdown()
        {
            if (!initialized) return;
            initialized = false;

            for (int i = 0; i < StreamCount; i++)
            {
                if (streams[i] != IntPtr.Zero)
                {
                    try { SDL3.SDL_ClearAudioStream(streams[i]); } catch { }
                    try { SDL3.SDL_DestroyAudioStream(streams[i]); } catch { }
                    streams[i] = IntPtr.Zero;
                }
            }

            if (device != 0)
            {
                try { SDL3.SDL_CloseAudioDevice(device); } catch { }
                device = 0;
            }

            if (beaconScratch != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(beaconScratch);
                beaconScratch = IntPtr.Zero;
            }

            try { SDL3.SDL_QuitSubSystem(SDL3.SDL_INIT_AUDIO); } catch { }
        }

        /// <summary>Sets the playback gain for a stream (1.0 = unity, 0.0 = silence).</summary>
        public static void SetGain(Stream id, float gain)
        {
            IntPtr s = streams[(int)id];
            if (!initialized || s == IntPtr.Zero) return;
            SDL3.SDL_SetAudioStreamGain(s, gain);
        }

        /// <summary>Output bytes currently queued on a stream (0 if unavailable).</summary>
        public static int QueuedBytes(Stream id)
        {
            IntPtr s = streams[(int)id];
            if (!initialized || s == IntPtr.Zero) return 0;
            int q = SDL3.SDL_GetAudioStreamQueued(s);
            return q < 0 ? 0 : q;
        }

        /// <summary>Drops any queued/in-flight audio on a stream (immediate stop).</summary>
        public static void Clear(Stream id)
        {
            IntPtr s = streams[(int)id];
            if (!initialized || s == IntPtr.Zero) return;
            SDL3.SDL_ClearAudioStream(s);
        }

        /// <summary>
        /// Appends PCM from a WAV byte array (skipping <paramref name="offset"/> header bytes)
        /// to a stream's queue. SDL copies synchronously, so the pin is short-lived.
        /// </summary>
        public static void Submit(Stream id, byte[] wav, int offset, int length)
        {
            IntPtr s = streams[(int)id];
            if (!initialized || s == IntPtr.Zero || wav == null || length <= 0) return;
            if (offset < 0 || offset + length > wav.Length) return;

            var handle = GCHandle.Alloc(wav, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = IntPtr.Add(handle.AddrOfPinnedObject(), offset);
                SDL3.SDL_PutAudioStreamData(s, ptr, length);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Plays a one-shot: sets the stream gain, drops anything already queued, and submits
        /// this buffer. Used for footstep and wall-bump effects.
        /// </summary>
        public static void PlayOneShot(Stream id, byte[] wav, int offset, int length, float gain)
        {
            IntPtr s = streams[(int)id];
            if (!initialized || s == IntPtr.Zero) return;
            SDL3.SDL_SetAudioStreamGain(s, gain);
            SDL3.SDL_ClearAudioStream(s);
            Submit(id, wav, offset, length);
        }

        /// <summary>
        /// Beacon path: fills the reusable scratch buffer with freshly synthesized PCM via the
        /// callback, then replaces the Beacon stream's queue with it. Zero managed allocations
        /// per ping (matches the old PlayDirect path). Volume is baked into the samples, so the
        /// Beacon stream keeps unity gain.
        /// </summary>
        public static void PlayBeaconDirect(int length, Action<IntPtr> fill)
        {
            IntPtr s = streams[(int)Stream.Beacon];
            if (!initialized || s == IntPtr.Zero || fill == null) return;
            if (length <= 0 || length > SoundConstants.CHANNEL_BUFFER_SIZE) return;

            fill(beaconScratch);
            SDL3.SDL_ClearAudioStream(s);
            SDL3.SDL_PutAudioStreamData(s, beaconScratch, length);
        }
    }
}
