using System;
using System.Runtime.InteropServices;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Resolves whether the game's main window is the foreground window. Used to gate mod
    /// hotkeys so they don't fire while the player is focused on another application.
    /// (The old focus-stealing / invisible-window mechanism was removed — modals are now
    /// virtual and game input is suppressed via ControllerRouter.SuppressGameInput +
    /// InputPassthroughPatches + Input.ResetInputAxes.)
    /// </summary>
    public static class WindowsFocusHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static IntPtr cachedGameWindow = IntPtr.Zero;

        /// <summary>
        /// True when the game's main window is the foreground window. Fails open (returns true)
        /// if the game window handle can't be resolved, so input is never permanently blocked.
        /// </summary>
        public static bool IsGameWindowFocused()
        {
            if (cachedGameWindow == IntPtr.Zero)
            {
                try { cachedGameWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
                catch { }
            }
            if (cachedGameWindow == IntPtr.Zero)
                return true;
            return GetForegroundWindow() == cachedGameWindow;
        }
    }
}
