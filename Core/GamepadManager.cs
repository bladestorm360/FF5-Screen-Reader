using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Manages SDL3 gamepad and keyboard input. Sole input authority for the mod.
    /// Polls all gamepad buttons/axes and keyboard state each frame via SDL.
    /// Replaces Unity's Input.GetKeyDown and WindowsFocusHelper.GetAsyncKeyState.
    /// </summary>
    public static class GamepadManager
    {
        // --- SDL state ---
        private static bool sdlInitialized;
        private static IntPtr gamepadHandle = IntPtr.Zero;
        private static int gamepadType = SDL3.SDL_GAMEPAD_TYPE_UNKNOWN;

        /// <summary>True if SDL3 loaded and a gamepad is open.</summary>
        public static bool IsAvailable => sdlInitialized && gamepadHandle != IntPtr.Zero;

        /// <summary>SDL_GamepadType of the connected controller.</summary>
        public static int GamepadType => gamepadType;

        /// <summary>True if SDL3 loaded (keyboard works even without gamepad).</summary>
        public static bool IsSdlReady => sdlInitialized;

        // =====================================================================
        // Gamepad buttons — unified tracking for all 15 buttons
        // =====================================================================
        private static readonly bool[] btnCurrent = new bool[SDL3.SDL_GAMEPAD_BUTTON_COUNT];
        private static readonly bool[] btnPrevious = new bool[SDL3.SDL_GAMEPAD_BUTTON_COUNT];

        public static bool IsButtonPressed(int btn) => btn >= 0 && btn < SDL3.SDL_GAMEPAD_BUTTON_COUNT && btnCurrent[btn] && !btnPrevious[btn];
        public static bool IsButtonHeld(int btn) => btn >= 0 && btn < SDL3.SDL_GAMEPAD_BUTTON_COUNT && btnCurrent[btn];
        public static bool IsButtonReleased(int btn) => btn >= 0 && btn < SDL3.SDL_GAMEPAD_BUTTON_COUNT && !btnCurrent[btn] && btnPrevious[btn];

        // D-pad convenience
        public static bool DpadUpPressed => IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_DPAD_UP);
        public static bool DpadDownPressed => IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_DPAD_DOWN);
        public static bool DpadLeftPressed => IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_DPAD_LEFT);
        public static bool DpadRightPressed => IsButtonPressed(SDL3.SDL_GAMEPAD_BUTTON_DPAD_RIGHT);

        // =====================================================================
        // Analog sticks
        // =====================================================================
        public static float LeftStickX { get; private set; }
        public static float LeftStickY { get; private set; }
        public static float RightStickX { get; private set; }
        public static float RightStickY { get; private set; }
        public static float LeftTrigger { get; private set; }
        public static float RightTrigger { get; private set; }

        // Deadzone for stick normalization
        private const short AXIS_DEADZONE = 8000;
        // Right stick cardinal isolation thresholds
        private const short RSTICK_THRESHOLD = 16000;
        private const short RSTICK_DOMINANCE = 8000;
        // Left stick direction threshold (for menu navigation edge detection)
        private const short LSTICK_DIR_THRESHOLD = 16000;

        // --- Right stick virtual directions (cardinal isolation) ---
        private static readonly bool[] rstickCurrent = new bool[4];
        private static readonly bool[] rstickPrevious = new bool[4];
        private const int DIR_UP = 0, DIR_DOWN = 1, DIR_LEFT = 2, DIR_RIGHT = 3;

        public static bool RStickUpPressed => rstickCurrent[DIR_UP] && !rstickPrevious[DIR_UP];
        public static bool RStickDownPressed => rstickCurrent[DIR_DOWN] && !rstickPrevious[DIR_DOWN];
        public static bool RStickLeftPressed => rstickCurrent[DIR_LEFT] && !rstickPrevious[DIR_LEFT];
        public static bool RStickRightPressed => rstickCurrent[DIR_RIGHT] && !rstickPrevious[DIR_RIGHT];

        // --- Left stick virtual directions (for menu passthrough edge detection) ---
        private static readonly bool[] lstickCurrent = new bool[4];
        private static readonly bool[] lstickPrevious = new bool[4];

        public static bool LeftStickUpPressed => lstickCurrent[DIR_UP] && !lstickPrevious[DIR_UP];
        public static bool LeftStickDownPressed => lstickCurrent[DIR_DOWN] && !lstickPrevious[DIR_DOWN];
        public static bool LeftStickLeftPressed => lstickCurrent[DIR_LEFT] && !lstickPrevious[DIR_LEFT];
        public static bool LeftStickRightPressed => lstickCurrent[DIR_RIGHT] && !lstickPrevious[DIR_RIGHT];

        public static bool LeftStickUpHeld => lstickCurrent[DIR_UP];
        public static bool LeftStickDownHeld => lstickCurrent[DIR_DOWN];
        public static bool LeftStickLeftHeld => lstickCurrent[DIR_LEFT];
        public static bool LeftStickRightHeld => lstickCurrent[DIR_RIGHT];

        // =====================================================================
        // Keyboard via GetAsyncKeyState (reads hardware state, no window focus needed)
        // SDL keyboard doesn't work without an SDL window — GetAsyncKeyState is the
        // keyboard equivalent of SDL gamepad (reads device state directly).
        // =====================================================================

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // Track state for edge detection — indexed by VK code
        private const int VK_MAX = 256;
        private static readonly bool[] keyCurrent = new bool[VK_MAX];
        private static readonly bool[] keyPrevious = new bool[VK_MAX];
        private static bool anyKeyDownThisFrame;

        // All VK codes the mod uses (polled each frame)
        private static readonly int[] TrackedVKCodes;

        static GamepadManager()
        {
            // Build list of all VK codes we need to poll
            var vks = new List<int>();
            foreach (var vk in KeyCodeToVK.Values)
            {
                if (!vks.Contains(vk))
                    vks.Add(vk);
            }
            TrackedVKCodes = vks.ToArray();
        }

        /// <summary>True on the frame a VK code key is first pressed.</summary>
        public static bool IsKeyPressed(int vk)
        {
            return vk >= 0 && vk < VK_MAX && keyCurrent[vk] && !keyPrevious[vk];
        }

        /// <summary>True while a VK code key is held.</summary>
        public static bool IsKeyHeld(int vk)
        {
            return vk >= 0 && vk < VK_MAX && keyCurrent[vk];
        }

        /// <summary>True if any tracked keyboard key was pressed this frame.</summary>
        public static bool AnyKeyboardKeyDown() => anyKeyDownThisFrame;

        // =====================================================================
        // KeyCode → Windows VK code mapping
        // =====================================================================
        private static readonly Dictionary<KeyCode, int> KeyCodeToVK = new Dictionary<KeyCode, int>
        {
            // Letters (VK_A=0x41 through VK_Z=0x5A)
            { KeyCode.A, 0x41 }, { KeyCode.B, 0x42 }, { KeyCode.C, 0x43 }, { KeyCode.D, 0x44 },
            { KeyCode.E, 0x45 }, { KeyCode.F, 0x46 }, { KeyCode.G, 0x47 }, { KeyCode.H, 0x48 },
            { KeyCode.I, 0x49 }, { KeyCode.J, 0x4A }, { KeyCode.K, 0x4B }, { KeyCode.L, 0x4C },
            { KeyCode.M, 0x4D }, { KeyCode.N, 0x4E }, { KeyCode.O, 0x4F }, { KeyCode.P, 0x50 },
            { KeyCode.Q, 0x51 }, { KeyCode.R, 0x52 }, { KeyCode.S, 0x53 }, { KeyCode.T, 0x54 },
            { KeyCode.U, 0x55 }, { KeyCode.V, 0x56 }, { KeyCode.W, 0x57 }, { KeyCode.X, 0x58 },
            { KeyCode.Y, 0x59 }, { KeyCode.Z, 0x5A },
            // Numbers (VK_0=0x30 through VK_9=0x39)
            { KeyCode.Alpha0, 0x30 }, { KeyCode.Alpha1, 0x31 }, { KeyCode.Alpha2, 0x32 },
            { KeyCode.Alpha3, 0x33 }, { KeyCode.Alpha4, 0x34 }, { KeyCode.Alpha5, 0x35 },
            { KeyCode.Alpha6, 0x36 }, { KeyCode.Alpha7, 0x37 }, { KeyCode.Alpha8, 0x38 },
            { KeyCode.Alpha9, 0x39 },
            // Function keys (VK_F1=0x70 through VK_F8=0x77)
            { KeyCode.F1, 0x70 }, { KeyCode.F2, 0x71 }, { KeyCode.F3, 0x72 }, { KeyCode.F4, 0x73 },
            { KeyCode.F5, 0x74 }, { KeyCode.F6, 0x75 }, { KeyCode.F7, 0x76 }, { KeyCode.F8, 0x77 },
            // Arrows
            { KeyCode.UpArrow, 0x26 }, { KeyCode.DownArrow, 0x28 },
            { KeyCode.LeftArrow, 0x25 }, { KeyCode.RightArrow, 0x27 },
            // Control keys
            { KeyCode.Return, 0x0D }, { KeyCode.Escape, 0x1B }, { KeyCode.Backspace, 0x08 },
            { KeyCode.Tab, 0x09 }, { KeyCode.Space, 0x20 },
            // Navigation
            { KeyCode.Home, 0x24 }, { KeyCode.End, 0x23 },
            // Modifiers
            { KeyCode.LeftShift, 0xA0 }, { KeyCode.RightShift, 0xA1 },
            { KeyCode.LeftControl, 0xA2 }, { KeyCode.RightControl, 0xA3 },
            { KeyCode.LeftAlt, 0xA4 }, { KeyCode.RightAlt, 0xA5 },
            { KeyCode.LeftWindows, 0x5B }, { KeyCode.RightWindows, 0x5C },
            // Punctuation (OEM keys)
            { KeyCode.Minus, 0xBD }, { KeyCode.Equals, 0xBB },
            { KeyCode.LeftBracket, 0xDB }, { KeyCode.RightBracket, 0xDD },
            { KeyCode.Backslash, 0xDC }, { KeyCode.Semicolon, 0xBA },
            { KeyCode.Quote, 0xDE }, { KeyCode.BackQuote, 0xC0 },
            { KeyCode.Comma, 0xBC }, { KeyCode.Period, 0xBE },
            { KeyCode.Slash, 0xBF },
        };

        /// <summary>
        /// Check if a Unity KeyCode was pressed this frame via GetAsyncKeyState.
        /// Replaces Input.GetKeyDown(KeyCode) for all mod keyboard reading.
        /// </summary>
        public static bool IsKeyCodePressed(KeyCode keyCode)
        {
            if (KeyCodeToVK.TryGetValue(keyCode, out int vk))
                return IsKeyPressed(vk);
            return false;
        }

        /// <summary>
        /// Check if a Unity KeyCode is held via GetAsyncKeyState.
        /// Replaces Input.GetKey(KeyCode) for modifier detection.
        /// </summary>
        public static bool IsKeyCodeHeld(KeyCode keyCode)
        {
            if (KeyCodeToVK.TryGetValue(keyCode, out int vk))
                return IsKeyHeld(vk);
            return false;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        public static void Initialize()
        {
            try
            {
                // Subsystem-scoped init (not SDL_Init) so the gamepad and audio subsystems are
                // reference-counted independently — AudioEngine inits SDL_INIT_AUDIO separately
                // and neither teardown tears down the other.
                if (!SDL3.SDL_InitSubSystem(SDL3.SDL_INIT_GAMEPAD))
                {
                    MelonLogger.Error("[GamepadManager] SDL_InitSubSystem(GAMEPAD) failed");
                    return;
                }
                sdlInitialized = true;

                OpenFirstGamepad();

                if (gamepadHandle != IntPtr.Zero)
                    DisableUnityGamepad();
                else
                    MelonLogger.Warning("[GamepadManager] SDL3 initialized but no gamepad found");
            }
            catch (DllNotFoundException)
            {
                MelonLogger.Error("[GamepadManager] SDL3.dll not found — gamepad and SDL keyboard features disabled");
                sdlInitialized = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GamepadManager] Initialization failed: {ex.Message}");
                sdlInitialized = false;
            }
        }

        public static void Shutdown()
        {
            // Re-arm both primings so an Initialize → Shutdown → Initialize cycle starts
            // from a seeded snapshot rather than an all-false one.
            keyboardPrimed = false;
            gamepadPrimed = false;

            if (gamepadHandle != IntPtr.Zero)
            {
                try { SDL3.SDL_CloseGamepad(gamepadHandle); } catch { }
                gamepadHandle = IntPtr.Zero;
            }

            if (sdlInitialized)
            {
                // Quit ONLY the gamepad subsystem — never blanket SDL_Quit(), which would also
                // kill the audio device AudioEngine owns. SDL fully cleans up when its last
                // subsystem quits.
                try { SDL3.SDL_QuitSubSystem(SDL3.SDL_INIT_GAMEPAD); } catch { }
                sdlInitialized = false;
            }

            EnableUnityGamepad();
        }

        private static bool unityGamepadDisabled;

        private static bool IsGamepadLike(UnityEngine.InputSystem.InputDevice device)
        {
            // TryCast (Il2CppInterop) — `is T` checks the managed-side type, but
            // InputSystem.devices returns Il2Cpp-wrapped objects, so `is Gamepad`
            // silently returns false for every device. TryCast is the canonical
            // Il2Cpp type-check pattern used throughout this project.
            return device.TryCast<UnityEngine.InputSystem.Gamepad>() != null
                || device.TryCast<UnityEngine.InputSystem.Joystick>() != null;
        }

        /// <summary>
        /// Disables every Unity InputSystem gamepad/joystick device so the game cannot
        /// read controller input through Unity's stack — SDL is the sole source. On
        /// reconnect Unity creates a fresh device object (Gamepad.current may be null or
        /// stale for a frame), so iterating all devices is required, not just .current.
        /// </summary>
        private static void DisableUnityGamepad()
        {
            try
            {
                int disabled = 0;
                foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (device == null || !IsGamepadLike(device)) continue;
                    if (!device.enabled) continue;

                    UnityEngine.InputSystem.InputSystem.DisableDevice(device);
                    disabled++;
                }
                if (disabled > 0)
                {
                    unityGamepadDisabled = true;
                    MelonLogger.Msg($"[GamepadManager] Disabled {disabled} Unity gamepad/joystick device(s)");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GamepadManager] Could not disable Unity gamepad: {ex.Message}");
            }
        }

        private static void EnableUnityGamepad()
        {
            if (!unityGamepadDisabled) return;
            try
            {
                foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (device == null || !IsGamepadLike(device)) continue;
                    if (device.enabled) continue;

                    UnityEngine.InputSystem.InputSystem.EnableDevice(device);
                }
                unityGamepadDisabled = false;
            }
            catch { }
        }

        /// <summary>
        /// Guard: re-disable if Unity has re-enabled (scene transitions, hotplug, etc.).
        /// Called from Update each frame so we catch devices Unity registers asynchronously
        /// after the SDL ADDED event has already fired.
        /// </summary>
        private static void EnsureUnityGamepadDisabled()
        {
            if (!IsAvailable) return;
            try
            {
                foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (device == null || !IsGamepadLike(device)) continue;
                    if (device.enabled)
                    {
                        DisableUnityGamepad();
                        return;
                    }
                }
            }
            catch { }
        }

        private static void OpenFirstGamepad()
        {
            IntPtr ids = SDL3.SDL_GetGamepads(out int count);
            if (ids == IntPtr.Zero || count <= 0)
                return;

            try
            {
                uint firstId = (uint)Marshal.ReadInt32(ids, 0);
                OpenGamepad(firstId);
            }
            finally
            {
                SDL3.SDL_free(ids);
            }
        }

        /// <summary>
        /// Opens the gamepad with the specified SDL instance ID and sets gamepadHandle/Type.
        /// Logs success or failure. Called from OpenFirstGamepad (startup, fallback) and
        /// directly from HandleGamepadAdded (using the event's instance ID).
        /// </summary>
        private static void OpenGamepad(uint instanceId)
        {
            gamepadHandle = SDL3.SDL_OpenGamepad(instanceId);
            if (gamepadHandle != IntPtr.Zero)
            {
                gamepadType = SDL3.SDL_GetGamepadType(gamepadHandle);
                IntPtr namePtr = SDL3.SDL_GetGamepadName(gamepadHandle);
                string name = namePtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(namePtr)
                    : "Unknown";
                MelonLogger.Msg($"[GamepadManager] Opened gamepad id={instanceId}, name={name}, type={ControllerLabels.GetControllerTypeName(gamepadType)}");
            }
            else
            {
                MelonLogger.Warning($"[GamepadManager] SDL_OpenGamepad({instanceId}) returned null");
            }
        }

        // =====================================================================
        // Per-frame polling (called from InputManager.Update)
        // =====================================================================

        public static void Update()
        {
            // Keyboard via GetAsyncKeyState — always works, no SDL needed
            PollKeyboard();

            if (!sdlInitialized)
                return;

            SDL3.SDL_PumpEvents();

            // Drain pending events — pick out gamepad add/remove so the mod recovers
            // from controllers unplugged/replugged mid-session. Keyboard polls state
            // directly via GetAsyncKeyState, so draining the SDL event queue is safe.
            while (SDL3.SDL_PollEvent(out var evt))
            {
                if (evt.type == SDL3.SDL_EVENT_GAMEPAD_REMOVED)
                    HandleGamepadRemoved(evt.gdevice_which);
                else if (evt.type == SDL3.SDL_EVENT_GAMEPAD_ADDED)
                    HandleGamepadAdded(evt.gdevice_which);
            }

            if (gamepadHandle != IntPtr.Zero)
            {
                PollGamepad();
                EnsureUnityGamepadDisabled();
            }
        }

        private static void HandleGamepadRemoved(uint id)
        {
            try
            {
                if (gamepadHandle == IntPtr.Zero)
                    return;

                // Only react to the removal of the gamepad we currently own.
                uint ownId = SDL3.SDL_GetGamepadID(gamepadHandle);
                if (ownId != id)
                    return;

                CloseAndClearGamepadState();
                ControllerRouter.OnGamepadRemoved();
                MelonLogger.Msg("[GamepadManager] Controller disconnected");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GamepadManager] Disconnect handling failed: {ex.Message}");
            }
        }

        private static void HandleGamepadAdded(uint instanceId)
        {
            try
            {
                // Hot-swap support: if we already hold a handle (player swapped
                // controllers without an intervening REMOVED), close it first.
                if (gamepadHandle != IntPtr.Zero)
                    CloseAndClearGamepadState();

                OpenGamepad(instanceId);

                if (gamepadHandle != IntPtr.Zero)
                    DisableUnityGamepad();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GamepadManager] Reconnect handling failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the SDL gamepad handle and wipes cached input state. Shared by
        /// disconnect handling and hot-swap-while-open recovery.
        /// </summary>
        private static void CloseAndClearGamepadState()
        {
            try { SDL3.SDL_CloseGamepad(gamepadHandle); } catch { }
            gamepadHandle = IntPtr.Zero;
            gamepadType = SDL3.SDL_GAMEPAD_TYPE_UNKNOWN;

            // Wipe edge-detection state so a reconnect doesn't synthesize fake "released"
            // or "pressed" transitions from the stale snapshot. Clearing alone is not
            // enough for the "pressed" half — an all-false previous snapshot turns any
            // button held at reconnect into a fresh press — so re-arm priming as well.
            gamepadPrimed = false;
            Array.Clear(btnCurrent, 0, btnCurrent.Length);
            Array.Clear(btnPrevious, 0, btnPrevious.Length);
            Array.Clear(rstickCurrent, 0, rstickCurrent.Length);
            Array.Clear(rstickPrevious, 0, rstickPrevious.Length);
            Array.Clear(lstickCurrent, 0, lstickCurrent.Length);
            Array.Clear(lstickPrevious, 0, lstickPrevious.Length);
            LeftStickX = LeftStickY = RightStickX = RightStickY = 0f;
            LeftTrigger = RightTrigger = 0f;
        }

        /// <summary>
        /// Seeds the keyboard snapshot on the very first poll. Without it keyPrevious[] is
        /// all-false, so any key physically down on the mod's first input frame satisfies
        /// "current && !previous" and dispatches as a fresh press — which is how a held G
        /// announced gil on the boot screen with nobody touching the keyboard. The mod's
        /// first poll can land seconds after load (SDL drains its queued GAMEPAD_ADDED on
        /// the same frame), so there is plenty of time for a key to be down.
        /// </summary>
        private static bool keyboardPrimed;

        private static void PollKeyboard()
        {
            anyKeyDownThisFrame = false;

            bool priming = !keyboardPrimed;

            for (int i = 0; i < TrackedVKCodes.Length; i++)
            {
                int vk = TrackedVKCodes[i];
                bool down = (GetAsyncKeyState(vk) & 0x8000) != 0;

                // Priming: previous := current, so a key already held produces no edge.
                keyPrevious[vk] = priming ? down : keyCurrent[vk];
                keyCurrent[vk] = down;

                if (keyCurrent[vk] && !keyPrevious[vk])
                    anyKeyDownThisFrame = true;
            }

            if (priming)
            {
                keyboardPrimed = true;
                LogPrimedKeys();
            }
        }

        /// <summary>
        /// One-shot: names any keys found held on the priming frame. If a spurious
        /// announcement ever reappears at load, this line is the difference between
        /// "a key really was down" and "the cause is somewhere else entirely".
        /// </summary>
        private static void LogPrimedKeys()
        {
            var held = new List<string>();
            for (int i = 0; i < TrackedVKCodes.Length; i++)
            {
                int vk = TrackedVKCodes[i];
                if (keyCurrent[vk])
                    held.Add($"0x{vk:X2}");
            }

            MelonLogger.Msg(held.Count == 0
                ? "[GamepadManager] Keyboard primed, no keys held"
                : $"[GamepadManager] Keyboard primed, ignoring held keys: {string.Join(", ", held)}");
        }

        /// <summary>
        /// Same priming as the keyboard, re-armed on every open. CloseAndClearGamepadState()
        /// zeroes both button arrays, so a button (or a held stick) that survives a
        /// disconnect/reconnect would otherwise synthesize a press on the next poll.
        /// </summary>
        private static bool gamepadPrimed;

        private static void PollGamepad()
        {
            bool priming = !gamepadPrimed;

            // Buttons: copy current to previous, read all 15
            for (int i = 0; i < SDL3.SDL_GAMEPAD_BUTTON_COUNT; i++)
            {
                bool down = SDL3.SDL_GetGamepadButton(gamepadHandle, i);
                btnPrevious[i] = priming ? down : btnCurrent[i];
                btnCurrent[i] = down;
            }

            // Left stick
            short rawLX = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_LEFTX);
            short rawLY = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_LEFTY);
            LeftStickX = ApplyDeadzone(rawLX);
            LeftStickY = -ApplyDeadzone(rawLY); // SDL Y positive=down, Unity positive=up

            // Right stick
            short rawRX = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_RIGHTX);
            short rawRY = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_RIGHTY);
            RightStickX = ApplyDeadzone(rawRX);
            RightStickY = -ApplyDeadzone(rawRY);

            // Triggers (0-1 range, no deadzone needed)
            short rawLT = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_LEFT_TRIGGER);
            short rawRT = SDL3.SDL_GetGamepadAxis(gamepadHandle, SDL3.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER);
            LeftTrigger = Math.Max(0, (int)rawLT) / 32767f;
            RightTrigger = Math.Max(0, (int)rawRT) / 32767f;

            // Right stick virtual directions (cardinal isolation)
            for (int i = 0; i < 4; i++) rstickPrevious[i] = rstickCurrent[i];
            int absRX = rawRX < 0 ? -rawRX : rawRX;
            int absRY = rawRY < 0 ? -rawRY : rawRY;
            bool yDom = absRY > RSTICK_THRESHOLD && absRY > absRX + RSTICK_DOMINANCE;
            bool xDom = absRX > RSTICK_THRESHOLD && absRX > absRY + RSTICK_DOMINANCE;
            rstickCurrent[DIR_UP] = yDom && rawRY < 0;
            rstickCurrent[DIR_DOWN] = yDom && rawRY > 0;
            rstickCurrent[DIR_LEFT] = xDom && rawRX < 0;
            rstickCurrent[DIR_RIGHT] = xDom && rawRX > 0;

            // Left stick virtual directions (for menu navigation edge detection)
            for (int i = 0; i < 4; i++) lstickPrevious[i] = lstickCurrent[i];
            lstickCurrent[DIR_UP] = rawLY < -LSTICK_DIR_THRESHOLD;
            lstickCurrent[DIR_DOWN] = rawLY > LSTICK_DIR_THRESHOLD;
            lstickCurrent[DIR_LEFT] = rawLX < -LSTICK_DIR_THRESHOLD;
            lstickCurrent[DIR_RIGHT] = rawLX > LSTICK_DIR_THRESHOLD;

            // Sticks prime the same way: previous := current, so a stick already deflected
            // when the pad opens produces no direction edge. Done after the direction
            // computation because these are derived values, not raw reads.
            if (priming)
            {
                for (int i = 0; i < 4; i++)
                {
                    rstickPrevious[i] = rstickCurrent[i];
                    lstickPrevious[i] = lstickCurrent[i];
                }
                gamepadPrimed = true;
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static float ApplyDeadzone(short raw)
        {
            if (raw > -AXIS_DEADZONE && raw < AXIS_DEADZONE)
                return 0f;

            float max = 32767f;
            float dz = AXIS_DEADZONE;
            if (raw > 0)
                return (raw - dz) / (max - dz);
            else
                return (raw + dz) / (max - dz);
        }
    }
}
