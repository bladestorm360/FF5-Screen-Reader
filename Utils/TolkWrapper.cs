using System;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Wrapper for Tolk screen reader integration.
    /// Handles initialization, speaking text, and cleanup.
    /// </summary>
    public class TolkWrapper
    {
        private readonly Tolk.Tolk tolk = new Tolk.Tolk();
        private readonly object tolkLock = new object();

        public void Load()
        {
            try
            {
                tolk.Load();
                if (!tolk.IsLoaded())
                {
                    MelonLogger.Warning("No screen reader detected");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize screen reader support: {ex.Message}");
            }
        }

        public void Unload()
        {
            try
            {
                tolk.Unload();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error unloading screen reader: {ex.Message}");
            }
        }

        public void Speak(string text, bool interrupt = true)
        {
            try
            {
                if (tolk.IsLoaded() && !string.IsNullOrEmpty(text))
                {
                    // An Environment.StackTrace dump used to sit here. It was useful for finding
                    // which reader produced an announcement, but it ran synchronously before
                    // tolk.Output — capturing a managed stack through the IL2CPP trampolines and
                    // writing ~10 console lines per utterance. Measured testing showed it was NOT
                    // the source of the field-menu lag, so this is a cost cleanup, not a fix.
                    // Restore the line temporarily if you need to identify a speech's caller.
                    MelonLogger.Msg($"[Speech] \"{text}\" (interrupt={interrupt})");
                    // Thread-safe: ensure only one Tolk call at a time to prevent native crashes
                    lock (tolkLock)
                    {
                        tolk.Output(text, interrupt);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error speaking text: {ex.Message}");
            }
        }

        public bool IsLoaded() => tolk.IsLoaded();

        /// <summary>
        /// Silences current speech immediately. Used by controller navigation
        /// to interrupt ongoing announcements since NVDA doesn't see controller
        /// input as key events.
        /// </summary>
        public void Silence()
        {
            try
            {
                if (tolk.IsLoaded())
                {
                    lock (tolkLock)
                    {
                        tolk.Silence();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error silencing screen reader: {ex.Message}");
            }
        }
    }
}