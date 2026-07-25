namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Small null-safe wrappers around Unity object checks.
    /// IL2CPP components can be destroyed underneath a cached reference, so every
    /// access is wrapped — a destroyed component throws rather than returning null.
    /// </summary>
    public static class UnityHelpers
    {
        /// <summary>
        /// Validates that a Unity Component's gameObject is still active.
        /// Shared helper for the menu tracker ValidateState() pattern.
        /// Returns true if the controller is valid and active.
        /// </summary>
        public static bool IsControllerActive(UnityEngine.Component controller)
        {
            if (controller == null) return false;
            try
            {
                return controller.gameObject != null && controller.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
