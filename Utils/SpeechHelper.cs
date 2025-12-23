using System.Collections;
using FFV_ScreenReader.Core;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Shared speech helper utilities for all patches.
    /// </summary>
    internal static class SpeechHelper
    {
        /// <summary>
        /// Coroutine that speaks text after one frame delay.
        /// Use with CoroutineManager.StartManaged().
        /// </summary>
        internal static IEnumerator DelayedSpeech(string text)
        {
            yield return null; // Wait one frame
            FFV_ScreenReaderMod.SpeakText(text);
        }
    }
}
