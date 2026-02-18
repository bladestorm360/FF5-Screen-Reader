using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using FFV_ScreenReader.Utils;
using MelonLoader;
using Il2CppSerial.FF5.UI.KeyInput;
using FFV_ScreenReader.Menus;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using LibraryInfoController_KeyInput = Il2CppLast.UI.KeyInput.LibraryInfoController;

namespace FFV_ScreenReader.Core
{
    public class InputManager
    {
        private readonly FFV_ScreenReaderMod mod;
        private StatusDetailsController cachedStatusController;
        private LibraryInfoController_KeyInput cachedBestiaryInfoController;
        private readonly KeyBindingRegistry registry = new KeyBindingRegistry();

        #region Static SDL State

        private static IntPtr _sdlWindow = IntPtr.Zero;
        private static bool _sdlInitialized;
        private static bool _hasFocus;

        // Current frame key states (used for edge detection and held checks)
        private static readonly Dictionary<ModKey, bool> _currState = new Dictionary<ModKey, bool>();

        // ModKey -> SDL scancode mapping (built once during SDL init)
        private static readonly Dictionary<ModKey, int> _sdlScancodeMap = new Dictionary<ModKey, int>();

        // Cached keyboard state pointer from SDL (valid for app lifetime)
        private static IntPtr _sdlKeyStatePtr = IntPtr.Zero;
        private static int _sdlKeyStateLen;

        // Track whether any key went down this frame
        private static bool _anyKeyDown;

        #endregion

        public InputManager(FFV_ScreenReaderMod mod)
        {
            this.mod = mod;
            InitializeBindings();
        }

        #region SDL Init / Shutdown (Static)

