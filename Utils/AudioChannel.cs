using System;
using System.Runtime.InteropServices;
using FFV_ScreenReader.Core;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Low-level SDL3 audio stream management for concurrent audio playback.
    /// One SDL audio device with 6 bound audio streams (one per SoundChannel).
    /// SDL automatically mixes all streams. Looping via demand-driven callback.
    /// </summary>
    public static class AudioChannel
    {
        #region Channel State

        private class ChannelState
        {
            public IntPtr Stream;
            public IntPtr BufferPtr;
            public volatile bool IsPlaying;
            public volatile bool IsLooping;
            public int LoopDataLength;
            public readonly object Lock = new object();
        }

        private static ChannelState[] channels;
        private static uint deviceId;
        private static SDL3Interop.SDL_AudioStreamCallback loopCallbackDelegate;
        private static bool initialized = false;
        private static int channelCount;

        #endregion

        public static void Initialize()
        {
            if (initialized) return;

            if (!SDL3Interop.SDL_Init(SDL3Interop.SDL_INIT_AUDIO))
            {
                MelonLogger.Error($"[AudioChannel] SDL_Init(AUDIO) failed: {SDL3Interop.GetError()}");
                return;
            }

            var spec = new SDL3Interop.SDL_AudioSpec
            {
                format = SDL3Interop.SDL_AUDIO_S16LE,
                channels = 2,
                freq = SoundConstants.SAMPLE_RATE
            };

            deviceId = SDL3Interop.SDL_OpenAudioDevice(SDL3Interop.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, ref spec);
            if (deviceId == 0)
            {
                MelonLogger.Error($"[AudioChannel] SDL_OpenAudioDevice failed: {SDL3Interop.GetError()}");
                return;
            }

            channelCount = Enum.GetValues(typeof(SoundChannel)).Length;
            channels = new ChannelState[channelCount];

            for (int i = 0; i < channelCount; i++)
            {
                channels[i] = new ChannelState();

                var srcSpec = new SDL3Interop.SDL_AudioSpec
                {
                    format = SDL3Interop.SDL_AUDIO_S16LE,
                    channels = 2,
                    freq = SoundConstants.SAMPLE_RATE
                };
                var dstSpec = srcSpec;

                channels[i].Stream = SDL3Interop.SDL_CreateAudioStream(ref srcSpec, ref dstSpec);
                if (channels[i].Stream == IntPtr.Zero)
                {
                    MelonLogger.Error($"[AudioChannel] SDL_CreateAudioStream failed for channel {i}: {SDL3Interop.GetError()}");
                    continue;
                }

                if (!SDL3Interop.SDL_BindAudioStream(deviceId, channels[i].Stream))
                {
                    MelonLogger.Error($"[AudioChannel] SDL_BindAudioStream failed for channel {i}: {SDL3Interop.GetError()}");
                    SDL3Interop.SDL_DestroyAudioStream(channels[i].Stream);
                    channels[i].Stream = IntPtr.Zero;
                    continue;
                }

                channels[i].BufferPtr = Marshal.AllocHGlobal(SoundConstants.CHANNEL_BUFFER_SIZE);
            }

            // Store delegate in static field to prevent GC collection
            loopCallbackDelegate = LoopCallbackHandler;

            if (!SDL3Interop.SDL_ResumeAudioDevice(deviceId))
            {
                MelonLogger.Warning($"[AudioChannel] SDL_ResumeAudioDevice failed: {SDL3Interop.GetError()}");
            }

            initialized = true;
            MelonLogger.Msg("[AudioChannel] SDL3 audio initialized with {0} channels", channelCount);
        }

        public static void Shutdown()
        {
            if (!initialized || channels == null) return;

            for (int i = 0; i < channelCount; i++)
            {
                if (channels[i] != null)
                {
                    if (channels[i].Stream != IntPtr.Zero)
                    {
                        SDL3Interop.SDL_DestroyAudioStream(channels[i].Stream);
                        channels[i].Stream = IntPtr.Zero;
                    }
                    if (channels[i].BufferPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(channels[i].BufferPtr);
                        channels[i].BufferPtr = IntPtr.Zero;
                    }
                }
            }

            if (deviceId != 0)
            {
                SDL3Interop.SDL_CloseAudioDevice(deviceId);
                deviceId = 0;
            }

            initialized = false;
        }

        public static void Play(byte[] wavData, SoundChannel channel, bool loop = false, int volumePercent = 50)
        {
            if (wavData == null || !initialized) return;

            int channelIndex = (int)channel;
            var state = channels[channelIndex];
            if (state?.Stream == IntPtr.Zero) return;

            lock (state.Lock)
            {
                // Stop any previous playback and remove loop callback
                state.IsLooping = false;
                SDL3Interop.SDL_SetAudioStreamGetCallback(state.Stream, null, IntPtr.Zero);
                SDL3Interop.SDL_ClearAudioStream(state.Stream);

                if (wavData.Length <= SoundConstants.WAV_HEADER_SIZE) return;

                int dataLength = wavData.Length - SoundConstants.WAV_HEADER_SIZE;
                Marshal.Copy(wavData, SoundConstants.WAV_HEADER_SIZE, state.BufferPtr, dataLength);

                // Set volume via stream gain (50% = 1.0 gain)
                SDL3Interop.SDL_SetAudioStreamGain(state.Stream, volumePercent / 50.0f);

                SDL3Interop.SDL_PutAudioStreamData(state.Stream, state.BufferPtr, dataLength);

                if (loop)
                {
                    state.LoopDataLength = dataLength;
                    state.IsLooping = true;
                    SDL3Interop.SDL_SetAudioStreamGetCallback(state.Stream, loopCallbackDelegate, (IntPtr)channelIndex);
                }

                state.IsPlaying = true;
            }
        }

        public static void PlayDirect(SoundChannel channel, int dataLength, Action<IntPtr> fillBuffer)
        {
            if (!initialized || fillBuffer == null) return;

            int channelIndex = (int)channel;
            var state = channels[channelIndex];
            if (state?.Stream == IntPtr.Zero) return;

            lock (state.Lock)
            {
                state.IsLooping = false;
                SDL3Interop.SDL_SetAudioStreamGetCallback(state.Stream, null, IntPtr.Zero);
                SDL3Interop.SDL_ClearAudioStream(state.Stream);

                fillBuffer(state.BufferPtr);
                SDL3Interop.SDL_PutAudioStreamData(state.Stream, state.BufferPtr, dataLength);

                state.IsPlaying = true;
            }
        }

        public static void Stop(SoundChannel channel)
        {
            if (!initialized || channels == null) return;

            var state = channels[(int)channel];
            if (state?.Stream == IntPtr.Zero) return;

            lock (state.Lock)
            {
                state.IsLooping = false;
                SDL3Interop.SDL_SetAudioStreamGetCallback(state.Stream, null, IntPtr.Zero);
                SDL3Interop.SDL_ClearAudioStream(state.Stream);
                state.IsPlaying = false;
            }
        }

        public static bool IsPlaying(SoundChannel channel)
        {
            if (!initialized || channels == null) return false;
            var state = channels[(int)channel];
            if (state == null) return false;
            return state.IsLooping || SDL3Interop.SDL_GetAudioStreamAvailable(state.Stream) > 0;
        }

        /// <summary>
        /// SDL audio thread callback — refills loop data on demand.
        /// No lock to prevent deadlock with SDL_SetAudioStreamGetCallback.
        /// Safe because IsLooping is volatile, and BufferPtr/LoopDataLength are
        /// only written while holding Lock after setting IsLooping = false.
        /// </summary>
        private static void LoopCallbackHandler(IntPtr userdata, IntPtr stream,
            int additionalAmount, int totalAmount)
        {
            int channelIndex = (int)userdata;
            var state = channels[channelIndex];
            if (state.IsLooping && state.LoopDataLength > 0)
            {
                SDL3Interop.SDL_PutAudioStreamData(stream, state.BufferPtr, state.LoopDataLength);
            }
        }
    }
}
