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
    /// <summary>
    /// Shared helper for job and ability menu patches.
    /// </summary>
    internal static class JobAbilityPatchHelper
    {
        /// <summary>
        /// Helper coroutine to speak text after one frame delay.
        /// </summary>
        internal static IEnumerator DelayedSpeech(string text)
        {
            yield return null; // Wait one frame
            FFV_ScreenReaderMod.SpeakText(text);
        }
    }

    /// <summary>
    /// Patch for job selection - announces job name and level when browsing job list.
    /// Uses the same pattern as ItemMenuPatches - patching SelectContent method.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.Touch.JobChangeWindowController), "SelectContent")]
    public static class JobChangeWindowController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.Touch.JobChangeWindowController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                if (__instance == null) return;

                // Get the selected character's data
                var targetCharacter = __instance.GetTargetCharacterData();
                if (targetCharacter == null) return;

                // Get the character's owned job list
                var ownedJobDataList = targetCharacter.OwnedJobDataList;
                if (ownedJobDataList == null || index < 0 || index >= ownedJobDataList.Count) return;

                // Get the job at the selected index
                var ownedJob = ownedJobDataList[index];
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

                string announcement = $"{jobName}, level {jobLevel}";

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Job Selection] {announcement}");
                CoroutineManager.StartManaged(JobAbilityPatchHelper.DelayedSpeech(announcement));
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

                string announcement = $"{abilityName}, level {abilityLevel}";

                // Skip duplicate announcements
                if (announcement == lastAnnouncement) return;
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability List] {announcement}");
                CoroutineManager.StartManaged(JobAbilityPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability command slots - announces equipped abilities or "empty" when selecting slots.
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

                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0) return;

                if (index < 0 || index >= contentList.Count) return;

                var contentView = contentList[index];
                if (contentView == null) return;

                // Get the command for this slot
                var command = contentView.Command;
                if (command == null)
                {
                    // Empty slot
                    string emptyAnnouncement = "Empty slot";
                    if (emptyAnnouncement == lastAnnouncement) return;
                    lastAnnouncement = emptyAnnouncement;

                    MelonLogger.Msg($"[Ability Slot] {emptyAnnouncement}");
                    CoroutineManager.StartManaged(JobAbilityPatchHelper.DelayedSpeech(emptyAnnouncement));
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
                CoroutineManager.StartManaged(JobAbilityPatchHelper.DelayedSpeech(announcement));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityCommandController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
