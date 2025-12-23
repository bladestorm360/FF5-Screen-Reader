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
using Il2CppSerial.FF5.UI.Touch;
using Il2CppSerial.FF5.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    // Track job menu state for I key handling
    public static class JobMenuTracker
    {
        public static bool IsJobMenuActive { get; set; }
        public static Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController ActiveController { get; set; }
        public static int CurrentJobIndex { get; set; }
    }

    /// <summary>
    /// Patch for job selection - announces job name, level, and stats when browsing job list.
    /// Uses KeyInput namespace for keyboard/gamepad support.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController), "SelectContent")]
    public static class JobChangeWindowController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                if (__instance == null) return;

                // Track that job menu is active
                JobMenuTracker.IsJobMenuActive = true;
                JobMenuTracker.ActiveController = __instance;
                JobMenuTracker.CurrentJobIndex = index;

                // Get the selected character's data
                var targetCharacter = __instance.GetTargetCharacterData();
                if (targetCharacter == null) return;

                // Get the character's owned job list
                var ownedJobDataList = targetCharacter.OwnedJobDataList;
                if (ownedJobDataList == null || index < 0 || index >= ownedJobDataList.Count) return;

                // TEMPORARY FIX: Testing if index needs offset
                int actualIndex = (index + 1) % ownedJobDataList.Count;

                MelonLogger.Msg($"[Job Selection DEBUG] Param Index: {index}, Adjusted Index: {actualIndex}, Job List Count: {ownedJobDataList.Count}");

                // Get the job at the adjusted index
                var ownedJob = ownedJobDataList[actualIndex];
                if (ownedJob == null) return;

                // Get job master data
                var masterManager = MasterManager.Instance;
                if (masterManager == null) return;

                var job = masterManager.GetData<Job>(ownedJob.Id);
                if (job == null) return;

                // Get localized job name
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string jobName = messageManager.GetMessage(job.MesIdName);
                if (string.IsNullOrWhiteSpace(jobName)) return;

                // Get job mastery level
                int jobLevel = ownedJob.Level;

                // Build announcement with ONLY job name and level
                string announcement = $"{jobName}, level {jobLevel}";

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Job Selection] {announcement}");
                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in JobChangeWindowController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability list selection - announces abilities when browsing ability menu.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController.SelectContent))]
    public static class AbilityContentListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController __instance, int index, CustomScrollView.WithinRangeType type)
        {
            try
            {
                if (__instance == null) return;

                var abilityList = __instance.AbilityList;
                if (abilityList == null || index < 0 || index >= abilityList.Count) return;

                var ownedAbility = abilityList[index];
                if (ownedAbility == null || ownedAbility.Ability == null) return;

                // Get ability name
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                string abilityName = messageManager.GetMessage(ownedAbility.MesIdName);
                if (string.IsNullOrWhiteSpace(abilityName)) return;

                // Get ability skill level (ABP level)
                int abilityLevel = ownedAbility.SkillLevel;

                // Build announcement
                string announcement = $"{abilityName}, level {abilityLevel}";

                // Add MP cost if available
                try
                {
                    if (ownedAbility.Ability != null)
                    {
                        int mpCost = ownedAbility.Ability.UseValue;
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

                // Add description
                try
                {
                    string description = messageManager.GetMessage(ownedAbility.MesIdDescription);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $". {description}";
                    }
                }
                catch
                {
                    // Description not available, continue without it
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability List] {announcement}");
                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability command slots - announces equipped abilities or "empty" when selecting slots.
    /// Reads from character's actual CommandList to avoid stale cached data after job changes.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController.SelectContent))]
    public static class AbilityCommandController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityCommandController __instance, int index)
        {
            try
            {
                if (__instance == null) return;

                // Access the private targetCharacter field using Harmony's Traverse
                var targetCharacter = Traverse.Create(__instance).Field("targetCharacter").GetValue<OwnedCharacterData>();
                if (targetCharacter == null)
                {
                    MelonLogger.Warning("[Ability Slot] targetCharacter is null");
                    return;
                }

                // Read directly from character's actual CommandList instead of view's contentList
                var commandList = targetCharacter.CommandList;
                if (commandList == null)
                {
                    MelonLogger.Warning("[Ability Slot] CommandList is null");
                    return;
                }

                if (index < 0 || index >= commandList.Count)
                {
                    MelonLogger.Warning($"[Ability Slot] Index {index} out of range (CommandList count: {commandList.Count})");
                    return;
                }

                // Get the command for this slot from the actual character data
                var command = commandList[index];
                if (command == null)
                {
                    // Empty slot
                    string emptyAnnouncement = "Empty slot";
                    if (emptyAnnouncement == lastAnnouncement) return;
                    lastAnnouncement = emptyAnnouncement;

                    MelonLogger.Msg($"[Ability Slot] {emptyAnnouncement}");
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
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Slot] {announcement}");
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
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController), nameof(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController.SelectContent))]
    public static class AbilityChangeController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                if (__instance == null) return;

                var view = __instance.view;
                if (view == null) return;

                // Get the content list from the view
                var contentList = view.CurrentList;
                if (contentList == null || contentList.Count == 0) return;

                // Validate index
                if (index < 0 || index >= contentList.Count) return;

                // Get the content controller at the index
                var content = contentList[index];
                if (content == null) return;

                // Get the content view
                var contentView = content.view;
                if (contentView == null) return;

                // Get the content controller which contains the data
                var contentController = contentView.Content;
                if (contentController == null) return;

                // Get the ability equip data
                var abilityEquipData = contentController.TargetData;
                if (abilityEquipData == null) return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                // Get ability name
                string abilityName = messageManager.GetMessage(abilityEquipData.NameMessageId);
                if (string.IsNullOrWhiteSpace(abilityName)) return;

                // Build announcement
                string announcement = abilityName;

                // Add equipped status
                if (abilityEquipData.IsEquiped)
                {
                    announcement += " - EQUIPPED";
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

                // Add description
                try
                {
                    string description = messageManager.GetMessage(abilityEquipData.DescriptionMessageId);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $". {description}";
                    }
                }
                catch
                {
                    // Description not available, continue without it
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Equip List] {announcement}");
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
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null) return;

                var view = __instance.view;
                if (view == null) return;

                // Get the equipped command from the slot's view controller
                string slotContent = "empty";

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
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading command slot {index + 1}: {ex.Message}");
                }

                // Format: "Slot 1: White Magic" or "Slot 1: empty"
                string announcement = $"Slot {index + 1}: {slotContent}";

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Equip Command] {announcement}");
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
        new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController>), typeof(Cursor) })]
    public static class AbilityUseContentListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityUseContentListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController> targetContents,
            Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null) return;

                // Get the content list from the controller
                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0) return;

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count) return;

                var selectedController = contentList[index];
                if (selectedController == null || selectedController.CurrentData == null) return;

                var data = selectedController.CurrentData;
                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName)) return;

                // Build announcement with HP and MP information
                string announcement = characterName;

                try
                {
                    // Get the character's parameter data
                    var parameter = data.Parameter;
                    if (parameter != null)
                    {
                        int currentHP = parameter.CurrentHP;
                        int maxHP = parameter.ConfirmedMaxHp();
                        int currentMP = parameter.CurrentMP;
                        int maxMP = parameter.ConfirmedMaxMp();

                        announcement += $", HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}";

                        // Get status conditions
                        var conditionList = parameter.ConfirmedConditionList();
                        if (conditionList != null && conditionList.Count > 0)
                        {
                            var messageManager = MessageManager.Instance;
                            if (messageManager != null)
                            {
                                var statusNames = new System.Collections.Generic.List<string>();

                                foreach (var condition in conditionList)
                                {
                                    if (condition != null)
                                    {
                                        string conditionMesId = condition.MesIdName;

                                        // Skip conditions with no message ID (internal/hidden statuses)
                                        if (!string.IsNullOrEmpty(conditionMesId) && conditionMesId != "None")
                                        {
                                            string localizedConditionName = messageManager.GetMessage(conditionMesId);
                                            if (!string.IsNullOrEmpty(localizedConditionName))
                                            {
                                                statusNames.Add(localizedConditionName);
                                            }
                                        }
                                    }
                                }

                                if (statusNames.Count > 0)
                                {
                                    announcement += $", {string.Join(", ", statusNames)}";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading HP/MP/Status for {characterName}: {ex.Message}");
                    // Continue with just the name if stats can't be read
                }

                // Skip duplicates
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Target] {announcement}");
                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityUseContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to announce full job details (stats + description) when I key is pressed
    /// </summary>
    public static class JobDetailsAnnouncer
    {
        public static void AnnounceCurrentJobDetails()
        {
            try
            {
                if (!JobMenuTracker.IsJobMenuActive || JobMenuTracker.ActiveController == null)
                {
                    FFV_ScreenReaderMod.SpeakText("Job menu not active");
                    return;
                }

                var controller = JobMenuTracker.ActiveController;
                int index = JobMenuTracker.CurrentJobIndex;

                // Get the selected character's data
                var targetCharacter = controller.GetTargetCharacterData();
                if (targetCharacter == null) return;

                // Get the character's owned job list
                var ownedJobDataList = targetCharacter.OwnedJobDataList;
                if (ownedJobDataList == null || index < 0 || index >= ownedJobDataList.Count) return;

                // Use same index adjustment as SelectContent patch
                int actualIndex = (index + 1) % ownedJobDataList.Count;

                // Get the job at the adjusted index
                var ownedJob = ownedJobDataList[actualIndex];
                if (ownedJob == null) return;

                // Get job master data
                var masterManager = MasterManager.Instance;
                if (masterManager == null) return;

                var job = masterManager.GetData<Job>(ownedJob.Id);
                if (job == null) return;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return;

                // Build announcement with stats and description
                string announcement = $"Strength {job.Strength}, Vitality {job.Vitality}, Agility {job.Agility}, Magic {job.Magic}";

                // Add job description
                try
                {
                    string description = messageManager.GetMessage(job.MesIdDescription);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $". {description}";
                    }
                }
                catch
                {
                    // Description not available, continue without it
                }

                MelonLogger.Msg($"[Job Details] {announcement}");
                FFV_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing job details: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch to detect when job menu window is hidden/closed
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.JobChangeWindowController), "OnHide")]
    public static class JobChangeWindowController_OnHide_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            JobMenuTracker.IsJobMenuActive = false;
            JobMenuTracker.ActiveController = null;
            JobMenuTracker.CurrentJobIndex = -1;
        }
    }
}
