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
            if (IsJobMenuActive && !AnnouncementDeduplicator.IsControllerActive(ActiveController))
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
            if (IsAbilityMenuActive && !AnnouncementDeduplicator.IsControllerActive(ActiveController))
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
            if (IsActive && !AnnouncementDeduplicator.IsControllerActive(ActiveController))
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
            if (IsActive && !AnnouncementDeduplicator.IsControllerActive(ActiveController))
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
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                if (__instance == null) return;

                // Track that job menu is active and clear other menu trackers
                ItemMenuTracker.ClearState();
                JobMenuTracker.IsJobMenuActive = true;
                JobMenuTracker.ActiveController = __instance;
                JobMenuTracker.CurrentJobIndex = index;

                // Get character and job data using the public method
                var targetCharacter = __instance.GetTargetCharacterData();
                if (targetCharacter == null) return;

                // Get released (unlocked) jobs
                var job = SelectContentHelper.TryGetItem(__instance.GetReleaseJobs(), index);
                if (job == null) return;

                // Get job name from message manager
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string jobName = messageManager.GetMessage(job.MesIdName);
                if (string.IsNullOrWhiteSpace(jobName)) return;

                // Read level, mastered status, and ABP from UI text fields
                // (data-based OwnedJob.Level returns wrong values for level 0 jobs)
                var view = __instance.view;
                if (view == null) return;

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

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_SELECT, announcement)) return;

                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in JobChangeWindowController.SelectContent patch: {ex.Message}");
            }
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
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController __instance, int index)
        {
            try
            {
                if (__instance == null) return;

                var contentView = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (contentView == null) return;

                // Get the command from the content view
                var command = contentView.Command;
                if (command == null)
                {
                    string emptyAnnouncement = "Empty slot";
                    if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_COMMAND_SLOT, emptyAnnouncement)) return;

                    FFV_ScreenReaderMod.SpeakText(emptyAnnouncement);
                    return;
                }

                // Get command name
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string commandName = messageManager.GetMessage(command.MesIdName);
                if (string.IsNullOrWhiteSpace(commandName)) return;

                string announcement = commandName;

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_COMMAND_SLOT, announcement)) return;

                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityCommandController.SelectContent patch: {ex.Message}");
            }
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
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                if (__instance == null) return;

                var view = __instance.view;
                if (view == null) return;

                var scrollView = view.ScrollView;
                if (scrollView == null) return;

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
                    string emptyText = "Empty";
                    if (AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_ABILITY_EQUIP, emptyText))
                    {
                        FFV_ScreenReaderMod.SpeakText(emptyText);
                    }
                    return;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

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
                    string emptyText = "Empty";
                    if (AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_ABILITY_EQUIP, emptyText))
                    {
                        FFV_ScreenReaderMod.SpeakText(emptyText);
                    }
                    return;
                }

                // Get ability name
                string abilityName = messageManager.GetMessage(abilityEquipData.NameMessageId);
                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    string emptyText = "Empty";
                    if (AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_ABILITY_EQUIP, emptyText))
                    {
                        FFV_ScreenReaderMod.SpeakText(emptyText);
                    }
                    return;
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

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_ABILITY_EQUIP, announcement)) return;

                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for command slot selection in ability equipping menu.
    /// Announces which command slot is being modified and what's currently equipped.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController.SelectCommand))]
    public static class AbilityChangeController_SelectCommand_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null) return;

                var view = __instance.view;
                if (view == null) return;

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

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_EQUIP_COMMAND, announcement)) return;

                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectCommand patch: {ex.Message}");
            }
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
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController> targetContents,
            GameCursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                int index = targetCursor.Index;
                var selectedController = SelectContentHelper.TryGetItem(__instance.contentList, index);
                if (selectedController == null || selectedController.CurrentData == null) return;

                var data = selectedController.CurrentData;
                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName)) return;

                // Build announcement with HP, MP, and status conditions
                string announcement = characterName + CharacterStatusHelper.GetFullStatus(data.Parameter);

                // Skip duplicates
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_USE_TARGET, announcement)) return;

                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityUseContentListController.SelectContent patch: {ex.Message}");
            }
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
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController __instance,
            GameCursor targetCursor, bool isScroll, CustomScrollView.WithinRangeType type, bool pageSkip)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                int index = targetCursor.Index;
                OwnedAbility ability = null;
                unsafe
                {
                    IntPtr contentListPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x50);
                    if (contentListPtr != IntPtr.Zero)
                    {
                        var contentList = new Il2CppSystem.Collections.Generic.List<BattleAbilityInfomationContentController>(contentListPtr);
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
                    string emptyAnnouncement = "Empty";
                    if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_SPELL_LIST, emptyAnnouncement)) return;
                    FFV_ScreenReaderMod.SpeakText(emptyAnnouncement);
                    return;
                }

                // Get ability name from message manager
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string abilityName = messageManager.GetMessage(ability.MesIdName);
                if (string.IsNullOrWhiteSpace(abilityName)) return;

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

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.JOB_SPELL_LIST, announcement)) return;

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
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityContentListController.SetCursor patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to announce full job details (description) when I key is pressed
    /// </summary>
    public static class JobDetailsAnnouncer
    {
        public static void AnnounceCurrentJobDetails()
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
                    announcement = "No description available";
                }

                FFV_ScreenReaderMod.SpeakText(announcement);
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
        public static void AnnounceCurrentAbilityDetails()
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
                    FFV_ScreenReaderMod.SpeakText("No description available");
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description);
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
        public static void AnnounceCurrentDetails()
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
                    FFV_ScreenReaderMod.SpeakText("No description available");
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description);
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
        public static void AnnounceCurrentDetails()
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
                    FFV_ScreenReaderMod.SpeakText("No description available");
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(description);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing ability equip details: {ex.Message}");
            }
        }
    }

    // NOTE: OnHide method does not exist in FF5's JobChangeWindowController
    // Job menu state is now cleared via other means or when menu visibility changes
}
