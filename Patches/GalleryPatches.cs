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

        public static void ClearState()
        {
            IsInGallery = false;
            SuppressContentChange = false;
            CachedFocusedPtr = IntPtr.Zero;
            PreviousState = 0;
            MenuStateRegistry.Reset(MenuStateRegistry.GALLERY);
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
                            MenuStateRegistry.SetActiveExclusive(MenuStateRegistry.GALLERY);
                            CoroutineManager.StartManaged(AnnounceGalleryEntry());
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

        private static IEnumerator AnnounceGalleryEntry()
        {
            yield return null;
            FFV_ScreenReaderMod.SpeakText(T("Gallery"), true);

            float elapsed = 0f;
            while (elapsed < 2f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                try
                {
                    IntPtr focusedPtr = GalleryStateTracker.CachedFocusedPtr;
                    if (focusedPtr != IntPtr.Zero &&
                        GalleryReader.ReadContentFromPointer(focusedPtr, out int number, out string name))
                    {
                        string entry = GalleryReader.ReadListEntry(number, name);
                        if (!string.IsNullOrEmpty(entry))
                            FFV_ScreenReaderMod.SpeakText(entry, false);
                        GalleryStateTracker.SuppressContentChange = false;
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Gallery] Error announcing entry item: {ex.Message}");
                    break;
                }
            }

            GalleryStateTracker.SuppressContentChange = false;
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
                    GalleryStateTracker.CachedFocusedPtr = ptr;
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
