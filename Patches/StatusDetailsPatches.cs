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
    /// Announce helper for the status character-select list, shared by the navigation postfix and
    /// the initial-focus path (FieldStatusReannouncePatches) so both produce the same string.
    /// Returns TRUE when it spoke and FALSE when the row isn't readable yet, which is what lets
    /// the MenuFocusAnnouncer settle loop retry until the list is built.
    /// </summary>
    public static class StatusMenuState
    {
        // The list can be re-driven on the same row while the window settles.
        private static int _lastIndex = -1;

        /// <summary>Clears the guard so a menu (re)entry announces even on the same character.</summary>
        public static void ClearLast() => _lastIndex = -1;

        // One-shot: confirms the gate below actually fired, rather than the read merely not
        // happening. Remove once verified in the log.
        private static bool _loggedPopupSuppression = false;

        public static bool AnnounceCharacterRow(UnityEngine.Transform contentTransform, int index, int count)
        {
            if (contentTransform == null) return false;

            // A confirmation popup (or the save/load flow) owns the screen, so the party panel
            // behind it is not what the player is interacting with. Confirming Quick Save re-drives
            // StatusWindowController's cursor as the prompt opens, which otherwise announced a
            // party row over the prompt ("Bartz, Freelancer, Level 3..." before "Save your
            // progress?"). Any popup raised over the pause menu had the same exposure.
            //
            // Safe to read the flag here even though the popup's Open() may run in the same frame
            // as SelectContent: both routes into this method are deferred — the navigation postfix
            // by a one-frame yield, the initial-focus path by the MenuFocusAnnouncer settle loop —
            // and PopupState.SetActive runs synchronously inside the Open() postfix. A synchronous
            // guard would have been subject to that ordering race.
            //
            // Returns false, not true: false is this method's "not readable yet" contract, so the
            // settle loop retries and still announces if the popup closes while it is alive.
            if (PopupState.IsConfirmationPopupActive || SaveLoadMenuState.IsActive)
            {
                if (!_loggedPopupSuppression)
                {
                    _loggedPopupSuppression = true;
                    MelonLogger.Msg("[StatusMenu] Suppressed character row read — confirmation popup active");
                }
                return false;
            }

            // Use CharacterSelectionReader to get character info from text components
            string characterInfo = CharacterSelectionReader.TryReadCharacterSelection(contentTransform, index);
            if (string.IsNullOrWhiteSpace(characterInfo)) return false;

            if (index == _lastIndex) return false;
            _lastIndex = index;

            // Append list position last (character index within the party list).
            FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(characterInfo, index, count));
            return true;
        }
    }

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

                StatusMenuState.AnnounceCharacterRow(selectedContent.transform, index, contents.Count);
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

                StatusDetailsHelpers.ShowCharacter(controller, StatusDetailsHelpers.GetCharacterDataFromController(controller));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed status announcement: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// LB/RB character switch on the status details screen. SetNextPlayer/SetPrevPlayer live on the
    /// root base class Serial.Template.UI.StatusDetailsControllerBase (protected virtual, unique RVAs
    /// 0x524E70 / 0x525320, not overridden by FF5's KeyInput controller). Each moves targetIndex and
    /// raises OnChange; the view is refilled for the new character, so read it one frame later and
    /// rebuild the stat list (the Commands group differs per character).
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.Template.UI.StatusDetailsControllerBase), nameof(Il2CppSerial.Template.UI.StatusDetailsControllerBase.SetNextPlayer))]
    public static class StatusDetailsControllerBase_SetNextPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.Template.UI.StatusDetailsControllerBase __instance)
            => StatusDetailsHelpers.OnPlayerSwitch(__instance);
    }

    [HarmonyPatch(typeof(Il2CppSerial.Template.UI.StatusDetailsControllerBase), nameof(Il2CppSerial.Template.UI.StatusDetailsControllerBase.SetPrevPlayer))]
    public static class StatusDetailsControllerBase_SetPrevPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.Template.UI.StatusDetailsControllerBase __instance)
            => StatusDetailsHelpers.OnPlayerSwitch(__instance);
    }

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
        /// Arms stat navigation for a character (index to the top, stat list rebuilt — the Commands
        /// group is per character) and speaks the summary. Navigation is armed even when the
        /// summary is empty, so the arrows never go dead on a screen that failed to read.
        /// </summary>
        public static void ShowCharacter(StatusDetailsController controller, OwnedCharacterData characterData)
        {
            if (characterData != null)
            {
                var tracker = StatusNavigationTracker.Instance;
                tracker.IsNavigationActive = true;
                tracker.CurrentStatIndex = 0;  // Start at top
                tracker.ActiveController = controller;
                tracker.CurrentCharacterData = characterData;

                StatusNavigationReader.InitializeStatList();
            }
            else
            {
                MelonLogger.Warning("[Status] Could not get character data for navigation");
            }

            string statusText = StatusDetailsReader.ReadStatusDetails(controller);
            if (!string.IsNullOrWhiteSpace(statusText))
                FFV_ScreenReaderMod.SpeakText(statusText);
        }

        /// <summary>SetNextPlayer/SetPrevPlayer postfix: re-read the screen one frame later.</summary>
        public static void OnPlayerSwitch(Il2CppSerial.Template.UI.StatusDetailsControllerBase instance)
        {
            try
            {
                var controller = instance?.TryCast<StatusDetailsController>();
                if (controller == null || !StatusNavigationTracker.Instance.IsNavigationActive) return;
                CoroutineManager.StartManaged(DelayedPlayerSwitch(controller));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Status] Error in player switch postfix: {ex.Message}");
            }
        }

        private static IEnumerator DelayedPlayerSwitch(StatusDetailsController controller)
        {
            yield return null;

            try
            {
                if (controller == null || controller.gameObject == null || !controller.gameObject.activeInHierarchy)
                    yield break;

                var characterData = GetCharacterDataFromController(controller);
                if (characterData == null) yield break;

                // A redundant switch call (or a display re-init that already read this character)
                // leaves the same character up — nothing new to say.
                var current = StatusNavigationTracker.Instance.CurrentCharacterData;
                if (current != null && current.Pointer == characterData.Pointer) yield break;

                ShowCharacter(controller, characterData);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Status] Error in delayed player switch: {ex.Message}");
            }
        }

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
