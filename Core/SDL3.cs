using System;
using System.Runtime.InteropServices;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// P/Invoke bindings for SDL3.dll — gamepad and keyboard input.
    /// SDL3 v3.4.0 - https://www.libsdl.org/
    /// </summary>
    public static class SDL3
    {
        private const string SDL3_DLL = "SDL3";

        // =====================================================================
        // Init flags
        // =====================================================================
        public const uint SDL_INIT_GAMEPAD = 0x00002000;

        // =====================================================================
        // Gamepad buttons (SDL_GamepadButton enum)
        // =====================================================================
        public const int SDL_GAMEPAD_BUTTON_SOUTH = 0;          // A / Cross
        public const int SDL_GAMEPAD_BUTTON_EAST = 1;           // B / Circle
        public const int SDL_GAMEPAD_BUTTON_WEST = 2;           // X / Square
        public const int SDL_GAMEPAD_BUTTON_NORTH = 3;          // Y / Triangle
        public const int SDL_GAMEPAD_BUTTON_BACK = 4;           // Back / Select
        public const int SDL_GAMEPAD_BUTTON_GUIDE = 5;          // Xbox / PS button
        public const int SDL_GAMEPAD_BUTTON_START = 6;          // Start / Menu
        public const int SDL_GAMEPAD_BUTTON_LEFT_STICK = 7;     // L3
        public const int SDL_GAMEPAD_BUTTON_RIGHT_STICK = 8;    // R3
        public const int SDL_GAMEPAD_BUTTON_LEFT_SHOULDER = 9;  // LB / L1
        public const int SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER = 10;// RB / R1
        public const int SDL_GAMEPAD_BUTTON_DPAD_UP = 11;
        public const int SDL_GAMEPAD_BUTTON_DPAD_DOWN = 12;
        public const int SDL_GAMEPAD_BUTTON_DPAD_LEFT = 13;
        public const int SDL_GAMEPAD_BUTTON_DPAD_RIGHT = 14;
        public const int SDL_GAMEPAD_BUTTON_COUNT = 15;

        // =====================================================================
        // Gamepad axes (SDL_GamepadAxis enum)
        // =====================================================================
        public const int SDL_GAMEPAD_AXIS_LEFTX = 0;
        public const int SDL_GAMEPAD_AXIS_LEFTY = 1;
        public const int SDL_GAMEPAD_AXIS_RIGHTX = 2;
        public const int SDL_GAMEPAD_AXIS_RIGHTY = 3;
        public const int SDL_GAMEPAD_AXIS_LEFT_TRIGGER = 4;
        public const int SDL_GAMEPAD_AXIS_RIGHT_TRIGGER = 5;

        // =====================================================================
        // SDL scancodes (SDL_Scancode enum) — every key used by the mod
        // =====================================================================

        // Letters
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

        // Numbers (top row)
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

        // Control keys
        public const int SDL_SCANCODE_RETURN = 40;
        public const int SDL_SCANCODE_ESCAPE = 41;
        public const int SDL_SCANCODE_BACKSPACE = 42;
        public const int SDL_SCANCODE_TAB = 43;
        public const int SDL_SCANCODE_SPACE = 44;

        // Punctuation
        public const int SDL_SCANCODE_MINUS = 45;
        public const int SDL_SCANCODE_EQUALS = 46;
        public const int SDL_SCANCODE_LEFTBRACKET = 47;
        public const int SDL_SCANCODE_RIGHTBRACKET = 48;
        public const int SDL_SCANCODE_BACKSLASH = 49;
        public const int SDL_SCANCODE_SEMICOLON = 51;
        public const int SDL_SCANCODE_APOSTROPHE = 52;
        public const int SDL_SCANCODE_GRAVE = 53;
        public const int SDL_SCANCODE_COMMA = 54;
        public const int SDL_SCANCODE_PERIOD = 55;
        public const int SDL_SCANCODE_SLASH = 56;

        // Function keys
        public const int SDL_SCANCODE_F1 = 58;
        public const int SDL_SCANCODE_F2 = 59;
        public const int SDL_SCANCODE_F3 = 60;
        public const int SDL_SCANCODE_F4 = 61;
        public const int SDL_SCANCODE_F5 = 62;
        public const int SDL_SCANCODE_F6 = 63;
        public const int SDL_SCANCODE_F7 = 64;
        public const int SDL_SCANCODE_F8 = 65;

        // Navigation
        public const int SDL_SCANCODE_HOME = 74;
        public const int SDL_SCANCODE_END = 77;
        public const int SDL_SCANCODE_RIGHT = 79;
        public const int SDL_SCANCODE_LEFT = 80;
        public const int SDL_SCANCODE_DOWN = 81;
        public const int SDL_SCANCODE_UP = 82;

        // Modifiers
        public const int SDL_SCANCODE_LCTRL = 224;
        public const int SDL_SCANCODE_LSHIFT = 225;
        public const int SDL_SCANCODE_LALT = 226;
        public const int SDL_SCANCODE_RCTRL = 228;
        public const int SDL_SCANCODE_RSHIFT = 229;

        // Maximum scancode we track (RSHIFT is highest at 229, round up)
        public const int SDL_NUM_SCANCODES = 512;

        // =====================================================================
        // P/Invoke — Lifecycle
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_Init(uint flags);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_Quit();

        // =====================================================================
        // Event types (subset — gamepad hotplug). Values from SDL_events.h:
        // the gamepad event range starts at 0x650 with AXIS_MOTION, BUTTON_DOWN,
        // BUTTON_UP, then ADDED, REMOVED, REMAPPED.
        // =====================================================================
        public const uint SDL_EVENT_GAMEPAD_ADDED = 0x653;
        public const uint SDL_EVENT_GAMEPAD_REMOVED = 0x654;

        /// <summary>
        /// SDL_Event union — 128 bytes. We only need the type field and, for
        /// gamepad device events, the `which` instance ID at offset 16.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct SDL_Event
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(16)] public uint gdevice_which; // SDL_GamepadDeviceEvent.which
        }

        // =====================================================================
        // P/Invoke — Events
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_PumpEvents();

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_PollEvent(out SDL_Event evt);

        // =====================================================================
        // P/Invoke — Gamepad enumeration
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetGamepads(out int count);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_free(IntPtr mem);

        // =====================================================================
        // P/Invoke — Gamepad open/close
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_OpenGamepad(uint instance_id);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_CloseGamepad(IntPtr gamepad);

        // =====================================================================
        // P/Invoke — Gamepad state
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GetGamepadButton(IntPtr gamepad, int button);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern short SDL_GetGamepadAxis(IntPtr gamepad, int axis);

        // =====================================================================
        // P/Invoke — Gamepad info
        // =====================================================================

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetGamepadType(IntPtr gamepad);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SDL_GetGamepadID(IntPtr gamepad);

        // SDL_GamepadType enum
        public const int SDL_GAMEPAD_TYPE_UNKNOWN = 0;
        public const int SDL_GAMEPAD_TYPE_STANDARD = 1;
        public const int SDL_GAMEPAD_TYPE_XBOX360 = 2;
        public const int SDL_GAMEPAD_TYPE_XBOXONE = 3;
        public const int SDL_GAMEPAD_TYPE_PS3 = 4;
        public const int SDL_GAMEPAD_TYPE_PS4 = 5;
        public const int SDL_GAMEPAD_TYPE_PS5 = 6;
        public const int SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO = 7;
        public const int SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT = 8;
        public const int SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT = 9;
        public const int SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR = 10;

        // =====================================================================
        // P/Invoke — Keyboard
        // =====================================================================

        /// <summary>
        /// Returns a pointer to an internal array of key states indexed by SDL_Scancode.
        /// Each element is a bool (1 byte): non-zero = pressed.
        /// The pointer remains valid for the lifetime of the application.
        /// Must call SDL_PumpEvents() before reading to get current state.
        /// </summary>
        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetKeyboardState(out int numkeys);

        // =====================================================================
        // Audio — subsystem flag, device/stream API (SDL3 audio backend).
        // The mod feeds raw S16LE / stereo / 22050 Hz PCM, so src == dst spec
        // means SDL does no resampling — just mixing of all bound streams.
        // =====================================================================

        public const uint SDL_INIT_AUDIO = 0x00000010;

        /// <summary>Default playback device id (SDL_AudioDeviceID)0xFFFFFFFF.</summary>
        public const uint SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK = 0xFFFFFFFF;

        /// <summary>SDL_AUDIO_S16LE: signed (0x8000) | 16-bit (0x10) little-endian PCM.</summary>
        public const int SDL_AUDIO_S16LE = 0x8010;

        /// <summary>
        /// SDL_AudioSpec — 12 bytes. SDL_AudioFormat is a 4-byte enum.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SDL_AudioSpec
        {
            public int format;    // SDL_AudioFormat
            public int channels;
            public int freq;
        }

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_InitSubSystem(uint flags);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_QuitSubSystem(uint flags);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SDL_OpenAudioDevice(uint devid, ref SDL_AudioSpec spec);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_CloseAudioDevice(uint devid);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_ResumeAudioDevice(uint devid);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_CreateAudioStream(ref SDL_AudioSpec srcSpec, ref SDL_AudioSpec dstSpec);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_DestroyAudioStream(IntPtr stream);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_BindAudioStream(uint devid, IntPtr stream);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_PutAudioStreamData(IntPtr stream, IntPtr buf, int len);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetAudioStreamQueued(IntPtr stream);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_ClearAudioStream(IntPtr stream);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_SetAudioStreamGain(IntPtr stream, float gain);

        /// <summary>Last SDL error message (UTF-8 const char*, "" if none). Used by the audio failure logs.</summary>
        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_GetError();
    }
}
