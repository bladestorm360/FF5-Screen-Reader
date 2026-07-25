using System;
using System.Collections;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Shared machinery for INITIAL-FOCUS announcements — reading the already-focused row when a
    /// menu is first opened or returned to.
    ///
    /// Navigation flows through per-menu SelectContent/SetCursor/SetFocus postfixes (and
    /// Cursor.NextIndex -> MenuTextDiscovery for generic menus), but the game's INITIAL cursor
    /// placement fires none of those, so the entry row is silent. Each menu instead hooks its
    /// state-entry *Init() method and asks for a read here.
    ///
    /// Rules 2/3: this is not a poll and not a timer. A request runs a FRAME-bounded settle loop
    /// that stops the instant the read succeeds and hard-stops after MaxSettleFrames, and it
    /// installs no per-frame Harmony hooks. Same shape the Gallery and Music Player entry
    /// announcements already use, but bounded in frames rather than seconds.
    /// </summary>
    public static class MenuFocusAnnouncer
    {
        // TIMEOUT, not a delay: the loop exits the instant the read succeeds, so a ready menu costs
        // exactly one frame. The cap only bounds how long an unready menu keeps retrying.
        //
        // Lowered from 30 (~0.5s) to 6 (~0.1s) because a late announcement is worse than none — at
        // half a second the user has already moved the cursor and the read describes a stale row.
        // Menus finish building their lists 1-3 frames after the state-entry Init returns, so 6
        // leaves margin without being perceptible.
        private const int MaxSettleFrames = 6;

        // Frames a read may take before it is worth logging. A read that repeatedly needs more than
        // this is gated on the wrong readiness signal — usually IsMenuOpen() on a screen that is not
        // a MenuManager menu — and should be diagnosed rather than absorbed by a bigger cap.
        private const int SlowReadFrames = 2;

        // Bumped on every Request; an in-flight loop aborts if a newer request superseded it. On
        // open, Show and InitNone both fire within a frame or two — the latch collapses them to a
        // single announce (the later one wins) while still re-announcing on every sub-menu
        // back-out. One menu is entered at a time, so a single global latch is correct: the newest
        // request always describes current focus. It also caps concurrent settle coroutines at ~1,
        // which matters given CoroutineManager's 20-coroutine limit with oldest-first eviction.
        private static int _gen;

        /// <summary>Cancels any in-flight request. Call from menu-close / state-exit hooks.</summary>
        public static void Cancel() => _gen++;

        /// <param name="tag">Log prefix, e.g. "Field", "ItemMenu".</param>
        /// <param name="tryAnnounce">Returns TRUE once it has spoken, FALSE while the menu's data
        /// is not ready yet (which retries on the next frame).</param>
        public static void Request(string tag, Func<bool> tryAnnounce)
        {
            if (tryAnnounce == null) return;

            int gen = ++_gen;
            try
            {
                CoroutineManager.StartManaged(SettleAndAnnounce(tag, gen, tryAnnounce));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{tag}] Error scheduling focus read: {ex.Message}");
            }
        }

        // The yield sits OUTSIDE the try (yield-in-try-with-catch is illegal in C#), so the
        // callback is wrapped individually.
        private static IEnumerator SettleAndAnnounce(string tag, int gen, Func<bool> tryAnnounce)
        {
            for (int frame = 0; frame < MaxSettleFrames; frame++)
            {
                yield return null; // let the cursor settle AND MenuManager.IsOpen flip true

                if (gen != _gen) yield break; // a newer Request superseded this one

                bool done;
                try
                {
                    done = tryAnnounce();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{tag}] Error reading focus: {ex.Message}");
                    yield break;
                }

                if (done)
                {
                    if (frame >= SlowReadFrames)
                        MelonLogger.Msg($"[{tag}] initial focus read took {frame + 1} frames");
                    yield break;
                }
            }

            MelonLogger.Msg($"[{tag}] initial focus read gave up after {MaxSettleFrames} frames");
        }

        /// <summary>
        /// True when a real MenuManager-driven menu is up. Reliably FALSE during the
        /// scene-construction flurry of a map/asset load, which is exactly when Show/SetActive/Init
        /// fire spuriously. Menus outside MenuManager (shop, title) must gate on their own state.
        /// </summary>
        public static bool IsMenuOpen()
        {
            try
            {
                var menuManager = Il2CppLast.UI.MenuManager.Instance;
                return menuManager != null && menuManager.IsOpen;
            }
            catch
            {
                return false; // MenuManager not constructed yet during early load
            }
        }

        /// <summary>
        /// True if the component is alive and on screen. IL2CPP components can be destroyed
        /// underneath a cached reference, so every access is wrapped.
        /// </summary>
        public static bool IsAlive(UnityEngine.Component component)
        {
            try
            {
                return component != null
                    && component.gameObject != null
                    && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
