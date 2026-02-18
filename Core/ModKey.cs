namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Mod-specific key enum backed by Win32 Virtual Key codes.
    /// Used for all mod input detection. Background mode calls GetAsyncKeyState((int)key) directly.
    /// Focus mode maps to SDL scancodes via InputManager's mapping table.
    /// </summary>
    public enum ModKey
    {
        None = 0,

        // --- Navigation ---
        Backspace   = 0x08,
        Tab         = 0x09,
        Return      = 0x0D,
        Escape      = 0x1B,
        Space       = 0x20,
        End         = 0x23,
        Home        = 0x24,
        LeftArrow   = 0x25,
        UpArrow     = 0x26,
        RightArrow  = 0x27,
        DownArrow   = 0x28,

        // --- Numbers (0x30-0x39) ---
        Alpha0 = 0x30,
        Alpha1 = 0x31,
        Alpha2 = 0x32,
        Alpha3 = 0x33,
        Alpha4 = 0x34,
        Alpha5 = 0x35,
        Alpha6 = 0x36,
        Alpha7 = 0x37,
        Alpha8 = 0x38,
        Alpha9 = 0x39,

        // --- Letters (0x41-0x5A) ---
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
        F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A,
        K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F,
        P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54,
        U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59,
        Z = 0x5A,

        // --- Function keys ---
        F1  = 0x70,
        F3  = 0x72,
        F5  = 0x74,
        F8  = 0x77,

        // --- Modifiers ---
        LeftShift    = 0xA0,
        RightShift   = 0xA1,
        LeftControl  = 0xA2,
        RightControl = 0xA3,

        // --- Punctuation (OEM keys) ---
        Semicolon    = 0xBA,  // ; :
        Equals       = 0xBB,  // = +
        Comma        = 0xBC,  // , <
        Minus        = 0xBD,  // - _
        Period       = 0xBE,  // . >
        Slash        = 0xBF,  // / ?
        Backtick     = 0xC0,  // ` ~
        LeftBracket  = 0xDB,  // [ {
        Backslash    = 0xDC,  // \ |
        RightBracket = 0xDD,  // ] }
        Quote        = 0xDE,  // ' "
    }

    /// <summary>
    /// Display names for ModKey values (for future rebinding UI).
    /// </summary>
    public static class ModKeyNames
    {
        public static string GetName(ModKey key)
        {
            return key switch
            {
                ModKey.Backspace    => "Backspace",
                ModKey.Tab          => "Tab",
                ModKey.Return       => "Enter",
                ModKey.Escape       => "Escape",
                ModKey.Space        => "Space",
                ModKey.End          => "End",
                ModKey.Home         => "Home",
                ModKey.LeftArrow    => "Left Arrow",
                ModKey.UpArrow      => "Up Arrow",
                ModKey.RightArrow   => "Right Arrow",
                ModKey.DownArrow    => "Down Arrow",
                ModKey.LeftShift    => "Left Shift",
                ModKey.RightShift   => "Right Shift",
                ModKey.LeftControl  => "Left Ctrl",
                ModKey.RightControl => "Right Ctrl",
                ModKey.F1           => "F1",
                ModKey.F3           => "F3",
                ModKey.F5           => "F5",
                ModKey.F8           => "F8",
                ModKey.Semicolon    => ";",
                ModKey.Equals       => "=",
                ModKey.Comma        => ",",
                ModKey.Minus        => "-",
                ModKey.Period       => ".",
                ModKey.Slash        => "/",
                ModKey.Backtick     => "`",
                ModKey.LeftBracket  => "[",
                ModKey.Backslash    => "\\",
                ModKey.RightBracket => "]",
                ModKey.Quote        => "'",
                _ when key >= ModKey.A && key <= ModKey.Z =>
                    ((char)('A' + (key - ModKey.A))).ToString(),
                _ when key >= ModKey.Alpha0 && key <= ModKey.Alpha9 =>
                    ((char)('0' + (key - ModKey.Alpha0))).ToString(),
                _ => key.ToString()
            };
        }
    }
}
