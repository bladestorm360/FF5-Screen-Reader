namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Provides human-readable button labels based on the connected controller type.
    /// SDL normalizes buttons positionally (SOUTH = bottom face button), so labels
    /// must map to the physical markings on each controller type.
    /// </summary>
    public static class ControllerLabels
    {
        // Controller family for label selection
        private enum ControllerFamily { Xbox360, XboxOne, PS3, PS4, PS5, Nintendo, Generic }

        private static ControllerFamily GetFamily()
        {
            int type = GamepadManager.GamepadType;
            switch (type)
            {
                case SDL3.SDL_GAMEPAD_TYPE_XBOX360:
                    return ControllerFamily.Xbox360;

                case SDL3.SDL_GAMEPAD_TYPE_XBOXONE:
                    return ControllerFamily.XboxOne;

                case SDL3.SDL_GAMEPAD_TYPE_PS3:
                    return ControllerFamily.PS3;
                case SDL3.SDL_GAMEPAD_TYPE_PS4:
                    return ControllerFamily.PS4;
                case SDL3.SDL_GAMEPAD_TYPE_PS5:
                    return ControllerFamily.PS5;

                case SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO:
                case SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT:
                case SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT:
                case SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR:
                    return ControllerFamily.Nintendo;

                default:
                    return ControllerFamily.XboxOne; // Most common modern controller
            }
        }

        /// <summary>
        /// Gets the display name for a gamepad button based on connected controller type.
        /// </summary>
        public static string GetButtonLabel(int button)
        {
            var family = GetFamily();

            switch (button)
            {
                case SDL3.SDL_GAMEPAD_BUTTON_SOUTH:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "Cross",
                        ControllerFamily.Nintendo => "B",
                        _ => "A"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_EAST:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "Circle",
                        ControllerFamily.Nintendo => "A",
                        _ => "B"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_WEST:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "Square",
                        ControllerFamily.Nintendo => "Y",
                        _ => "X"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_NORTH:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "Triangle",
                        ControllerFamily.Nintendo => "X",
                        _ => "Y"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_BACK:
                    return family switch
                    {
                        ControllerFamily.Xbox360 => "Back",
                        ControllerFamily.XboxOne => "View",
                        ControllerFamily.PS3 => "Select",
                        ControllerFamily.PS4 => "Share",
                        ControllerFamily.PS5 => "Create",
                        ControllerFamily.Nintendo => "Minus",
                        _ => "View"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_START:
                    return family switch
                    {
                        ControllerFamily.Xbox360 => "Start",
                        ControllerFamily.XboxOne => "Menu",
                        ControllerFamily.PS3 => "Start",
                        ControllerFamily.PS4 or ControllerFamily.PS5 => "Options",
                        ControllerFamily.Nintendo => "Plus",
                        _ => "Menu"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "L1",
                        ControllerFamily.Nintendo => "L",
                        _ => "LB"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "R1",
                        ControllerFamily.Nintendo => "R",
                        _ => "RB"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_LEFT_STICK:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "L3",
                        _ => "LS"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_RIGHT_STICK:
                    return family switch
                    {
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "R3",
                        _ => "RS"
                    };

                case SDL3.SDL_GAMEPAD_BUTTON_DPAD_UP: return "D-pad Up";
                case SDL3.SDL_GAMEPAD_BUTTON_DPAD_DOWN: return "D-pad Down";
                case SDL3.SDL_GAMEPAD_BUTTON_DPAD_LEFT: return "D-pad Left";
                case SDL3.SDL_GAMEPAD_BUTTON_DPAD_RIGHT: return "D-pad Right";

                case SDL3.SDL_GAMEPAD_BUTTON_GUIDE:
                    return family switch
                    {
                        ControllerFamily.Xbox360 => "Guide",
                        ControllerFamily.XboxOne => "Xbox",
                        ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "PS",
                        ControllerFamily.Nintendo => "Home",
                        _ => "Xbox"
                    };

                default: return $"Button {button}";
            }
        }

        /// <summary>
        /// Gets labels for trigger axes.
        /// </summary>
        public static string GetLeftTriggerLabel()
        {
            return GetFamily() switch
            {
                ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "L2",
                ControllerFamily.Nintendo => "ZL",
                _ => "LT"
            };
        }

        public static string GetRightTriggerLabel()
        {
            return GetFamily() switch
            {
                ControllerFamily.PS3 or ControllerFamily.PS4 or ControllerFamily.PS5 => "R2",
                ControllerFamily.Nintendo => "ZR",
                _ => "RT"
            };
        }

        /// <summary>
        /// Gets a human-readable name for an SDL gamepad type constant.
        /// </summary>
        public static string GetControllerTypeName(int type)
        {
            return type switch
            {
                SDL3.SDL_GAMEPAD_TYPE_XBOX360 => "Xbox 360",
                SDL3.SDL_GAMEPAD_TYPE_XBOXONE => "Xbox One",
                SDL3.SDL_GAMEPAD_TYPE_PS3 => "PlayStation 3",
                SDL3.SDL_GAMEPAD_TYPE_PS4 => "PlayStation 4",
                SDL3.SDL_GAMEPAD_TYPE_PS5 => "PlayStation 5",
                SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO => "Switch Pro",
                SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT => "Joy-Con Left",
                SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT => "Joy-Con Right",
                SDL3.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR => "Joy-Con Pair",
                SDL3.SDL_GAMEPAD_TYPE_STANDARD => "Standard",
                _ => "Unknown"
            };
        }
    }
}
