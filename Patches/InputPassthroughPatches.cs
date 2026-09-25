using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Harmony postfix patches on InputSystemManager to:
    /// 1. Suppress all game input when mod is consuming (mod menu, dialogs, mod mode)
    /// 2. Inject SDL controller state for game passthrough when not suppressing
    ///
    /// Applied manually in TryManualPatching because InputSystemManager uses
    /// nested enum types that require reflection-based patching.
    /// </summary>
    public static class InputPassthroughPatches
    {
        // InputActionType values from dump.cs
        private const int ACTION_ENTER = 0;
        private const int ACTION_CANCEL = 1;
        private const int ACTION_SHORTCUT = 2;
        private const int ACTION_MENU = 3;
        private const int ACTION_UP = 4;
        private const int ACTION_DOWN = 5;
        private const int ACTION_LEFT = 6;
        private const int ACTION_RIGHT = 7;
        private const int ACTION_SWITCH_LEFT = 8;
        private const int ACTION_SWITCH_RIGHT = 9;
        private const int ACTION_PAGE_UP = 10;
        private const int ACTION_PAGE_DOWN = 11;
        private const int ACTION_START = 12;
        private const int ACTION_STICK_L = 13;
        private const int ACTION_STICK_R = 14;

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                var ismType = typeof(Il2CppSystem.Input.InputSystemManager);

                PatchMethod(harmony, ismType, "GetKeyDown", nameof(GetKeyDown_Postfix));
                PatchMethod(harmony, ismType, "GetKey", nameof(GetKey_Postfix));
                PatchMethod(harmony, ismType, "GetKeyUp", nameof(GetKeyUp_Postfix));
                PatchMethod(harmony, ismType, "GetAnyKey", nameof(GetAnyKey_Postfix));

                MelonLogger.Msg("[InputPassthrough] Patched InputSystemManager (GetKeyDown/GetKey/GetKeyUp/GetAnyKey)");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InputPassthrough] Failed to apply patches: {ex.Message}");
            }
        }

        private static void PatchMethod(HarmonyLib.Harmony harmony, Type type, string methodName, string postfixName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                MelonLogger.Warning($"[InputPassthrough] Could not find {methodName} on InputSystemManager");
                return;
            }

            var postfix = typeof(InputPassthroughPatches).GetMethod(postfixName, BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        }

        // =====================================================================
        // GetKeyDown postfix — edge-detected (pressed this frame)
        // =====================================================================

        private static void GetKeyDown_Postfix(int __0, ref bool __result)
        {
            // Full suppression when mod is consuming all input. This MUST run before the
            // gamepad-availability check so keyboard-only users (no controller) also get game
            // input blocked while a mod dialog/menu is open — the mod no longer steals OS focus.
            if (ControllerRouter.SuppressGameInput)
            {
                __result = false;
                return;
            }

            if (!GamepadManager.IsAvailable) return;

            // If keyboard already returned true, keep it (game keyboard works normally)
            if (__result) return;

            // Inject SDL controller state for game passthrough
            __result = GetSDLKeyDown(__0);
        }

        // =====================================================================
        // GetKey postfix — held state
        // =====================================================================

        private static void GetKey_Postfix(int __0, ref bool __result)
        {
            if (ControllerRouter.SuppressGameInput)
            {
                __result = false;
                return;
            }

            if (!GamepadManager.IsAvailable) return;

            if (__result) return;

            __result = GetSDLKeyHeld(__0);
        }

        // =====================================================================
        // GetKeyUp postfix — released this frame
        // =====================================================================

        private static void GetKeyUp_Postfix(int __0, ref bool __result)
        {
            if (ControllerRouter.SuppressGameInput)
            {
                __result = false;
                return;
            }

            if (!GamepadManager.IsAvailable) return;

            if (__result) return;

            __result = GetSDLKeyReleased(__0);
        }

        // =====================================================================
        // GetAnyKey postfix
        // =====================================================================

        private static void GetAnyKey_Postfix(ref bool __result)
        {
            if (ControllerRouter.SuppressGameInput)
            {
                __result = false;
                return;
            }

            if (!GamepadManager.IsAvailable) return;

            if (__result) return;

            // Check if any non-consumed button is pressed
            for (int i = 0; i < SDL3.SDL_GAMEPAD_BUTTON_COUNT; i++)
            {
                if (GamepadManager.IsButtonHeld(i) && !ControllerRouter.IsButtonConsumed(i))
                {
                    __result = true;
                    return;
                }
            }
        }

        // =====================================================================
        // SDL → InputActionType mapping (pressed/held/released)
        // =====================================================================

        // A field stick click reaches the game as a synthetic press once it resolves as a lone
        // click (ControllerRouter.UpdateStickClicks); the raw button is consumed until then.
        private static bool GetSDLKeyDown(int actionType)
        {
            return CheckSDL(actionType, GamepadManager.IsButtonPressed, true)
                || ControllerRouter.IsStickPulseDown(StickButtonFor(actionType));
        }

        private static bool GetSDLKeyHeld(int actionType)
        {
            return CheckSDL(actionType, GamepadManager.IsButtonHeld, false)
                || ControllerRouter.IsStickPulseDown(StickButtonFor(actionType));
        }

        private static bool GetSDLKeyReleased(int actionType)
        {
            return CheckSDL(actionType, GamepadManager.IsButtonReleased, true)
                || ControllerRouter.IsStickPulseUp(StickButtonFor(actionType));
        }

        /// <summary>SDL button behind a stick-click action, or -1 for any other action.</summary>
        private static int StickButtonFor(int actionType) =>
            actionType == ACTION_STICK_L ? SDL3.SDL_GAMEPAD_BUTTON_LEFT_STICK
            : actionType == ACTION_STICK_R ? SDL3.SDL_GAMEPAD_BUTTON_RIGHT_STICK
            : -1;

        /// <summary>
        /// Maps an InputActionType to SDL button(s) and checks the state.
        /// For directional actions: context-dependent (field vs menus).
        /// </summary>
        private static bool CheckSDL(int actionType, Func<int, bool> check, bool useDirectionEdge)
        {
            switch (actionType)
            {
                case ACTION_ENTER:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_SOUTH)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_SOUTH);

                case ACTION_CANCEL:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_EAST)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_EAST);

                case ACTION_SHORTCUT:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_WEST)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_WEST);

                case ACTION_MENU:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_NORTH)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_NORTH);

                case ACTION_UP:
                case ACTION_DOWN:
                case ACTION_LEFT:
                case ACTION_RIGHT:
                    return CheckDirectional(actionType, check, useDirectionEdge);

                case ACTION_SWITCH_LEFT:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER);

                case ACTION_SWITCH_RIGHT:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER);

                case ACTION_PAGE_UP:
                    // Left trigger: consumed on active field (pathfind), pass through otherwise
                    if (ControllerRouter.IsFieldActive)
                        return false;
                    return GamepadManager.LeftTrigger > 0.5f;

                case ACTION_PAGE_DOWN:
                    return GamepadManager.RightTrigger > 0.5f;

                case ACTION_START:
                    // Never pass through — mod menu owns Start
                    return false;

                case ACTION_STICK_L:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_LEFT_STICK)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_LEFT_STICK);

                case ACTION_STICK_R:
                    return !ControllerRouter.IsButtonConsumed(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_STICK)
                        && check(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_STICK);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Directional passthrough is context-dependent:
        /// - Field: left stick ONLY (d-pad/rstick go to mod waypoints/entities)
        /// - Non-field: d-pad + left stick (both navigate menus)
        /// </summary>
        private static bool CheckDirectional(int actionType, Func<int, bool> check, bool useEdge)
        {
            // "Field active" means on the field map with no game menu overlay.
            // When a menu is open (InputEnable=false), treat as non-field so d-pad passes through.
            bool isField = ControllerRouter.IsFieldActive;

            int dpadBtn;
            bool lstickDir;

            switch (actionType)
            {
                case ACTION_UP:
                    dpadBtn = SDL3.SDL_GAMEPAD_BUTTON_DPAD_UP;
                    lstickDir = useEdge ? GamepadManager.LeftStickUpPressed : GamepadManager.LeftStickUpHeld;
                    break;
                case ACTION_DOWN:
                    dpadBtn = SDL3.SDL_GAMEPAD_BUTTON_DPAD_DOWN;
                    lstickDir = useEdge ? GamepadManager.LeftStickDownPressed : GamepadManager.LeftStickDownHeld;
                    break;
                case ACTION_LEFT:
                    dpadBtn = SDL3.SDL_GAMEPAD_BUTTON_DPAD_LEFT;
                    lstickDir = useEdge ? GamepadManager.LeftStickLeftPressed : GamepadManager.LeftStickLeftHeld;
                    break;
                case ACTION_RIGHT:
                    dpadBtn = SDL3.SDL_GAMEPAD_BUTTON_DPAD_RIGHT;
                    lstickDir = useEdge ? GamepadManager.LeftStickRightPressed : GamepadManager.LeftStickRightHeld;
                    break;
                default:
                    return false;
            }

            // Field: only left stick passes through (d-pad goes to mod waypoints)
            // Non-field: both d-pad and left stick pass through for menu navigation
            if (isField)
                return lstickDir;

            return check(dpadBtn) || lstickDir;
        }

        // Field-active check now centralized in ControllerRouter.IsFieldActive
    }
}
