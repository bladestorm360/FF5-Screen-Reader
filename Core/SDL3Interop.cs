using System;
using System.Runtime.InteropServices;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// P/Invoke declarations for SDL3.dll.
    /// Pure interop layer — no logic, just extern methods and constants.
    /// </summary>
    public static class SDL3Interop
    {
        private const string SDL3 = "SDL3";

        // --- Init / Shutdown ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_Init(uint flags);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_Quit();

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetError();

        /// <summary>
        /// Helper to marshal SDL_GetError() to a managed string.
        /// </summary>
        public static string GetError()
        {
            IntPtr ptr = SDL_GetError();
            return ptr == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(ptr) ?? "";
        }

        // --- Window ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_CreateWindow(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            int w, int h, uint flags);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_DestroyWindow(IntPtr window);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_SetWindowTitle(IntPtr window,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_ShowWindow(IntPtr window);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_HideWindow(IntPtr window);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_RaiseWindow(IntPtr window);

        // --- Events / Keyboard ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_PumpEvents();

        /// <summary>
        /// Returns a pointer to an array of key states indexed by SDL_Scancode.
        /// The pointer remains valid for the lifetime of the application.
        /// numkeys receives the length of the array.
        /// </summary>
        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetKeyboardState(out int numkeys);

        // --- Hints ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_SetHint(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        // --- Init flags ---
        public const uint SDL_INIT_VIDEO   = 0x00000020;
        public const uint SDL_INIT_AUDIO   = 0x00000010;
        public const uint SDL_INIT_GAMEPAD = 0x00002000;

        // --- Window flags ---
        public const uint SDL_WINDOW_HIDDEN = 0x00000008;

        // --- Audio constants ---
        public const int SDL_AUDIO_S16LE = 0x8010;
        public const uint SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK = 0xFFFFFFFFu;

        // --- Audio structs ---

        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_AudioSpec
        {
            public int format;
            public int channels;
            public int freq;
        }

        // --- Audio callback ---

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SDL_AudioStreamCallback(
            IntPtr userdata, IntPtr stream, int additionalAmount, int totalAmount);

        // --- Audio device ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SDL_OpenAudioDevice(uint devid, ref SDL_AudioSpec spec);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_CloseAudioDevice(uint devid);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_ResumeAudioDevice(uint devid);

        // --- Audio streams ---

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_CreateAudioStream(ref SDL_AudioSpec srcSpec, ref SDL_AudioSpec dstSpec);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_DestroyAudioStream(IntPtr stream);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_BindAudioStream(uint devid, IntPtr stream);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_PutAudioStreamData(IntPtr stream, IntPtr buf, int len);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_ClearAudioStream(IntPtr stream);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetAudioStreamAvailable(IntPtr stream);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_SetAudioStreamGain(IntPtr stream, float gain);

        [DllImport(SDL3, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SDL_SetAudioStreamGetCallback(
            IntPtr stream, SDL_AudioStreamCallback callback, IntPtr userdata);

        // --- SDL Scancodes (subset used by mod keys) ---
        public const int SDL_SCANCODE_A = 4;
        public const int SDL_SCANCODE_B = 5;
        public const int SDL_SCANCODE_C = 6;
        public const int SDL_SCANCODE_D = 7;
        public const int SDL_SCANCODE_E = 8;
        public const int SDL_SCANCODE_F = 9;
        public const int SDL_SCANCODE_G = 10;
        public const int SDL_SCANCODE_H = 11;
        public const int SDL_SCANCODE_I = 12;
        public const int SDL_SCANCODE_J = 13;
        public const int SDL_SCANCODE_K = 14;
        public const int SDL_SCANCODE_L = 15;
        public const int SDL_SCANCODE_M = 16;
        public const int SDL_SCANCODE_N = 17;
        public const int SDL_SCANCODE_O = 18;
        public const int SDL_SCANCODE_P = 19;
        public const int SDL_SCANCODE_Q = 20;
        public const int SDL_SCANCODE_R = 21;
        public const int SDL_SCANCODE_S = 22;
        public const int SDL_SCANCODE_T = 23;
        public const int SDL_SCANCODE_U = 24;
        public const int SDL_SCANCODE_V = 25;
        public const int SDL_SCANCODE_W = 26;
        public const int SDL_SCANCODE_X = 27;
        public const int SDL_SCANCODE_Y = 28;
        public const int SDL_SCANCODE_Z = 29;
        public const int SDL_SCANCODE_1 = 30;
        public const int SDL_SCANCODE_2 = 31;
        public const int SDL_SCANCODE_3 = 32;
        public const int SDL_SCANCODE_4 = 33;
        public const int SDL_SCANCODE_5 = 34;
        public const int SDL_SCANCODE_6 = 35;
        public const int SDL_SCANCODE_7 = 36;
        public const int SDL_SCANCODE_8 = 37;
        public const int SDL_SCANCODE_9 = 38;
        public const int SDL_SCANCODE_0 = 39;
        public const int SDL_SCANCODE_RETURN    = 40;
        public const int SDL_SCANCODE_ESCAPE    = 41;
        public const int SDL_SCANCODE_BACKSPACE = 42;
        public const int SDL_SCANCODE_TAB       = 43;
        public const int SDL_SCANCODE_SPACE     = 44;
        public const int SDL_SCANCODE_MINUS     = 45;
        public const int SDL_SCANCODE_EQUALS    = 46;
        public const int SDL_SCANCODE_LEFTBRACKET  = 47;
        public const int SDL_SCANCODE_RIGHTBRACKET = 48;
        public const int SDL_SCANCODE_BACKSLASH = 49;
        public const int SDL_SCANCODE_SEMICOLON = 51;
        public const int SDL_SCANCODE_APOSTROPHE = 52;
        public const int SDL_SCANCODE_GRAVE     = 53;
        public const int SDL_SCANCODE_COMMA     = 54;
        public const int SDL_SCANCODE_PERIOD    = 55;
        public const int SDL_SCANCODE_SLASH     = 56;
        public const int SDL_SCANCODE_F1        = 58;
        public const int SDL_SCANCODE_F3        = 60;
        public const int SDL_SCANCODE_F5        = 62;
        public const int SDL_SCANCODE_F8        = 65;
        public const int SDL_SCANCODE_HOME      = 74;
        public const int SDL_SCANCODE_END       = 77;
        public const int SDL_SCANCODE_RIGHT     = 79;
        public const int SDL_SCANCODE_LEFT      = 80;
        public const int SDL_SCANCODE_DOWN      = 81;
        public const int SDL_SCANCODE_UP        = 82;
        public const int SDL_SCANCODE_LCTRL     = 224;
        public const int SDL_SCANCODE_LSHIFT    = 225;
        public const int SDL_SCANCODE_RCTRL     = 228;
        public const int SDL_SCANCODE_RSHIFT    = 229;
    }
}
