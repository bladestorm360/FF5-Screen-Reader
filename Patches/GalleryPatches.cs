using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Menus;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using Il2CppLast.Management;
using Il2CppLast.UI.KeyInput;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks gallery scene state.
    /// Mirrors MusicPlayerStateTracker pattern.
    /// </summary>
    public static class GalleryStateTracker
    {
        public static bool IsInGallery { get; set; } = false;
        public static bool SuppressContentChange { get; set; } = false;
        public static IntPtr CachedFocusedPtr { get; set; } = IntPtr.Zero;
        public static int PreviousState { get; set; } = 0;

        // Entry read: "Gallery" spoken, focused item not read yet.
        public static bool TitleSpoken { get; set; } = false;

        public static void ClearState()
        {
            IsInGallery = false;
            SuppressContentChange = false;
            CachedFocusedPtr = IntPtr.Zero;
            PreviousState = 0;
            TitleSpoken = false;
            MenuStateRegistry.Reset(MenuStateRegistry.GALLERY);
        }

        /// <summary>
        /// The gallery entry read: "Gallery" once, then the focused item queued behind it as soon as
        /// the item SetFocusContent cached is readable. Returns true once the item is spoken (the
        /// entry is complete and suppression ends). Driven by MenuFocusAnnouncer from ChangeState
        /// and, for a late or slow list, by SetFocusContent itself.
        /// </summary>
        internal static bool TryAnnounceEntry()
        {
            if (!SuppressContentChange) return true;

            if (!TitleSpoken)
            {
                FFV_ScreenReaderMod.SpeakText(T("Gallery"), true);
                TitleSpoken = true;
            }

            IntPtr focusedPtr = CachedFocusedPtr;
            if (focusedPtr == IntPtr.Zero ||
                !GalleryReader.ReadContentFromPointer(focusedPtr, out int number, out string name))
                return false;

            string entry = GalleryReader.ReadListEntry(number, name);
            if (!string.IsNullOrEmpty(entry))
                FFV_ScreenReaderMod.SpeakText(entry, false);
            SuppressContentChange = false;
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 1: State transitions — SubSceneManagerExtraGallery.ChangeState
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(SubSceneManagerExtraGallery), nameof(SubSceneManagerExtraGallery.ChangeState))]
    public static class SubSceneManagerExtraGallery_ChangeState_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int state)
        {
            try
            {
                switch (state)
                {
                    case 1: // View
                        if (GalleryStateTracker.PreviousState == 0) // First entry from Init
                        {
                            GalleryStateTracker.IsInGallery = true;
                            GalleryStateTracker.SuppressContentChange = true;
                            GalleryStateTracker.TitleSpoken = false;
                            MenuStateRegistry.SetActiveExclusive(MenuStateRegistry.GALLERY);
                            // Frame-bounded settle, not a timer: the first try is one frame after
                            // the state change (where "Gallery" used to be spoken), and it stops the
                            // moment the item is read. It replaced a 2 s Time.deltaTime poll.
                            MenuFocusAnnouncer.Request("Gallery", GalleryStateTracker.TryAnnounceEntry);
                        }
                        // Returning from Details (state 2) needs nothing: SetFocusContent fires
                        // again on the way back and is the sole announcer for the list entry.
                        GalleryStateTracker.PreviousState = 1;
                        break;

                    case 2: // Details — image opened
                        FFV_ScreenReaderMod.SpeakText(T("Image open"), true);
                        GalleryStateTracker.PreviousState = 2;
                        break;

                    case 3: // GotoTitle — leaving gallery
                        GalleryStateTracker.ClearState();
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Gallery] Error in ChangeState patch: {ex.Message}");
            }
        }

    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 2: List navigation — GalleryTopListController.SetFocusContent
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(GalleryTopListController), "SetFocusContent")]
    public static class GalleryTopListController_SetFocusContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GalleryTopListController __instance, bool isFocus)
        {
            try
            {
                if (!isFocus) return;
                if (!GalleryStateTracker.IsInGallery) return;

                IntPtr ptr;
                try
                {
                    if (__instance == null) return;
                    ptr = __instance.Pointer;
                }
                catch { return; }
                if (ptr == IntPtr.Zero) return;

                if (GalleryStateTracker.SuppressContentChange)
                {
                    // Entry still pending: cache the item and complete the entry read now ("Gallery"
                    // first if the settle has not said it yet). If the item is not readable yet, the
                    // settle retries; if that gives up, the next focus change completes it.
                    GalleryStateTracker.CachedFocusedPtr = ptr;
                    GalleryStateTracker.TryAnnounceEntry();
                    return;
                }

                if (!GalleryReader.ReadContentFromPointer(ptr, out int number, out string name))
                    return;

                string entry = GalleryReader.ReadListEntry(number, name);
                if (!string.IsNullOrEmpty(entry))
                {
                    FFV_ScreenReaderMod.SpeakText(entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Gallery] Error in SetFocusContent patch: {ex.Message}");
            }
        }
    }

}
