using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Data.User;
using Il2CppLast.Data.Master;
using Il2CppLast.Management;
using Il2CppLast.Systems;
using Il2CppSerial.FF5.UI.Touch;
using Il2CppSerial.FF5.UI.KeyInput;
using Il2CppSerial.Template.UI;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using GameCursor = Il2CppLast.UI.Cursor;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Track job menu state for I key handling.
    /// Delegates IsJobMenuActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class JobMenuTracker
    {
        public static bool IsJobMenuActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.JOB_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.JOB_MENU, value);
        }
        public static Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController ActiveController { get; set; }
        public static int CurrentJobIndex { get; set; }

        /// <summary>
        /// Validates that job menu is actually active and visible.
        /// Clears stale state if controller is no longer active.
        /// </summary>
        public static bool ValidateState()
        {
            if (IsJobMenuActive && !UnityHelpers.IsControllerActive(ActiveController))
            {
                IsJobMenuActive = false;
                ActiveController = null;
                CurrentJobIndex = -1;
                return false;
            }
            return IsJobMenuActive;
        }
    }

    /// <summary>
    /// Track ability/magic menu state for I key handling.
    /// Delegates IsAbilityMenuActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class AbilityMenuTracker
    {
        public static bool IsAbilityMenuActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.ABILITY_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.ABILITY_MENU, value);
        }
        public static Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController ActiveController { get; set; }
        public static OwnedAbility CurrentAbility { get; set; }
        public static string CurrentAbilityDescription { get; set; }

        /// <summary>
        /// Validates that ability/magic menu is actually active and visible.
        /// Clears stale state if controller is no longer active.
        /// </summary>
        public static bool ValidateState()
        {
            if (IsAbilityMenuActive && !UnityHelpers.IsControllerActive(ActiveController))
            {
                ClearState();
                return false;
            }
            return IsAbilityMenuActive;
        }

        public static void ClearState()
        {
            IsAbilityMenuActive = false;
            ActiveController = null;
            CurrentAbility = null;
            CurrentAbilityDescription = null;
        }
    }

    /// <summary>
    /// Track ability slot menu state for I key handling (AbilityChangeController.SelectCommand).
    /// Delegates IsActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class AbilitySlotMenuTracker
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.ABILITY_SLOT_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.ABILITY_SLOT_MENU, value);
        }
        public static Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController ActiveController { get; set; }
        public static string CurrentDescription { get; set; }

        public static bool ValidateState()
        {
            if (IsActive && !UnityHelpers.IsControllerActive(ActiveController))
            {
                ClearState();
                return false;
            }
            return IsActive;
        }

        public static void ClearState()
        {
            IsActive = false;
            ActiveController = null;
            CurrentDescription = null;
        }
    }

    /// <summary>
    /// Helper to clear all job/ability tracker state at once.
    /// Used when entering unrelated menus (item, config, etc.) to prevent stale state.
    /// </summary>
    public static class JobAbilityTrackerHelper
    {
        public static void ClearAllTrackers()
        {
            JobMenuTracker.IsJobMenuActive = false;
            JobMenuTracker.ActiveController = null;
            JobMenuTracker.CurrentJobIndex = -1;
            AbilityMenuTracker.ClearState();
            AbilitySlotMenuTracker.ClearState();
            AbilityEquipMenuTracker.ClearState();
        }
    }

    /// <summary>
    /// Track ability equip menu state for I key handling (AbilityChangeController).
    /// Delegates IsActive to MenuStateRegistry for centralized state management.
    /// </summary>
    public static class AbilityEquipMenuTracker
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.ABILITY_EQUIP_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.ABILITY_EQUIP_MENU, value);
        }
        public static Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController ActiveController { get; set; }
        public static AbilityEquipData CurrentAbilityData { get; set; }
        public static string CurrentDescription { get; set; }

        public static bool ValidateState()
        {
            if (IsActive && !UnityHelpers.IsControllerActive(ActiveController))
            {
                ClearState();
                return false;
            }
            return IsActive;
        }

        public static void ClearState()
        {
            IsActive = false;
            ActiveController = null;
            CurrentAbilityData = null;
            CurrentDescription = null;
        }
    }

    /// <summary>
    /// Patch for job selection - announces job name and level when browsing job list.
    /// Uses GetTargetCharacterData() method and job master data.
    /// Uses KeyInput namespace for keyboard/gamepad support.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController), "SelectContent")]
    public static class JobChangeWindowController_SelectContent_Patch
    {
        // Scroll-view re-fire on the same row. Index-keyed rather than text-keyed so two jobs
        // that happen to read alike still both announce. Also debounces Auto Detail, whose
        // queued (interrupt:false) description would otherwise play twice back to back.
        private static int _lastIndex = -1;

        /// <summary>Clears the guard so a menu (re)entry announces even on the same row.</summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            Announce(__instance, index);
        }

        /// <summary>
        /// Announces one job row. Shared by navigation and the initial-focus path; returns true
        /// when it spoke and false when the data isn't usable yet (so the settle loop retries).
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                // Track that job menu is active and clear other menu trackers
                ItemMenuTracker.ClearState();
                JobMenuTracker.IsJobMenuActive = true;
                JobMenuTracker.ActiveController = __instance;
                JobMenuTracker.CurrentJobIndex = index;

                // Get character and job data using the public method
                var targetCharacter = __instance.GetTargetCharacterData();
                if (targetCharacter == null) return false;

                // Get released (unlocked) jobs
                var releaseJobs = __instance.GetReleaseJobs();
                var job = SelectContentHelper.TryGetItem(releaseJobs, index);
                if (job == null) return false;

                // Get job name from message manager
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return false;

                string jobName = messageManager.GetMessage(job.MesIdName);
                if (string.IsNullOrWhiteSpace(jobName)) return false;

                // Read level, mastered status, and ABP from UI text fields
                // (data-based OwnedJob.Level returns wrong values for level 0 jobs)
                var view = __instance.view;
                if (view == null) return false;

                string levelText = view.InfoSkillLevelValueText?.text?.Trim() ?? "";
                bool isMastered = view.InfoJobLevelMasterText?.gameObject?.activeInHierarchy == true;

                // Build announcement: "{name} Lv. {N}: ABP: {X}/{Y}" or "{name} Lv. {N}: Mastered!"
                string announcement = jobName;
                if (!string.IsNullOrWhiteSpace(levelText))
                {
                    announcement += $" Lv. {levelText}:";

                    if (isMastered)
                    {
                        announcement += " Mastered!";
                    }
                    else
                    {
                        // Read ABP from private UI fields (unhollowed as public by Il2CppInterop)
                        string currentAbp = view.infoJobLevelDetailsValue?.text?.Trim();
                        string maxAbp = view.infoJobLevelDetailsMaxValue?.text?.Trim();
                        if (!string.IsNullOrWhiteSpace(currentAbp) && !string.IsNullOrWhiteSpace(maxAbp))
                        {
                            announcement += $" ABP: {currentAbp}/{maxAbp}";
                        }
                    }
                }

                // Append list position last.
                announcement = MenuPosition.Format(announcement, index, releaseJobs != null ? releaseJobs.Count : 0);

                if (index == _lastIndex) return false;
                _lastIndex = index;

                FFV_ScreenReaderMod.SpeakText(announcement);

                // Auto Detail: queue the job description after the name (same reader as the details key).
                // Reached only when the row actually changed (guard above), giving a same-index debounce.
                if (PreferencesManager.AutoDetailEnabled)
                    JobDetailsAnnouncer.AnnounceCurrentJobDetails(interrupt: false, announceIfEmpty: false);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in JobChangeWindowController.SelectContent patch: {ex.Message}");
            }
            return false;
        }
    }

    // NOTE: Main menu magic/spell list patch is disabled pending investigation.
    // The AbilityContentListController.SelectContent method is not being called during navigation.
    // Battle magic works via BattleQuantityAbilityInfomationController which is patched separately.
    // Need to find what controller/method handles main menu spell list navigation.

    /// <summary>
    /// Patch for ability command slots - announces equipped abilities or "empty" when selecting slots.
    /// Reads from the controller's contentList views (avoids Il2Cpp Traverse issues).
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController.SelectContent))]
    public static class AbilityCommandController_SelectContent_Patch
    {
        // Scroll-view re-fire on the same slot. Index-keyed so consecutive empty slots each
        // announce — otherwise moving the cursor across them would be silent.
        private static int _lastIndex = -1;

        /// <summary>Clears the guard so a menu (re)entry announces even on the same slot.</summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController __instance, int index)
        {
            Announce(__instance, index);
        }

        /// <summary>
        /// Announces one command slot. Shared by navigation and the initial-focus path; returns
        /// true when it spoke and false when the data isn't usable yet.
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                var contentView = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (contentView == null) return false;

                int slotCount = __instance.contentList != null ? __instance.contentList.Count : 0;

                // Get the command from the content view
                var command = contentView.Command;
                if (command == null)
                {
                    if (index == _lastIndex) return false;
                    _lastIndex = index;

                    FFV_ScreenReaderMod.SpeakText(MenuPosition.Format("Empty slot", index, slotCount));
                    return true;
                }

                // Get command name
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return false;

                string commandName = messageManager.GetMessage(command.MesIdName);
                if (string.IsNullOrWhiteSpace(commandName)) return false;

                if (index == _lastIndex) return false;
                _lastIndex = index;

                FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(commandName, index, slotCount));
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityCommandController.SelectContent patch: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Patch for ability equipping/changing - announces abilities when browsing equip menu.
    /// Patches SelectContent for ability list navigation in the equip menu.
    /// Uses TargetData.IsFocus to find the focused ability data.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController), "SelectContent")]
    public static class AbilityChangeController_SelectContent_Patch
    {
        // Scroll-view re-fire on the same row (SelectContent carries a WithinRangeType).
        // Index-keyed so moving across consecutive empty slots announces each one, and so the
        // Auto Detail description below is queued at most once per row.
        private static int _lastIndex = -1;

        // AbilityChangeController has NO cursor field (dump.cs 286594) — the focused index only
        // ever arrives as a SelectContent parameter, so remember it for the initial-focus path.
        private static int _cachedIndex;

        /// <summary>Index last focused, for the initial-focus read. 0 is the game's own default.</summary>
        public static int CachedIndex => _cachedIndex;

        /// <summary>
        /// Clears the re-fire guard so a (re)entry announces even on the same row. Deliberately
        /// does NOT reset _cachedIndex: with no cursor to read, the last focused row is the best
        /// estimate of where the game restores the cursor on re-entry.
        /// </summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            _cachedIndex = index;
            Announce(__instance, index);
        }

        /// <summary>
        /// Announces one ability-equip row. Shared by navigation and the initial-focus path;
        /// returns true when it spoke and false when the data isn't usable yet.
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                var view = __instance.view;
                if (view == null) return false;

                var scrollView = view.ScrollView;
                if (scrollView == null) return false;

                // Search all visible content controllers for the one with focused data
                AbilityEquipData abilityEquipData = null;
                var contentControllers = scrollView.GetComponentsInChildren<Il2CppSerial.FF5.UI.KeyInput.AbilityChangeContentController>();

                foreach (var controller in contentControllers)
                {
                    if (controller != null && controller.TargetData != null)
                    {
                        var data = controller.TargetData;
                        // Check if this data has focus set (the game sets IsFocus on the data itself)
                        if (data.IsFocus)
                        {
                            abilityEquipData = data;
                            break;
                        }
                    }
                }

                // Fallback: if no focused data found, try matching by index
                if (abilityEquipData == null)
                {
                    foreach (var controller in contentControllers)
                    {
                        if (controller != null && controller.TargetData != null)
                        {
                            var data = controller.TargetData;
                            if (data.Index == index)
                            {
                                abilityEquipData = data;
                                break;
                            }
                        }
                    }
                }

                if (abilityEquipData == null)
                {
                    if (index == _lastIndex) return false;
                    _lastIndex = index;

                    FFV_ScreenReaderMod.SpeakText("Empty");
                    return true;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return false;

                // Clear other menu trackers for mutual exclusion
                AbilitySlotMenuTracker.ClearState();
                AbilityMenuTracker.ClearState();
                ItemMenuTracker.ClearState();

                // Track for I key
                AbilityEquipMenuTracker.IsActive = true;
                AbilityEquipMenuTracker.ActiveController = __instance;
                AbilityEquipMenuTracker.CurrentAbilityData = abilityEquipData;

                // Store description for I key
                try
                {
                    AbilityEquipMenuTracker.CurrentDescription = messageManager.GetMessage(abilityEquipData.DescriptionMessageId);
                }
                catch
                {
                    AbilityEquipMenuTracker.CurrentDescription = null;
                }

                // Check for null NameMessageId first (empty/unlocked slots)
                if (string.IsNullOrEmpty(abilityEquipData.NameMessageId))
                {
                    if (index == _lastIndex) return false;
                    _lastIndex = index;

                    FFV_ScreenReaderMod.SpeakText("Empty");
                    return true;
                }

                // Get ability name
                string abilityName = messageManager.GetMessage(abilityEquipData.NameMessageId);
                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    if (index == _lastIndex) return false;
                    _lastIndex = index;

                    FFV_ScreenReaderMod.SpeakText("Empty");
                    return true;
                }

                // Build announcement
                string announcement = abilityName;

                // Add equipped status
                if (abilityEquipData.IsEquiped)
                {
                    announcement += ", equipped";
                }

                // Add MP cost if available
                try
                {
                    if (abilityEquipData.Ability != null)
                    {
                        int mpCost = abilityEquipData.Ability.UseValue;
                        if (mpCost > 0)
                        {
                            announcement += $", MP {mpCost}";
                        }
                    }
                }
                catch
                {
                    // MP cost not available, continue without it
                }

                if (index == _lastIndex) return false;
                _lastIndex = index;

                FFV_ScreenReaderMod.SpeakText(announcement);

                // Auto Detail: queue the ability description after the name (same reader as the details key).
                // Reached only when the row actually changed (guard above), giving a same-index debounce.
                if (PreferencesManager.AutoDetailEnabled)
                    AbilityEquipDetailsAnnouncer.AnnounceCurrentDetails(interrupt: false, announceIfEmpty: false);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectContent patch: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Patch for command slot selection in ability equipping menu.
    /// Announces which command slot is being modified and what's currently equipped.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController.SelectCommand))]
    public static class AbilityChangeController_SelectCommand_Patch
    {
        // SelectCommand re-fires on the same slot when the panel is rebuilt.
        private static int _lastIndex = -1;

        // Like SelectContent above, AbilityChangeController exposes no cursor — cache the index.
        private static int _cachedIndex;

        /// <summary>Index last focused, for the initial-focus read.</summary>
        public static int CachedIndex => _cachedIndex;

        /// <summary>
        /// Clears the re-fire guard so a (re)entry announces even on the same slot. Deliberately
        /// does NOT reset _cachedIndex — see AbilityChangeController_SelectContent_Patch.
        /// </summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index)
        {
            _cachedIndex = index;
            Announce(__instance, index);
        }

        /// <summary>
        /// Announces one command slot. Shared by navigation and the initial-focus path; returns
        /// true when it spoke and false when the data isn't usable yet.
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                var view = __instance.view;
                if (view == null) return false;

                // Clear other menu trackers for mutual exclusion
                AbilityEquipMenuTracker.ClearState();
                AbilityMenuTracker.ClearState();
                ItemMenuTracker.ClearState();

                // Get the equipped command from the slot's view controller
                string slotContent = "empty";
                string slotDescription = null;

                try
                {
                    // Access the equipped slot controllers from the view's CurrentList
                    var currentList = view.CurrentList;
                    if (currentList != null && index >= 0 && index < currentList.Count)
                    {
                        var equippedController = currentList[index];
                        if (equippedController != null && equippedController.view != null)
                        {
                            var content = equippedController.view.Content;
                            if (content != null && content.TargetData != null)
                            {
                                var abilityEquipData = content.TargetData;
                                var messageManager = MessageManager.Instance;
                                if (messageManager != null)
                                {
                                    string commandName = messageManager.GetMessage(abilityEquipData.NameMessageId);
                                    if (!string.IsNullOrWhiteSpace(commandName))
                                    {
                                        slotContent = commandName;
                                    }
                                    // Store description for I key
                                    try
                                    {
                                        slotDescription = messageManager.GetMessage(abilityEquipData.DescriptionMessageId);
                                    }
                                    catch
                                    {
                                        slotDescription = null;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading command slot {index + 1}: {ex.Message}");
                }

                // Track for I key
                AbilitySlotMenuTracker.IsActive = true;
                AbilitySlotMenuTracker.ActiveController = __instance;
                AbilitySlotMenuTracker.CurrentDescription = slotDescription;

                // Format: "Slot 1: White Magic" or "Slot 1: empty"
                string announcement = $"Slot {index + 1}: {slotContent}";

                if (index == _lastIndex) return false;
                _lastIndex = index;

                FFV_ScreenReaderMod.SpeakText(announcement);

                // Auto Detail: queue the command-slot description after the name (same reader as the details key).
                // Reached only when the row actually changed (guard above), giving a same-index debounce.
                if (PreferencesManager.AutoDetailEnabled)
                    AbilitySlotDetailsAnnouncer.AnnounceCurrentDetails(interrupt: false, announceIfEmpty: false);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectCommand patch: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Patch for ability target selection - announces character vitals when selecting targets.
    /// Used when using abilities from the menu (Cure, Raise, etc.).
    /// Note: SelectContent is PRIVATE, so we must use string literal instead of nameof()
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController), "SelectContent",
        new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController>), typeof(GameCursor) })]
    public static class AbilityUseContentListController_SelectContent_Patch
    {
        // Cursor-settle re-fire on the same target character.
        private static int _lastIndex = -1;

        /// <summary>Clears the guard so a menu (re)entry announces even on the same target.</summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController> targetContents,
            GameCursor targetCursor)
        {
            if (targetCursor == null) return;
            Announce(__instance, targetCursor.Index);
        }

        /// <summary>
        /// Announces one ability target. Shared by navigation and the initial-focus path; returns
        /// true when it spoke and false when the data isn't usable yet.
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                var selectedController = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (selectedController == null || selectedController.CurrentData == null) return false;

                var data = selectedController.CurrentData;
                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName)) return false;

                // Build announcement with HP, MP, and status conditions
                string announcement = characterName + CharacterStatusHelper.GetFullStatus(data.Parameter);

                // Append target position last.
                announcement = MenuPosition.Format(announcement, index, __instance.contentList != null ? __instance.contentList.Count : 0);

                if (index == _lastIndex) return false;
                _lastIndex = index;

                FFV_ScreenReaderMod.SpeakText(announcement);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityUseContentListController.SelectContent patch: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Patch for spell list navigation - announces spell names when browsing magic lists.
    /// Patches SetCursor which is called when cursor moves in the ability list.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "SetCursor",
        new Type[] { typeof(GameCursor), typeof(bool), typeof(CustomScrollView.WithinRangeType), typeof(bool) })]
    public static class AbilityContentListController_SetCursor_Patch
    {
        // SetCursor carries a WithinRangeType — re-fires on scroll-range recalculation.
        // Index-keyed so consecutive empty spell rows each announce.
        private static int _lastIndex = -1;

        /// <summary>Clears the guard so a menu (re)entry announces even on the same spell.</summary>
        public static void ClearLast() => _lastIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController __instance,
            GameCursor targetCursor, bool isScroll, CustomScrollView.WithinRangeType type, bool pageSkip)
        {
            if (targetCursor == null) return;
            Announce(__instance, targetCursor.Index);
        }

        /// <summary>
        /// Announces one spell row. Shared by navigation and the initial-focus path; returns true
        /// when it spoke and false when the data is not usable yet.
        /// </summary>
        public static bool Announce(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController __instance, int index)
        {
            try
            {
                if (__instance == null) return false;

                int spellCount = 0;
                OwnedAbility ability = null;
                unsafe
                {
                    IntPtr contentListPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x50);
                    if (contentListPtr != IntPtr.Zero)
                    {
                        var contentList = new Il2CppSystem.Collections.Generic.List<BattleAbilityInfomationContentController>(contentListPtr);
                        spellCount = contentList.Count;
                        if (index >= 0 && index < contentList.Count)
                        {
                            var controller = contentList[index];
                            if (controller != null)
                                ability = controller.Data;
                        }
                    }
                }

                // Handle empty slots
                if (ability == null)
                {
                    if (index == _lastIndex) return false;
                    _lastIndex = index;

                    FFV_ScreenReaderMod.SpeakText(MenuPosition.Format("Empty", index, spellCount));
                    return true;
                }

                // Get ability name from message manager
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return false;

                string abilityName = messageManager.GetMessage(ability.MesIdName);
                if (string.IsNullOrWhiteSpace(abilityName)) return false;

                // Strip icon tags like <IC_WMGC>, <IC_BMGC>, etc.
                abilityName = System.Text.RegularExpressions.Regex.Replace(abilityName, @"<[^>]+>", "").Trim();

                // Build announcement
                string announcement = abilityName;

                // Add MP cost if available
                try
                {
                    if (ability.Ability != null)
                    {
                        int mpCost = ability.Ability.UseValue;
                        if (mpCost > 0)
                        {
                            announcement += $", MP {mpCost}";
                        }
                    }
                }
                catch
                {
                    // MP cost not available
                }

                // Check if ability can be used via game's utility method
                try
                {
                    var targetData = __instance.TargetData;
                    if (targetData != null && !AbilityUtility.CanUseMenuAbility(targetData, ability))
                    {
                        announcement += ", Not learned";
                    }
                }
                catch
                {
                    // CanUseMenuAbility check failed, continue without it
                }

                // Append list position last (after MP / learned status).
                announcement = MenuPosition.Format(announcement, index, spellCount);

                if (index == _lastIndex) return false;
                _lastIndex = index;

                // Clear other menu trackers for mutual exclusion
                AbilitySlotMenuTracker.ClearState();
                AbilityEquipMenuTracker.ClearState();
                ItemMenuTracker.ClearState();

                // Track for I key description
                AbilityMenuTracker.IsAbilityMenuActive = true;
                AbilityMenuTracker.ActiveController = __instance;
                AbilityMenuTracker.CurrentAbility = ability;

                // Store description for I key
                try
                {
                    AbilityMenuTracker.CurrentAbilityDescription = messageManager.GetMessage(ability.MesIdDescription);
                }
                catch
                {
                    AbilityMenuTracker.CurrentAbilityDescription = null;
                }

                FFV_ScreenReaderMod.SpeakText(announcement);

                // Auto Detail: queue the spell description after the name (same reader as the details key).
                // Reached only when the row actually changed (guard above), giving a same-index debounce.
                if (PreferencesManager.AutoDetailEnabled)
                    AbilityDetailsAnnouncer.AnnounceCurrentAbilityDetails(interrupt: false, announceIfEmpty: false);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityContentListController.SetCursor patch: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Helper class to announce full job details (description) when I key is pressed
    /// </summary>
    public static class JobDetailsAnnouncer
    {
        public static void AnnounceCurrentJobDetails(bool interrupt = true, bool announceIfEmpty = true)
        {
            try
            {
                // Verify job menu is actually active using ValidateState
                if (!JobMenuTracker.ValidateState())
                {
                    return; // Silently fail if not active
                }

                // Double-check with activeInHierarchy
                if (JobMenuTracker.ActiveController == null ||
                    JobMenuTracker.ActiveController.gameObject == null ||
                    !JobMenuTracker.ActiveController.gameObject.activeInHierarchy)
                {
                    // Menu is not visible, clear the flag
                    JobMenuTracker.IsJobMenuActive = false;
                    JobMenuTracker.ActiveController = null;
                    return;
                }

                var controller = JobMenuTracker.ActiveController;

                // Access the view directly (SerializeField generates public accessor in Il2Cpp)
                var view = controller.view;
                if (view == null)
                {
                    MelonLogger.Warning("[Job Details] Could not access view");
                    return;
                }

                // Read job description directly from view's info text field
                string announcement = "";
                try
                {
                    var descriptionText = view.InfoJobDescriptText;
                    if (descriptionText != null && !string.IsNullOrWhiteSpace(descriptionText.text))
                    {
                        announcement = descriptionText.text.Trim();
                    }
                }
                catch
                {
                    // Description not available from view
                }

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    if (!announceIfEmpty) return;
                    announcement = T("No description available");
                }

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing job details: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to announce ability/spell description when I key is pressed
    /// </summary>
    public static class AbilityDetailsAnnouncer
    {
        public static void AnnounceCurrentAbilityDetails(bool interrupt = true, bool announceIfEmpty = true)
        {
            try
            {
                // Verify ability/magic menu is actually active
                if (!AbilityMenuTracker.ValidateState())
                {
                    return; // Silently fail if not active
                }

                // Get stored description
                string description = AbilityMenuTracker.CurrentAbilityDescription;

                if (string.IsNullOrWhiteSpace(description))
                {
                    if (!announceIfEmpty) return;
                    FFV_ScreenReaderMod.SpeakText(T("No description available"), interrupt);
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing ability details: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to announce ability slot (command) description when I key is pressed
    /// </summary>
    public static class AbilitySlotDetailsAnnouncer
    {
        public static void AnnounceCurrentDetails(bool interrupt = true, bool announceIfEmpty = true)
        {
            try
            {
                // Verify ability slot menu is actually active
                if (!AbilitySlotMenuTracker.ValidateState())
                {
                    return; // Silently fail if not active
                }

                // Get stored description
                string description = AbilitySlotMenuTracker.CurrentDescription;

                if (string.IsNullOrWhiteSpace(description))
                {
                    if (!announceIfEmpty) return;
                    FFV_ScreenReaderMod.SpeakText(T("No description available"), interrupt);
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing ability slot details: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to announce ability equip description when I key is pressed
    /// </summary>
    public static class AbilityEquipDetailsAnnouncer
    {
        public static void AnnounceCurrentDetails(bool interrupt = true, bool announceIfEmpty = true)
        {
            try
            {
                // Verify ability equip menu is actually active
                if (!AbilityEquipMenuTracker.ValidateState())
                {
                    return; // Silently fail if not active
                }

                // Get stored description
                string description = AbilityEquipMenuTracker.CurrentDescription;

                if (string.IsNullOrWhiteSpace(description))
                {
                    if (!announceIfEmpty) return;
                    FFV_ScreenReaderMod.SpeakText(T("No description available"), interrupt);
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing ability equip details: {ex.Message}");
            }
        }
    }

    // NOTE: OnHide method does not exist in FF5's JobChangeWindowController
    // Job menu state is now cleared via other means or when menu visibility changes

    /// <summary>
    /// Announces the initially-focused row when a job or ability screen is entered or returned to.
    /// SelectContent / SetCursor only fire on cursor movement, so the row the game starts on was
    /// silent. Hooks the state-entry *Init methods, which the navigation path never fires — the
    /// two are disjoint, so neither needs to suppress the other.
    ///
    /// Manual patching because every target is private or protected override.
    /// </summary>
    public static class FieldJobAbilityReannouncePatches
    {
        // AbilityWindowController (dump.cs 286011): commandController 0x60, listController 0x70,
        //   useController 0x78
        // AbilityCommandController (280453): contentList 0x28, selectCursor 0x38
        // AbilityContentListController (285082): selectCursor 0x38, contentList 0x50
        // AbilityUseContentListController (285635): contentList 0x48, selectCursor 0x50
        // JobChangeWindowBaseController (292203): jobSelectCursor 0x40
        // AbilityChangeController (286594): NO cursor field — index comes from the cache in
        //   AbilityChangeController_SelectContent_Patch / _SelectCommand_Patch.
        // All read typed below; offsets documented for traceability only.

        private static readonly Type AbilityWindowType = typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityWindowController);
        private static readonly Type AbilityChangeType = typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController);
        private static readonly Type AbilityUseListType = typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController);
        private static readonly Type JobChangeType = typeof(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController);

        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, JobChangeType, "SelectJobInit", nameof(JobSelect_Init_Postfix));

            Patch(harmony, AbilityWindowType, "CommandInit", nameof(AbilityCommand_Init_Postfix));
            Patch(harmony, AbilityWindowType, "UseListInit", nameof(SpellList_Init_Postfix));
            Patch(harmony, AbilityWindowType, "UseTargetInit", nameof(UseTarget_Init_Postfix));

            // AbilityWindowController.NonInit has a real body at its own unique address, so it is
            // safe to hook and serves as the single exit point for this whole family.
            Patch(harmony, AbilityWindowType, "NonInit", nameof(Exit_Init_Postfix));

            // The target list also has its own Single/All states reached from UseTargetInit.
            Patch(harmony, AbilityUseListType, "SingleInit", nameof(UseTargetList_Init_Postfix));
            Patch(harmony, AbilityUseListType, "AllInit", nameof(UseTargetList_Init_Postfix));

            Patch(harmony, AbilityChangeType, "SelectCommandInit", nameof(EquipCommand_Init_Postfix));
            Patch(harmony, AbilityChangeType, "SelectListInit", nameof(EquipList_Init_Postfix));

            // JobChangeWindowController.NoneInit and AbilityChangeController.NoneInit are
            // deliberately NOT patched: both bodies are empty, and IL2CPP folds every empty method
            // in the game onto ONE shared native address (2561440 here, backing 4398 methods; the
            // job one shares 4886656). Patching a folded stub detours all of them at once and
            // hard-crashes on launch with no managed exception. Verify with script.json before
            // hooking any *Init: two entries sharing an "Address" means it is a folded stub.
            // AbilityWindowController.NonInit above already covers the exit cleanup.
        }

        /// <summary>Any window left its panes — drop pending reads and clear the cached positions.</summary>
        public static void Exit_Init_Postfix()
        {
            MenuFocusAnnouncer.Cancel();
            AbilityChangeController_SelectContent_Patch.ClearLast();
            AbilityChangeController_SelectCommand_Patch.ClearLast();
        }

        public static void JobSelect_Init_Postfix(object __instance)
        {
            var controller = __instance as Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController;
            if (controller == null) return;

            JobChangeWindowController_SelectContent_Patch.ClearLast();
            MenuFocusAnnouncer.Request("JobSelect", () =>
            {
                if (!IsUsable(controller)) return false;

                var cursor = controller.jobSelectCursor;    // inherited from JobChangeWindowBaseController
                if (cursor == null) return false;

                return JobChangeWindowController_SelectContent_Patch.Announce(controller, cursor.Index);
            });
        }

        public static void AbilityCommand_Init_Postfix(object __instance)
        {
            var window = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityWindowController;
            if (window == null) return;

            AbilityCommandController_SelectContent_Patch.ClearLast();
            MenuFocusAnnouncer.Request("AbilityCommand", () =>
            {
                if (!IsUsable(window)) return false;

                var commandController = window.commandController;
                if (commandController == null) return false;

                var cursor = commandController.selectCursor;    // Cursor @ 0x38
                if (cursor == null) return false;

                return AbilityCommandController_SelectContent_Patch.Announce(commandController, cursor.Index);
            });
        }

        public static void SpellList_Init_Postfix(object __instance)
        {
            var window = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityWindowController;
            if (window == null) return;

            AbilityContentListController_SetCursor_Patch.ClearLast();
            MenuFocusAnnouncer.Request("SpellList", () =>
            {
                if (!IsUsable(window)) return false;

                var listController = window.listController;
                if (listController == null) return false;

                var cursor = listController.selectCursor;
                if (cursor == null) return false;

                return AbilityContentListController_SetCursor_Patch.Announce(listController, cursor.Index);
            });
        }

        public static void UseTarget_Init_Postfix(object __instance)
        {
            var window = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityWindowController;
            if (window == null) return;

            AbilityUseContentListController_SelectContent_Patch.ClearLast();
            MenuFocusAnnouncer.Request("AbilityTarget", () =>
            {
                if (!IsUsable(window)) return false;

                var useController = window.useController;
                return useController != null && AnnounceUseTarget(useController);
            });
        }

        public static void UseTargetList_Init_Postfix(object __instance)
        {
            var useController = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController;
            if (useController == null) return;

            AbilityUseContentListController_SelectContent_Patch.ClearLast();
            MenuFocusAnnouncer.Request("AbilityTarget", () =>
            {
                if (!MenuFocusAnnouncer.IsAlive(useController)) return false;
                if (BattleState.IsInBattle) return false;    // battle targeting has its own reader
                if (!MenuFocusAnnouncer.IsMenuOpen()) return false;

                return AnnounceUseTarget(useController);
            });
        }

        public static void EquipCommand_Init_Postfix(object __instance)
        {
            var controller = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController;
            if (controller == null) return;

            AbilityChangeController_SelectCommand_Patch.ClearLast();
            MenuFocusAnnouncer.Request("AbilityEquipCommand", () =>
            {
                if (!IsUsable(controller)) return false;

                // No cursor field on this controller — use the cached position (0 on first entry,
                // which is the game's own default).
                return AbilityChangeController_SelectCommand_Patch.Announce(
                    controller, AbilityChangeController_SelectCommand_Patch.CachedIndex);
            });
        }

        public static void EquipList_Init_Postfix(object __instance)
        {
            var controller = __instance as Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController;
            if (controller == null) return;

            AbilityChangeController_SelectContent_Patch.ClearLast();
            MenuFocusAnnouncer.Request("AbilityEquipList", () =>
            {
                if (!IsUsable(controller)) return false;

                return AbilityChangeController_SelectContent_Patch.Announce(
                    controller, AbilityChangeController_SelectContent_Patch.CachedIndex);
            });
        }

        private static bool AnnounceUseTarget(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController useController)
        {
            var cursor = useController.selectCursor;
            if (cursor == null) return false;

            return AbilityUseContentListController_SelectContent_Patch.Announce(useController, cursor.Index);
        }

        private static bool IsUsable(UnityEngine.Component controller)
        {
            if (!MenuFocusAnnouncer.IsAlive(controller)) return false;
            return MenuFocusAnnouncer.IsMenuOpen();
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type type, string methodName, string postfixName)
        {
            try
            {
                var target = AccessTools.Method(type, methodName);
                if (target == null)
                {
                    MelonLogger.Warning($"[JobAbility] {type.Name}.{methodName} not found");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    typeof(FieldJobAbilityReannouncePatches).GetMethod(postfixName)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[JobAbility] Failed to patch {type.Name}.{methodName}: {ex.Message}");
            }
        }
    }
}
