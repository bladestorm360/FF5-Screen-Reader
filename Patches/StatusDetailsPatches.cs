using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppSerial.FF5.UI.KeyInput;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Data.User;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Menus;
using FFV_ScreenReader.Utils;
using Il2CppSystem.Collections.Generic;
using Il2CppSerial.Template.UI.KeyInput;
using UnityEngine;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks status menu state to distinguish user navigation from initialization
    /// </summary>
    public static class StatusMenuTracker
    {
        public static bool IsUserOpened { get; set; }
        public static DateTime LastSelectTime { get; set; }
    }

    /// <summary>
    /// Tracks navigation state within the status screen for arrow key navigation
    /// </summary>
    public class StatusNavigationTracker
    {
        private static StatusNavigationTracker instance = null;
        public static StatusNavigationTracker Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new StatusNavigationTracker();
                }
                return instance;
            }
        }

        public bool IsNavigationActive { get; set; }
        public int CurrentStatIndex { get; set; }
        public OwnedCharacterData CurrentCharacterData { get; set; }
        public StatusDetailsController ActiveController { get; set; }

        private StatusNavigationTracker()
        {
            Reset();
        }

        public void Reset()
        {
            IsNavigationActive = false;
            CurrentStatIndex = 0;
            CurrentCharacterData = null;
            ActiveController = null;
        }

        public bool ValidateState()
        {
            return IsNavigationActive &&
                   CurrentCharacterData != null &&
                   ActiveController != null &&
                   ActiveController.gameObject != null &&
                   ActiveController.gameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Controller-based patches for the character status menu.
    /// Announces character names when navigating the selection list and status details when viewing.
    /// Ported from FF6 screen reader.
    /// </summary>

    /// <summary>
    /// Patch for character selection list navigation.
    /// Announces character names when navigating up/down in the status character list.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.StatusWindowController), nameof(Il2CppLast.UI.KeyInput.StatusWindowController.SelectContent))]
    public static class StatusWindowController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.StatusWindowController __instance, List<StatusWindowContentControllerBase> contents, int index, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                // Safety checks
                if (__instance == null || contents == null)
                {
                    return;
                }

                if (index < 0 || index >= contents.Count)
                {
                    return;
                }

                // IMPORTANT: Filter out initialization/background calls
                // Only announce when the status window is actually visible and active
                if (__instance.gameObject == null || !__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Also check if the cursor is active - if not, this is likely initialization
                if (targetCursor == null || targetCursor.gameObject == null || !targetCursor.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Mark that user is actively navigating the status menu
                StatusMenuTracker.IsUserOpened = true;
                StatusMenuTracker.LastSelectTime = DateTime.UtcNow;

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedCharacterAnnouncement(contents, index));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusWindowController.SelectContent patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedCharacterAnnouncement(List<StatusWindowContentControllerBase> contents, int index)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                var selectedContent = SelectContentHelper.TryGetItem(contents, index);
                if (selectedContent == null)
                {
                    yield break;
                }

                // Use CharacterSelectionReader to get character info from text components
                string characterInfo = CharacterSelectionReader.TryReadCharacterSelection(selectedContent.transform, index);

                if (!string.IsNullOrWhiteSpace(characterInfo))
                {
                    FFV_ScreenReaderMod.SpeakText(characterInfo);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed character announcement: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for StatusDetailsController.InitDisplay.
    /// </summary>
    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.InitDisplay))]
    public static class StatusDetailsController_InitDisplay_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatusDetailsController __instance)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Register the controller in GameObjectCache
                Utils.GameObjectCache.Register(__instance);

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedStatusAnnouncement(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.InitDisplay patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedStatusAnnouncement(StatusDetailsController controller)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                if (controller == null)
                {
                    yield break;
                }

                // IMPORTANT: Only announce if user actively opened the status menu
                // InitDisplay fires during game load - we want to suppress that
                if (!StatusMenuTracker.IsUserOpened)
                {
                    yield break;
                }

                // Also suppress if status screen isn't actually visible
                if (controller.gameObject == null || !controller.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                // Read all status details
                string statusText = StatusDetailsReader.ReadStatusDetails(controller);

                if (string.IsNullOrWhiteSpace(statusText))
                {
                    yield break;
                }

                FFV_ScreenReaderMod.SpeakText(statusText);

                // Initialize navigation state
                try
                {
                    var characterData = StatusDetailsHelpers.GetCharacterDataFromController(controller);
                    if (characterData != null)
                    {
                        var tracker = StatusNavigationTracker.Instance;
                        tracker.IsNavigationActive = true;
                        tracker.CurrentStatIndex = 0;  // Start at top
                        tracker.ActiveController = controller;
                        tracker.CurrentCharacterData = characterData;

                        // Initialize the stat list
                        StatusNavigationReader.InitializeStatList();
                    }
                    else
                    {
                        MelonLogger.Warning("[Status] Could not get character data for navigation");
                    }
                }
                catch (Exception navEx)
                {
                    MelonLogger.Warning($"Error initializing navigation: {navEx.Message}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed status announcement: {ex.Message}");
            }
        }
    }

    // NOTE: SetNextPlayer, SetPrevPlayer, and SetParameter methods do not exist in FF5's StatusDetailsController
    // Character navigation in FF5 uses different methods (likely RB/LB button handling)
    // Character data tracking is handled through InitDisplay patch instead

    /// <summary>
    /// Patch ExitDisplay to clear character data when leaving status screen.
    /// </summary>
    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.ExitDisplay))]
    public static class StatusDetailsController_ExitDisplay_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Reset navigation state — InitDisplay will re-initialize for new character
                StatusNavigationTracker.Instance.Reset();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.ExitDisplay patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper methods for status screen patches
    /// </summary>
    public static class StatusDetailsHelpers
    {
        /// <summary>
        /// Extract character data from the StatusDetailsController
        /// </summary>
        public static OwnedCharacterData GetCharacterDataFromController(StatusDetailsController controller)
        {
            try
            {
                var statusController = controller?.statusController;
                if (statusController != null)
                {
                    // Try direct access first
                    try
                    {
                        var targetData = statusController.targetData;
                        if (targetData != null)
                        {
                            return targetData;
                        }
                    }
                    catch
                    {
                        // Direct access failed, try Traverse
                    }

                    // Try Traverse if field is private
                    try
                    {
                        var traversed = Traverse.Create(statusController).Field("targetData").GetValue<OwnedCharacterData>();
                        if (traversed != null)
                        {
                            return traversed;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Status] Traverse access failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error accessing character data: {ex.Message}");
            }
            return null;
        }
    }

}