        /// <summary>
        /// Initializes SDL video subsystem and creates a hidden window for focus stealing.
        /// Non-fatal: if SDL3.dll is missing, background keyboard mode still works.
        /// </summary>
        public static void InitializeSDL()
        {
            try
            {
                if (!SDL3Interop.SDL_Init(SDL3Interop.SDL_INIT_VIDEO))
                {
                    MelonLogger.Warning($"[InputManager] SDL_Init failed: {SDL3Interop.GetError()}");
                    return;
                }

                _sdlWindow = SDL3Interop.SDL_CreateWindow("FFV_ScreenReader", 1, 1, SDL3Interop.SDL_WINDOW_HIDDEN);
                if (_sdlWindow == IntPtr.Zero)
                {
                    MelonLogger.Warning($"[InputManager] SDL_CreateWindow failed: {SDL3Interop.GetError()}");
                    SDL3Interop.SDL_Quit();
                    return;
                }

                // Cache keyboard state pointer (valid for entire app lifetime)
                _sdlKeyStatePtr = SDL3Interop.SDL_GetKeyboardState(out _sdlKeyStateLen);

                BuildScancodeMap();

                _sdlInitialized = true;
                MelonLogger.Msg("[InputManager] SDL initialized successfully");
            }
            catch (DllNotFoundException)
            {
                MelonLogger.Warning("[InputManager] SDL3.dll not found. Modal dialogs will be unavailable. Background keyboard input works.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[InputManager] SDL init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down SDL. Called during mod unload.
        /// </summary>
        public static void ShutdownSDL()
        {
            try
            {
                if (_sdlWindow != IntPtr.Zero)
                {
                    SDL3Interop.SDL_DestroyWindow(_sdlWindow);
                    _sdlWindow = IntPtr.Zero;
                }
                if (_sdlInitialized)
                {
                    SDL3Interop.SDL_Quit();
                    _sdlInitialized = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[InputManager] SDL shutdown error: {ex.Message}");
            }
        }

        /// <summary>
        /// Whether SDL initialized successfully (modal dialogs available).
        /// </summary>
        public static bool IsSDLAvailable => _sdlInitialized;

        #endregion

        #region Focus Stealing (Static)

        /// <summary>
        /// Steals focus from the game by showing the hidden SDL window with the given title.
        /// NVDA announces the window title for accessibility.
        /// </summary>
        public static void StealFocus(string windowName)
        {
            if (!_sdlInitialized || _sdlWindow == IntPtr.Zero)
            {
                MelonLogger.Warning("[InputManager] Cannot steal focus: SDL not initialized");
                return;
            }

            try
            {
                SDL3Interop.SDL_SetWindowTitle(_sdlWindow, windowName);
                SDL3Interop.SDL_ShowWindow(_sdlWindow);
                SDL3Interop.SDL_RaiseWindow(_sdlWindow);
                _hasFocus = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[InputManager] Error stealing focus: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores focus to the game by hiding the SDL window.
        /// </summary>
        public static void RestoreFocus()
        {
            if (!_sdlInitialized || _sdlWindow == IntPtr.Zero)
                return;

            try
            {
                SDL3Interop.SDL_HideWindow(_sdlWindow);
                _hasFocus = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[InputManager] Error restoring focus: {ex.Message}");
            }
        }

        #endregion

        #region Polling & Key State (Static)

        /// <summary>
        /// Polls a specific set of keys for edge detection. Used by modal dialogs
        /// which have their own key sets and run before the main poll.
        /// </summary>
        public static void Poll(ModKey[] trackedKeys)
        {
            _justPressed.Clear();
            _anyKeyDown = false;

            if (_hasFocus && _sdlInitialized)
            {
                SDL3Interop.SDL_PumpEvents();

                foreach (var key in trackedKeys)
                {
                    bool wasPressed = _currState.TryGetValue(key, out var prev) && prev;
                    bool isPressed = IsKeyHeldSDL(key);

                    _currState[key] = isPressed;

                    if (isPressed && !wasPressed)
                    {
                        _justPressed.Add(key);
                        _anyKeyDown = true;
                    }
                }
            }
            else
            {
                if (!IsGameProcessForeground())
                    return;

                foreach (var key in trackedKeys)
                {
                    bool wasPressed = _currState.TryGetValue(key, out var prev) && prev;
                    bool isPressed = (GetAsyncKeyState((int)key) & 0x8000) != 0;

                    _currState[key] = isPressed;

                    if (isPressed && !wasPressed)
                    {
                        _justPressed.Add(key);
                        _anyKeyDown = true;
                    }
                }
            }
        }

        /// <summary>
        /// Whether any tracked key was just pressed this frame (edge-detected).
        /// </summary>
        public static bool AnyKeyDown => _anyKeyDown;

        // Tracks which keys had a rising edge this frame (released -> pressed)
        private static readonly HashSet<ModKey> _justPressed = new HashSet<ModKey>();

        /// <summary>
        /// Whether the given key was just pressed this frame.
        /// </summary>
        public static bool IsKeyDown(ModKey key)
        {
            return _justPressed.Contains(key);
        }

        /// <summary>
        /// Whether the given key is currently held (raw state, not edge-detected).
        /// Used for modifier detection (Shift, Ctrl).
        /// </summary>
        public static bool IsKeyHeld(ModKey key)
        {
            return _currState.TryGetValue(key, out var held) && held;
        }

        /// <summary>
        /// Returns the current modifier key state.
        /// </summary>
        public static KeyModifier GetModifiers()
        {
            bool shift = IsKeyHeld(ModKey.LeftShift) || IsKeyHeld(ModKey.RightShift);
            bool ctrl = IsKeyHeld(ModKey.LeftControl) || IsKeyHeld(ModKey.RightControl);

            if (ctrl && shift) return KeyModifier.CtrlShift;
            if (ctrl) return KeyModifier.Ctrl;
            if (shift) return KeyModifier.Shift;
            return KeyModifier.None;
        }

        /// <summary>
        /// Snapshots current key states to prevent keys held during dialog open from
        /// triggering immediately. Call when opening a modal dialog.
        /// </summary>
        public static void InitializeKeyStates(ModKey[] keys)
        {
            _currState.Clear();
            _justPressed.Clear();

            if (_hasFocus && _sdlInitialized)
                SDL3Interop.SDL_PumpEvents();

            foreach (var key in keys)
            {
                bool pressed;
                if (_hasFocus && _sdlInitialized)
                    pressed = IsKeyHeldSDL(key);
                else
                    pressed = IsGameProcessForeground() && (GetAsyncKeyState((int)key) & 0x8000) != 0;

                _currState[key] = pressed;
            }
        }

        /// <summary>
        /// Reads SDL keyboard state for a single key via scancode mapping.
        /// </summary>
        private static bool IsKeyHeldSDL(ModKey key)
        {
            if (_sdlKeyStatePtr == IntPtr.Zero) return false;
            if (!_sdlScancodeMap.TryGetValue(key, out int scancode)) return false;
            if (scancode < 0 || scancode >= _sdlKeyStateLen) return false;

            unsafe
            {
                byte* states = (byte*)_sdlKeyStatePtr;
                return states[scancode] != 0;
            }
        }

        #endregion

        #region Scancode Map Builder

        private static void BuildScancodeMap()
        {
            _sdlScancodeMap.Clear();

            // Letters A-Z
            for (int i = 0; i < 26; i++)
                _sdlScancodeMap[(ModKey)(0x41 + i)] = SDL3Interop.SDL_SCANCODE_A + i;

            // Numbers 0-9 (SDL scancodes: 1-9 are 30-38, 0 is 39)
            _sdlScancodeMap[ModKey.Alpha1] = SDL3Interop.SDL_SCANCODE_1;
            _sdlScancodeMap[ModKey.Alpha2] = SDL3Interop.SDL_SCANCODE_2;
            _sdlScancodeMap[ModKey.Alpha3] = SDL3Interop.SDL_SCANCODE_3;
            _sdlScancodeMap[ModKey.Alpha4] = SDL3Interop.SDL_SCANCODE_4;
            _sdlScancodeMap[ModKey.Alpha5] = SDL3Interop.SDL_SCANCODE_5;
            _sdlScancodeMap[ModKey.Alpha6] = SDL3Interop.SDL_SCANCODE_6;
            _sdlScancodeMap[ModKey.Alpha7] = SDL3Interop.SDL_SCANCODE_7;
            _sdlScancodeMap[ModKey.Alpha8] = SDL3Interop.SDL_SCANCODE_8;
            _sdlScancodeMap[ModKey.Alpha9] = SDL3Interop.SDL_SCANCODE_9;
            _sdlScancodeMap[ModKey.Alpha0] = SDL3Interop.SDL_SCANCODE_0;

            // Navigation
            _sdlScancodeMap[ModKey.Return]     = SDL3Interop.SDL_SCANCODE_RETURN;
            _sdlScancodeMap[ModKey.Escape]     = SDL3Interop.SDL_SCANCODE_ESCAPE;
            _sdlScancodeMap[ModKey.Backspace]  = SDL3Interop.SDL_SCANCODE_BACKSPACE;
            _sdlScancodeMap[ModKey.Tab]        = SDL3Interop.SDL_SCANCODE_TAB;
            _sdlScancodeMap[ModKey.Space]      = SDL3Interop.SDL_SCANCODE_SPACE;
            _sdlScancodeMap[ModKey.Home]       = SDL3Interop.SDL_SCANCODE_HOME;
            _sdlScancodeMap[ModKey.End]        = SDL3Interop.SDL_SCANCODE_END;
            _sdlScancodeMap[ModKey.LeftArrow]  = SDL3Interop.SDL_SCANCODE_LEFT;
            _sdlScancodeMap[ModKey.UpArrow]    = SDL3Interop.SDL_SCANCODE_UP;
            _sdlScancodeMap[ModKey.RightArrow] = SDL3Interop.SDL_SCANCODE_RIGHT;
            _sdlScancodeMap[ModKey.DownArrow]  = SDL3Interop.SDL_SCANCODE_DOWN;

            // Function keys
            _sdlScancodeMap[ModKey.F1] = SDL3Interop.SDL_SCANCODE_F1;
            _sdlScancodeMap[ModKey.F3] = SDL3Interop.SDL_SCANCODE_F3;
            _sdlScancodeMap[ModKey.F5] = SDL3Interop.SDL_SCANCODE_F5;
            _sdlScancodeMap[ModKey.F8] = SDL3Interop.SDL_SCANCODE_F8;

            // Modifiers
            _sdlScancodeMap[ModKey.LeftShift]    = SDL3Interop.SDL_SCANCODE_LSHIFT;
            _sdlScancodeMap[ModKey.RightShift]   = SDL3Interop.SDL_SCANCODE_RSHIFT;
            _sdlScancodeMap[ModKey.LeftControl]  = SDL3Interop.SDL_SCANCODE_LCTRL;
            _sdlScancodeMap[ModKey.RightControl] = SDL3Interop.SDL_SCANCODE_RCTRL;

            // Punctuation
            _sdlScancodeMap[ModKey.Semicolon]    = SDL3Interop.SDL_SCANCODE_SEMICOLON;
            _sdlScancodeMap[ModKey.Equals]       = SDL3Interop.SDL_SCANCODE_EQUALS;
            _sdlScancodeMap[ModKey.Comma]        = SDL3Interop.SDL_SCANCODE_COMMA;
            _sdlScancodeMap[ModKey.Minus]        = SDL3Interop.SDL_SCANCODE_MINUS;
            _sdlScancodeMap[ModKey.Period]       = SDL3Interop.SDL_SCANCODE_PERIOD;
            _sdlScancodeMap[ModKey.Slash]        = SDL3Interop.SDL_SCANCODE_SLASH;
            _sdlScancodeMap[ModKey.Backtick]     = SDL3Interop.SDL_SCANCODE_GRAVE;
            _sdlScancodeMap[ModKey.LeftBracket]  = SDL3Interop.SDL_SCANCODE_LEFTBRACKET;
            _sdlScancodeMap[ModKey.Backslash]    = SDL3Interop.SDL_SCANCODE_BACKSLASH;
            _sdlScancodeMap[ModKey.RightBracket] = SDL3Interop.SDL_SCANCODE_RIGHTBRACKET;
            _sdlScancodeMap[ModKey.Quote]        = SDL3Interop.SDL_SCANCODE_APOSTROPHE;
        }

        #endregion

        #region Instance Methods (Dispatch, Context)

        /// <summary>
        /// Registers a field-only binding with a "Not available in battle" fallback for the Battle context.
        /// </summary>
        private void RegisterFieldWithBattleFeedback(ModKey key, KeyModifier modifier, Action action, string description)
        {
            registry.Register(key, modifier, KeyContext.Field, action, description);
            registry.Register(key, modifier, KeyContext.Battle, NotAvailableInBattle, description + " (battle blocked)");
            registry.Register(key, modifier, KeyContext.Global, NotOnMap, description + " (no map)");
        }

        private static void NotAvailableInBattle()
        {
            FFV_ScreenReaderMod.SpeakText("Not available in battle", interrupt: true);
        }

        private static void NotOnMap()
        {
            FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
        }

        private void InitializeBindings()
        {
            // --- Status screen: arrow key navigation ---
            registry.Register(ModKey.DownArrow, KeyModifier.Ctrl, KeyContext.Status, StatusNavigationReader.JumpToBottom, "Jump to bottom stat");
            registry.Register(ModKey.DownArrow, KeyModifier.Shift, KeyContext.Status, StatusNavigationReader.JumpToNextGroup, "Jump to next stat group");
            registry.Register(ModKey.DownArrow, KeyModifier.None, KeyContext.Status, StatusNavigationReader.NavigateNext, "Next stat");
            registry.Register(ModKey.UpArrow, KeyModifier.Ctrl, KeyContext.Status, StatusNavigationReader.JumpToTop, "Jump to top stat");
            registry.Register(ModKey.UpArrow, KeyModifier.Shift, KeyContext.Status, StatusNavigationReader.JumpToPreviousGroup, "Jump to previous stat group");
            registry.Register(ModKey.UpArrow, KeyModifier.None, KeyContext.Status, StatusNavigationReader.NavigatePrevious, "Previous stat");

            // Status screen: bulk stat reading
            registry.Register(ModKey.LeftBracket, KeyContext.Status, () => FFV_ScreenReaderMod.SpeakText(StatusDetailsReader.ReadPhysicalStats()), "Read physical stats");
            registry.Register(ModKey.RightBracket, KeyContext.Status, () => FFV_ScreenReaderMod.SpeakText(StatusDetailsReader.ReadMagicalStats()), "Read magical stats");

            // --- Field: entity navigation (brackets + backslash) — with battle feedback ---
            RegisterFieldWithBattleFeedback(ModKey.LeftBracket, KeyModifier.Shift, mod.CyclePreviousCategory, "Previous entity category");
            RegisterFieldWithBattleFeedback(ModKey.LeftBracket, KeyModifier.None, mod.CyclePrevious, "Previous entity");
            RegisterFieldWithBattleFeedback(ModKey.RightBracket, KeyModifier.Shift, mod.CycleNextCategory, "Next entity category");
            RegisterFieldWithBattleFeedback(ModKey.RightBracket, KeyModifier.None, mod.CycleNext, "Next entity");
            RegisterFieldWithBattleFeedback(ModKey.Backslash, KeyModifier.Ctrl, mod.ToggleToLayerFilter, "Toggle layer filter");
            RegisterFieldWithBattleFeedback(ModKey.Backslash, KeyModifier.Shift, mod.TogglePathfindingFilter, "Toggle pathfinding filter");
            RegisterFieldWithBattleFeedback(ModKey.Backslash, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity");

            // --- Field: pathfinding alternate keys (J/K/L/P) — with battle feedback ---
            RegisterFieldWithBattleFeedback(ModKey.J, KeyModifier.Shift, mod.CyclePreviousCategory, "Previous entity category (alt)");
            RegisterFieldWithBattleFeedback(ModKey.J, KeyModifier.None, mod.CyclePrevious, "Previous entity (alt)");
            RegisterFieldWithBattleFeedback(ModKey.K, KeyModifier.Shift, mod.AnnounceCurrentEntity, "Announce current entity (alt shift)");
            RegisterFieldWithBattleFeedback(ModKey.K, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity (alt)");
            RegisterFieldWithBattleFeedback(ModKey.L, KeyModifier.Shift, mod.CycleNextCategory, "Next entity category (alt)");
            RegisterFieldWithBattleFeedback(ModKey.L, KeyModifier.None, mod.CycleNext, "Next entity (alt)");
            RegisterFieldWithBattleFeedback(ModKey.P, KeyModifier.Shift, mod.TogglePathfindingFilter, "Toggle pathfinding filter (alt)");
            RegisterFieldWithBattleFeedback(ModKey.P, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity (alt)");

            // --- Field: waypoint keys (with Global fallback) ---
            registry.Register(ModKey.Comma, KeyModifier.Shift, KeyContext.Field, mod.CyclePreviousWaypointCategory, "Previous waypoint category");
            registry.Register(ModKey.Comma, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Previous waypoint category (no map)");
            registry.Register(ModKey.Comma, KeyModifier.None, KeyContext.Field, mod.CyclePreviousWaypoint, "Previous waypoint");
            registry.Register(ModKey.Comma, KeyModifier.None, KeyContext.Global, NotOnMap, "Previous waypoint (no map)");
            registry.Register(ModKey.Period, KeyModifier.Ctrl, KeyContext.Field, mod.RenameCurrentWaypoint, "Rename waypoint");
            registry.Register(ModKey.Period, KeyModifier.Ctrl, KeyContext.Global, NotOnMap, "Rename waypoint (no map)");
            registry.Register(ModKey.Period, KeyModifier.Shift, KeyContext.Field, mod.CycleNextWaypointCategory, "Next waypoint category");
            registry.Register(ModKey.Period, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Next waypoint category (no map)");
            registry.Register(ModKey.Period, KeyModifier.None, KeyContext.Field, mod.CycleNextWaypoint, "Next waypoint");
            registry.Register(ModKey.Period, KeyModifier.None, KeyContext.Global, NotOnMap, "Next waypoint (no map)");
            registry.Register(ModKey.Slash, KeyModifier.CtrlShift, KeyContext.Field, mod.ClearAllWaypointsForMap, "Clear all waypoints for map");
            registry.Register(ModKey.Slash, KeyModifier.CtrlShift, KeyContext.Global, NotOnMap, "Clear all waypoints (no map)");
            registry.Register(ModKey.Slash, KeyModifier.Ctrl, KeyContext.Field, mod.RemoveCurrentWaypoint, "Remove current waypoint");
            registry.Register(ModKey.Slash, KeyModifier.Ctrl, KeyContext.Global, NotOnMap, "Remove waypoint (no map)");
            registry.Register(ModKey.Slash, KeyModifier.Shift, KeyContext.Field, mod.AddNewWaypointWithNaming, "Add waypoint with name");
            registry.Register(ModKey.Slash, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Add waypoint (no map)");
            registry.Register(ModKey.Slash, KeyModifier.None, KeyContext.Field, mod.PathfindToCurrentWaypoint, "Pathfind to waypoint");
            registry.Register(ModKey.Slash, KeyModifier.None, KeyContext.Global, NotOnMap, "Pathfind to waypoint (no map)");

            // --- Field: teleport (Ctrl+Arrow, not on status screen — handled by context) ---
            float t = GameConstants.TILE_SIZE;
            registry.Register(ModKey.UpArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(0, t)), "Teleport north");
            registry.Register(ModKey.DownArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(0, -t)), "Teleport south");
            registry.Register(ModKey.LeftArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(-t, 0)), "Teleport west");
            registry.Register(ModKey.RightArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(t, 0)), "Teleport east");

            // --- Global: info/announcements ---
            registry.Register(ModKey.G, KeyContext.Global, mod.AnnounceGilAmount, "Announce Gil");
            registry.Register(ModKey.H, KeyContext.Global, mod.AnnounceActiveCharacterStatus, "Announce character status");
            registry.Register(ModKey.M, KeyModifier.Shift, KeyContext.Global, mod.ToggleMapExitFilter, "Toggle map exit filter");
            registry.Register(ModKey.M, KeyModifier.None, KeyContext.Global, mod.AnnounceCurrentMap, "Announce current map");
            registry.Register(ModKey.T, KeyModifier.Shift, KeyContext.Global, Patches.TimerHelper.ToggleTimerFreeze, "Toggle timer freeze");
            registry.Register(ModKey.T, KeyModifier.None, KeyContext.Global, () => Patches.TimerHelper.AnnounceActiveTimers(), "Announce active timers");

            registry.Register(ModKey.V, KeyContext.Global, HandleMovementStateKey, "Announce vehicle state");
            registry.Register(ModKey.I, KeyContext.Global, HandleItemInfoKey, "Item details");
            registry.Register(ModKey.I, KeyModifier.Shift, KeyContext.Global, AnnounceKeyHelp, "Read control tooltips");

            // --- Field-only toggles (blocked in battle with feedback) ---
            RegisterFieldWithBattleFeedback(ModKey.Quote, KeyModifier.None, mod.ToggleFootsteps, "Toggle footsteps");
            RegisterFieldWithBattleFeedback(ModKey.Semicolon, KeyModifier.Shift, mod.ToggleLandingPings, "Toggle landing pings");
            RegisterFieldWithBattleFeedback(ModKey.Semicolon, KeyModifier.None, mod.ToggleWallTones, "Toggle wall tones");
            RegisterFieldWithBattleFeedback(ModKey.Alpha9, KeyModifier.None, mod.ToggleAudioBeacons, "Toggle audio beacons");
            RegisterFieldWithBattleFeedback(ModKey.Alpha0, KeyModifier.None, EntityTranslator.EntityDump.DumpCurrentMap, "Dump entity names");
            RegisterFieldWithBattleFeedback(ModKey.Equals, KeyModifier.None, mod.CycleNextCategory, "Next entity category (global)");
            RegisterFieldWithBattleFeedback(ModKey.Minus, KeyModifier.None, mod.CyclePreviousCategory, "Previous entity category (global)");

            // --- F1: walk/run announcement (field only, with "not on map" fallback) ---
            RegisterFieldWithBattleFeedback(ModKey.F1, KeyModifier.None, HandleF1Key, "Announce walk/run state");

            // --- F3: encounter toggle announcement (field only, with "not on map" fallback) ---
            RegisterFieldWithBattleFeedback(ModKey.F3, KeyModifier.None, HandleF3Key, "Announce encounter state");

            // --- F5: enemy HP display cycle (battle only) ---
            registry.Register(ModKey.F5, KeyModifier.None, KeyContext.Battle, HandleF5Key, "Cycle enemy HP display");
            registry.Register(ModKey.F5, KeyModifier.None, KeyContext.Global, () => FFV_ScreenReaderMod.SpeakText("Only available in battle", interrupt: true), "Enemy HP (not in battle)");

            // --- Bestiary detail: arrow key navigation ---
            registry.Register(ModKey.DownArrow, KeyModifier.Ctrl, KeyContext.Bestiary, BestiaryNavigationReader.JumpToBottom, "Jump to bottom bestiary stat");
            registry.Register(ModKey.DownArrow, KeyModifier.Shift, KeyContext.Bestiary, BestiaryNavigationReader.JumpToNextGroup, "Jump to next bestiary group");
            registry.Register(ModKey.DownArrow, KeyModifier.None, KeyContext.Bestiary, BestiaryNavigationReader.NavigateNext, "Next bestiary stat");
            registry.Register(ModKey.UpArrow, KeyModifier.Ctrl, KeyContext.Bestiary, BestiaryNavigationReader.JumpToTop, "Jump to top bestiary stat");
            registry.Register(ModKey.UpArrow, KeyModifier.Shift, KeyContext.Bestiary, BestiaryNavigationReader.JumpToPreviousGroup, "Jump to previous bestiary group");
            registry.Register(ModKey.UpArrow, KeyModifier.None, KeyContext.Bestiary, BestiaryNavigationReader.NavigatePrevious, "Previous bestiary stat");

            // --- Battle result navigator (L) ---
            registry.Register(ModKey.L, KeyModifier.None, KeyContext.BattleResult, OpenBattleResultNavigator, "Open battle result details");

            // --- Manual entity rescan (last-resort recovery) ---
            registry.Register(ModKey.K, KeyModifier.Ctrl, KeyContext.Global, () => {
                MelonLogger.Msg("[Input] Ctrl+K: Manual entity scan");
                FFV_ScreenReaderMod.Instance?.ForceEntityRescan();
                FFV_ScreenReaderMod.SpeakText("Entity scan done", interrupt: true);
            }, "Manual entity scan");

            // Sort for correct modifier precedence
            registry.FinalizeRegistration();
        }

        /// <summary>
        /// Main update loop. Called once per frame from FFV_ScreenReaderMod.OnUpdate().
        /// </summary>
        public void Update()
        {
            // Handle confirmation dialog first (consumes all input when open)
            if (ConfirmationDialog.HandleInput())
                return;

            // Handle text input window next (consumes all input when open)
            if (TextInputWindow.HandleInput())
                return;

            // Handle mod menu next (consumes all input when open)
            if (ModMenu.HandleInput())
                return;

            // Handle battle result navigator (consumes all input when open)
            if (BattleResultNavigator.HandleInput())
                return;

            // Poll all registered keys + modifiers
            PollRegisteredKeys();

            if (!AnyKeyDown)
                return;

            if (IsInputFieldFocused())
                return;

            // F8: mod menu
            if (IsKeyDown(ModKey.F8))
            {
                if (IsInBattle())
                    FFV_ScreenReaderMod.SpeakText("Unavailable in battle", interrupt: true);
                else if (!IsOnValidMap())
                    FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                else
                {
                    ModMenu.Open();
                    FFV_ScreenReaderMod.SpeakText("Mod menu", interrupt: true);
                }
                return;
            }

            // Determine active context
            KeyContext activeContext = DetermineContext();
            KeyModifier currentModifiers = GetModifiers();

            // Dispatch all registered bindings
            DispatchRegisteredBindings(activeContext, currentModifiers);
        }

        /// <summary>
        /// Polls all registry keys plus modifier keys.
        /// </summary>
        private void PollRegisteredKeys()
        {
            // Build the set of keys to poll: all registered + modifiers + F8
            var keysToPoll = new HashSet<ModKey>(registry.RegisteredKeys);
            keysToPoll.Add(ModKey.LeftShift);
            keysToPoll.Add(ModKey.RightShift);
            keysToPoll.Add(ModKey.LeftControl);
            keysToPoll.Add(ModKey.RightControl);
            keysToPoll.Add(ModKey.F8);

            // Store previous state before polling
            _justPressed.Clear();
            _anyKeyDown = false;

            if (_hasFocus && _sdlInitialized)
            {
                SDL3Interop.SDL_PumpEvents();

                foreach (var key in keysToPoll)
                {
                    bool wasPressed = _currState.TryGetValue(key, out var prev) && prev;
                    bool isPressed = IsKeyHeldSDL(key);

                    _currState[key] = isPressed;

                    if (isPressed && !wasPressed)
                    {
                        _justPressed.Add(key);
                        _anyKeyDown = true;
                    }
                }
            }
            else
            {
                if (!IsGameProcessForeground())
                    return;

                foreach (var key in keysToPoll)
                {
                    bool wasPressed = _currState.TryGetValue(key, out var prev) && prev;
                    bool isPressed = (GetAsyncKeyState((int)key) & 0x8000) != 0;

                    _currState[key] = isPressed;

                    if (isPressed && !wasPressed)
                    {
                        _justPressed.Add(key);
                        _anyKeyDown = true;
                    }
                }
            }
        }

        private void DispatchRegisteredBindings(KeyContext activeContext, KeyModifier currentModifiers)
        {
            foreach (var key in registry.RegisteredKeys)
            {
                if (IsKeyDown(key))
                    registry.TryExecute(key, currentModifiers, activeContext);
            }
        }

        /// <summary>
        /// Determine the current input context based on game state.
        /// </summary>
        private KeyContext DetermineContext()
        {
            // During Event state, skip all game object access — mod should be dormant
            if (Patches.GameStatePatches.IsInEventState)
                return KeyContext.Global;

            if (IsBestiaryDetailActive())
                return KeyContext.Bestiary;

            if (IsStatusScreenActive())
                return KeyContext.Status;

            // Check for battle results screen before general battle context
            if (BattleResultDataStore.HasData)
                return KeyContext.BattleResult;

            if (IsInBattle() || Patches.BattleState.IsInBattle)
                return KeyContext.Battle;

            if (Patches.DialogueTracker.ValidateState() || Patches.ShopMenuTracker.IsInShopSession)
                return KeyContext.Global;

            if (IsOnValidMap())
                return KeyContext.Field;

            // Fallback: neither field nor battle (e.g., menus, fading)
            return KeyContext.Global;
        }

        #endregion

        #region Action Handlers

        private static void OpenBattleResultNavigator()
        {
            if (BattleResultDataStore.HasData)
                BattleResultNavigator.Open();
            else
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
        }

        private static void HandleF1Key()
        {
            CoroutineManager.StartUntracked(AnnounceWalkRunState());
        }

        private static void HandleF3Key()
        {
            CoroutineManager.StartUntracked(AnnounceEncounterState());
        }

        private static System.Collections.IEnumerator AnnounceWalkRunState()
        {
            yield return null;
            yield return null;
            yield return null;
            bool isDashing = MoveStateHelper.GetDashFlag();
            FFV_ScreenReaderMod.SpeakText(isDashing ? "Run" : "Walk", interrupt: true);
        }

        private static System.Collections.IEnumerator AnnounceEncounterState()
        {
            yield return null;
            try
            {
                var userData = Il2CppLast.Management.UserDataManager.Instance();
                if (userData?.CheatSettingsData != null)
                {
                    bool enabled = userData.CheatSettingsData.IsEnableEncount;
                    FFV_ScreenReaderMod.SpeakText(enabled ? "Encounters on" : "Encounters off", interrupt: true);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error reading encounter state: {ex.Message}");
            }
        }

        private static void HandleF5Key()
        {
            int current = FFV_ScreenReaderMod.EnemyHPDisplay;
            int next = (current + 1) % 3;
            FFV_ScreenReaderMod.SetEnemyHPDisplay(next);
            string[] options = { "Numbers", "Percentage", "Hidden" };
            FFV_ScreenReaderMod.SpeakText($"Enemy HP: {options[next]}", interrupt: true);
        }

        private void HandleMovementStateKey()
        {
            if (!IsOnValidMap())
            {
                FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                return;
            }

            MoveStateHelper.SyncWithActualGameState();
            if (MoveStateHelper.IsOnFoot())
            {
                bool isRunning = MoveStateHelper.GetDashFlag();
                FFV_ScreenReaderMod.SpeakText(isRunning ? "Running" : "Walking", interrupt: true);
            }
            else
            {
                int moveState = MoveStateHelper.GetCurrentMoveState();
                FFV_ScreenReaderMod.SpeakText(MoveStateHelper.GetMoveStateName(moveState), interrupt: true);
            }
        }

        private void HandleItemInfoKey()
        {
            if (Patches.ShopMenuTracker.ValidateState())
            {
                Patches.ShopDetailsAnnouncer.AnnounceCurrentItemDetails();
            }
            else if (Patches.ItemMenuTracker.ValidateState())
            {
                Patches.ItemDetailsAnnouncer.AnnounceEquipRequirements();
            }
            else if (Patches.JobMenuTracker.ValidateState())
            {
                Patches.JobDetailsAnnouncer.AnnounceCurrentJobDetails();
            }
            else if (Patches.AbilitySlotMenuTracker.ValidateState())
            {
                Patches.AbilitySlotDetailsAnnouncer.AnnounceCurrentDetails();
            }
            else if (Patches.AbilityEquipMenuTracker.ValidateState())
            {
                Patches.AbilityEquipDetailsAnnouncer.AnnounceCurrentDetails();
            }
            else if (Patches.AbilityMenuTracker.ValidateState())
            {
                Patches.AbilityDetailsAnnouncer.AnnounceCurrentAbilityDetails();
            }
            else
            {
                Patches.JobAbilityTrackerHelper.ClearAllTrackers();
                Patches.ItemMenuTracker.ClearState();
                AnnounceConfigTooltip();
            }
        }

        #endregion

        #region Game State Helpers

        private bool IsInBattle()
        {
            return Patches.ActiveBattleCharacterTracker.CurrentActiveCharacter != null;
        }

        private bool IsInputFieldFocused()
        {
            try
            {
                if (EventSystem.current == null) return false;
                var currentObj = EventSystem.current.currentSelectedGameObject;
                if (currentObj == null) return false;
                return currentObj.TryGetComponent(out UnityEngine.UI.InputField inputField);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error checking input field state: {ex.Message}");
                return false;
            }
        }

        private bool IsOnValidMap()
        {
            if (Patches.GameStatePatches.IsScreenFading) return false;
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            return playerController?.fieldPlayer != null;
        }

        private bool IsBestiaryDetailActive()
        {
            if (!MenuStateRegistry.IsActive(MenuStateRegistry.BESTIARY_DETAIL))
                return false;

            if (cachedBestiaryInfoController == null || cachedBestiaryInfoController.gameObject == null)
            {
                cachedBestiaryInfoController = GameObjectCache.Get<LibraryInfoController_KeyInput>();
            }

            return cachedBestiaryInfoController != null &&
                   cachedBestiaryInfoController.gameObject != null &&
                   cachedBestiaryInfoController.gameObject.activeInHierarchy;
        }

        private bool IsStatusScreenActive()
        {
            if (cachedStatusController == null || cachedStatusController.gameObject == null)
            {
                cachedStatusController = GameObjectCache.Get<StatusDetailsController>();
            }

            return cachedStatusController != null &&
                   cachedStatusController.gameObject != null &&
                   cachedStatusController.gameObject.activeInHierarchy;
        }

        private void AnnounceConfigTooltip()
        {
            try
            {
                var keyInputController = GameObjectCache.Get<ConfigActualDetailsControllerBase_KeyInput>();
                if (keyInputController == null)
                    keyInputController = GameObjectCache.Refresh<ConfigActualDetailsControllerBase_KeyInput>();

                if (keyInputController != null && keyInputController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => keyInputController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFV_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }

                var touchController = GameObjectCache.Get<ConfigActualDetailsControllerBase_Touch>();
                if (touchController == null)
                    touchController = GameObjectCache.Refresh<ConfigActualDetailsControllerBase_Touch>();

                if (touchController != null && touchController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => touchController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFV_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading config tooltip: {ex.Message}");
            }
        }

        private static void AnnounceKeyHelp()
        {
            try
            {
                var controllers = UnityEngine.Object.FindObjectsOfType<Il2CppLast.UI.KeyInput.KeyHelpController>();
                if (controllers == null || controllers.Length == 0)
                {
                    FFV_ScreenReaderMod.SpeakText("No controls displayed", interrupt: true);
                    return;
                }

                var parts = new List<string>();
                for (int i = 0; i < controllers.Length; i++)
                {
                    var helpController = controllers[i];
                    if (helpController == null || helpController.gameObject == null || !helpController.gameObject.activeInHierarchy)
                        continue;

                    var views = helpController.GetComponentsInChildren<Il2CppLast.UI.KeyInput.KeyIconView>(false);
                    if (views == null) continue;

                    for (int j = 0; j < views.Length; j++)
                    {
                        var view = views[j];
                        if (view == null) continue;

                        string buttonLabel = "";
                        var iconTexts = view.IconTextList;
                        if (iconTexts != null)
                        {
                            var labels = new List<string>();
                            for (int k = 0; k < iconTexts.Count; k++)
                            {
                                var text = iconTexts[k];
                                if (text != null && !string.IsNullOrEmpty(text.text))
                                    labels.Add(text.text.Trim());
                            }
                            buttonLabel = string.Join("/", labels);
                        }

                        string action = view.KeyText != null ? view.KeyText.text?.Trim() : null;
                        string action2 = view.KeyText2 != null ? view.KeyText2.text?.Trim() : null;

                        string actionText = "";
                        if (!string.IsNullOrEmpty(action) && !string.IsNullOrEmpty(action2))
                            actionText = $"{action}, {action2}";
                        else if (!string.IsNullOrEmpty(action))
                            actionText = action;
                        else if (!string.IsNullOrEmpty(action2))
                            actionText = action2;

                        if (!string.IsNullOrEmpty(buttonLabel) && !string.IsNullOrEmpty(actionText))
                            parts.Add($"{buttonLabel}: {actionText}");
                        else if (!string.IsNullOrEmpty(actionText))
                            parts.Add(actionText);
                    }
                }

                if (parts.Count > 0)
                    FFV_ScreenReaderMod.SpeakText(string.Join(", ", parts), interrupt: true);
                else
                    FFV_ScreenReaderMod.SpeakText("No controls displayed", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading key help: {ex.Message}");
                FFV_ScreenReaderMod.SpeakText("No controls displayed", interrupt: true);
            }
        }

        private string TryReadDescriptionText(Func<UnityEngine.UI.Text> getTextField)
        {
            try
            {
                var descText = getTextField();
                if (descText != null && !string.IsNullOrWhiteSpace(descText.text))
                    return descText.text.Trim();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error accessing description text: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Platform-Specific

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly uint _ownProcessId = _isWindows
            ? (uint)System.Diagnostics.Process.GetCurrentProcess().Id
            : 0;

        /// <summary>
        /// Returns true if our process owns the foreground window (Windows),
        /// or falls back to Application.isFocused on other platforms.
        /// </summary>
        private static bool IsGameProcessForeground()
        {
            if (_isWindows)
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                GetWindowThreadProcessId(fg, out uint pid);
                return pid == _ownProcessId;
            }
            return Application.isFocused;
        }

        #endregion
    }
}
