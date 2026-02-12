using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppSerial.FF5.UI.KeyInput;
using Il2CppLast.Data.User;
using Il2CppLast.Data.Master;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Message;
using Il2CppLast.Management;
using MelonLoader;
using UnityEngine.UI;
using FFV_ScreenReader.Patches;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading character status details.
    /// Provides stat reading functions for physical and magical stats.
    /// Ported from FF6 screen reader.
    /// </summary>
    public static class StatusDetailsReader
    {
        private static OwnedCharacterData currentCharacterData = null;

        public static void SetCurrentCharacterData(OwnedCharacterData data)
        {
            currentCharacterData = data;
        }

        public static void ClearCurrentCharacterData()
        {
            currentCharacterData = null;
        }

        /// <summary>
        /// Read all character status information from the status details view.
        /// Returns a formatted string with all relevant information.
        /// </summary>
        public static string ReadStatusDetails(StatusDetailsController controller)
        {
            if (controller == null)
            {
                return null;
            }

            var statusView = controller.statusController?.view as AbilityCharaStatusView;
            var detailsView = controller.view;

            if (statusView == null && detailsView == null)
            {
                return null;
            }

            var parts = new List<string>();

            // Character name and level
            if (statusView != null)
            {
                string name = GetTextSafe(statusView.NameText);
                string level = GetTextSafe(statusView.CurrentLevelText);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    parts.Add(name);
                }

                if (!string.IsNullOrWhiteSpace(level))
                {
                    parts.Add($"Level {level}");
                }
            }

            // HP and MP
            if (statusView != null)
            {
                string currentHp = GetTextSafe(statusView.CurrentHpText);
                string maxHp = GetTextSafe(statusView.MaxHpText);
                string currentMp = GetTextSafe(statusView.CurrentMpText);
                string maxMp = GetTextSafe(statusView.MaxMpText);

                if (!string.IsNullOrWhiteSpace(currentHp) && !string.IsNullOrWhiteSpace(maxHp))
                {
                    parts.Add($"HP: {currentHp} / {maxHp}");
                }

                if (!string.IsNullOrWhiteSpace(currentMp) && !string.IsNullOrWhiteSpace(maxMp))
                {
                    parts.Add($"MP: {currentMp} / {maxMp}");
                }
            }

            // Experience info (if available)
            if (detailsView != null)
            {
                try
                {
                    string exp = GetTextSafe(detailsView.ExpText);
                    string nextExp = GetTextSafe(detailsView.NextExpText);

                    if (!string.IsNullOrWhiteSpace(exp))
                    {
                        parts.Add($"Experience: {exp}");
                    }

                    if (!string.IsNullOrWhiteSpace(nextExp))
                    {
                        parts.Add($"Next Level: {nextExp}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not read experience info: {ex.Message}");
                }
            }

            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>
        /// Read physical combat stats (Strength, Stamina, Defense, Evade).
        /// </summary>
        public static string ReadPhysicalStats()
        {
            if (currentCharacterData == null || currentCharacterData.parameter == null)
            {
                return "No character data available";
            }

            try
            {
                var param = currentCharacterData.parameter;
                var parts = new List<string>();

                int strength = param.ConfirmedPower();
                parts.Add($"Strength: {strength}");

                int stamina = param.ConfirmedVitality();
                parts.Add($"Stamina: {stamina}");

                int defense = param.ConfirmedDefense();
                parts.Add($"Defense: {defense}");

                int evade = param.ConfirmedDefenseCount();
                parts.Add($"Evade: {evade}");

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading physical stats: {ex.Message}");
                return $"Error reading physical stats: {ex.Message}";
            }
        }

        /// <summary>
        /// Read magical combat stats (Magic, Spirit, Magic Defense, Magic Evade).
        /// </summary>
        public static string ReadMagicalStats()
        {
            if (currentCharacterData == null || currentCharacterData.parameter == null)
            {
                return "No character data available";
            }

            try
            {
                var param = currentCharacterData.parameter;
                var parts = new List<string>();

                int magic = param.ConfirmedMagic();
                parts.Add($"Magic: {magic}");

                int spirit = param.ConfirmedSpirit();
                parts.Add($"Spirit: {spirit}");

                int magicDefense = param.ConfirmedAbilityDefense();
                parts.Add($"Magic Defense: {magicDefense}");

                int magicEvade = param.ConfirmedAbilityEvasionRate();
                parts.Add($"Magic Evade: {magicEvade}");

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading magical stats: {ex.Message}");
                return $"Error reading magical stats: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Stat groups for organizing status screen statistics
    /// </summary>
    public enum StatGroup
    {
        CharacterInfo,  // Job Name, Character Level, Job Level, Experience, ABP
        Vitals,         // HP, MP
        Attributes,     // Strength, Agility, Stamina, Magic
        CombatStats,    // Attack, Defense, Evasion, Magic Defense
        Progression     // Jobs, Abilities
    }

    /// <summary>
    /// Definition of a single stat that can be navigated
    /// </summary>
    public class StatusStatDefinition
    {
        public string Name { get; set; }
        public StatGroup Group { get; set; }
        public Func<OwnedCharacterData, string> Reader { get; set; }

        public StatusStatDefinition(string name, StatGroup group, Func<OwnedCharacterData, string> reader)
        {
            Name = name;
            Group = group;
            Reader = reader;
        }
    }

    /// <summary>
    /// Handles navigation through status screen stats using arrow keys
    /// </summary>
    public static class StatusNavigationReader
    {
        private static List<StatusStatDefinition> statList = null;
        private static readonly int[] GroupStartIndices = new int[] { 0, 5, 7, 11, 15 };

        /// <summary>
        /// Initialize the stat list with all 19 visible stats in UI order
        /// </summary>
        public static void InitializeStatList()
        {
            if (statList != null) return;

            statList = new List<StatusStatDefinition>();

            // Character Info Group (indices 0-4)
            statList.Add(new StatusStatDefinition("Job", StatGroup.CharacterInfo, ReadJobName));
            statList.Add(new StatusStatDefinition("Character Level", StatGroup.CharacterInfo, ReadCharacterLevel));
            statList.Add(new StatusStatDefinition("Job Level", StatGroup.CharacterInfo, ReadJobLevel));
            statList.Add(new StatusStatDefinition("Experience", StatGroup.CharacterInfo, ReadExperience));
            statList.Add(new StatusStatDefinition("ABP", StatGroup.CharacterInfo, ReadABP));

            // Vitals Group (indices 5-6)
            statList.Add(new StatusStatDefinition("HP", StatGroup.Vitals, ReadHP));
            statList.Add(new StatusStatDefinition("MP", StatGroup.Vitals, ReadMP));

            // Attributes Group (indices 7-10)
            statList.Add(new StatusStatDefinition("Strength", StatGroup.Attributes, ReadStrength));
            statList.Add(new StatusStatDefinition("Agility", StatGroup.Attributes, ReadAgility));
            statList.Add(new StatusStatDefinition("Stamina", StatGroup.Attributes, ReadStamina));
            statList.Add(new StatusStatDefinition("Magic", StatGroup.Attributes, ReadMagic));

            // Combat Stats Group (indices 11-14)
            statList.Add(new StatusStatDefinition("Attack", StatGroup.CombatStats, ReadAttack));
            statList.Add(new StatusStatDefinition("Defense", StatGroup.CombatStats, ReadDefense));
            statList.Add(new StatusStatDefinition("Evasion", StatGroup.CombatStats, ReadEvasion));
            statList.Add(new StatusStatDefinition("Magic Defense", StatGroup.CombatStats, ReadMagicDefense));

            // Progression Group (indices 15-16)
            statList.Add(new StatusStatDefinition("Jobs", StatGroup.Progression, ReadJobs));
            statList.Add(new StatusStatDefinition("Abilities", StatGroup.Progression, ReadAbilities));
        }

        /// <summary>
        /// Navigate to the next stat (wraps to top at end)
        /// </summary>
        public static void NavigateNext()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = (tracker.CurrentStatIndex + 1) % statList.Count;
            ReadCurrentStat();
        }

        /// <summary>
        /// Navigate to the previous stat (wraps to bottom at top)
        /// </summary>
        public static void NavigatePrevious()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex--;
            if (tracker.CurrentStatIndex < 0)
            {
                tracker.CurrentStatIndex = statList.Count - 1;
            }
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the first stat of the next group
        /// </summary>
        public static void JumpToNextGroup()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            int currentIndex = tracker.CurrentStatIndex;
            int nextGroupIndex = -1;

            // Find next group start index
            for (int i = 0; i < GroupStartIndices.Length; i++)
            {
                if (GroupStartIndices[i] > currentIndex)
                {
                    nextGroupIndex = GroupStartIndices[i];
                    break;
                }
            }

            // Wrap to first group if at end
            if (nextGroupIndex == -1)
            {
                nextGroupIndex = GroupStartIndices[0];
            }

            tracker.CurrentStatIndex = nextGroupIndex;
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the first stat of the previous group
        /// </summary>
        public static void JumpToPreviousGroup()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            int currentIndex = tracker.CurrentStatIndex;
            int prevGroupIndex = -1;

            // Find previous group start index
            for (int i = GroupStartIndices.Length - 1; i >= 0; i--)
            {
                if (GroupStartIndices[i] < currentIndex)
                {
                    prevGroupIndex = GroupStartIndices[i];
                    break;
                }
            }

            // Wrap to last group if at beginning
            if (prevGroupIndex == -1)
            {
                prevGroupIndex = GroupStartIndices[GroupStartIndices.Length - 1];
            }

            tracker.CurrentStatIndex = prevGroupIndex;
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the top (first stat)
        /// </summary>
        public static void JumpToTop()
        {
            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = 0;
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the bottom (last stat)
        /// </summary>
        public static void JumpToBottom()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = statList.Count - 1;
            ReadCurrentStat();
        }

        /// <summary>
        /// Read the stat at the current index
        /// </summary>
        public static void ReadCurrentStat()
        {
            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.ValidateState())
            {
                FFV_ScreenReader.Core.FFV_ScreenReaderMod.SpeakText("Navigation not available");
                return;
            }

            ReadStatAtIndex(tracker.CurrentStatIndex);
        }

        /// <summary>
        /// Read the stat at the specified index
        /// </summary>
        private static void ReadStatAtIndex(int index)
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;

            if (index < 0 || index >= statList.Count)
            {
                MelonLogger.Warning($"Invalid stat index: {index}");
                return;
            }

            if (tracker.CurrentCharacterData == null)
            {
                FFV_ScreenReader.Core.FFV_ScreenReaderMod.SpeakText("No character data");
                return;
            }

            try
            {
                var stat = statList[index];
                string value = stat.Reader(tracker.CurrentCharacterData);
                FFV_ScreenReader.Core.FFV_ScreenReaderMod.SpeakText(value, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading stat at index {index}: {ex.Message}");
                FFV_ScreenReader.Core.FFV_ScreenReaderMod.SpeakText("Error reading stat");
            }
        }

        // Character Info readers
        private static string ReadCharacterLevel(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Level: {data.Parameter.ConfirmedLevel()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading character level: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadJobLevel(OwnedCharacterData data)
        {
            try
            {
                if (data?.OwnedJob == null) return "N/A";
                return $"Job Level: {data.OwnedJob.Level}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading job level: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadJobName(OwnedCharacterData data)
        {
            try
            {
                if (data == null) return "N/A";

                // Read job name from UI text component
                var tracker = StatusNavigationTracker.Instance;
                if (tracker?.ActiveController?.statusController?.view != null)
                {
                    var statusView = tracker.ActiveController.statusController.view as AbilityCharaStatusView;
                    if (statusView?.JobNameText != null)
                    {
                        string jobName = statusView.JobNameText.text;
                        if (!string.IsNullOrWhiteSpace(jobName))
                        {
                            return $"Job: {jobName}";
                        }
                    }
                }

                // Fallback if UI text not available
                return $"Job: ID {data.JobId}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading job name: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadABP(OwnedCharacterData data)
        {
            try
            {
                if (data?.OwnedJob == null) return "N/A";

                // Read ABP from UI text components
                var tracker = StatusNavigationTracker.Instance;
                if (tracker?.ActiveController?.view != null)
                {
                    var detailsView = tracker.ActiveController.view;
                    if (detailsView.CurrentAbpText != null && detailsView.MaxAbpText != null)
                    {
                        string currentAbpText = detailsView.CurrentAbpText.text;
                        string maxAbpText = detailsView.MaxAbpText.text;

                        if (!string.IsNullOrWhiteSpace(currentAbpText) && !string.IsNullOrWhiteSpace(maxAbpText))
                        {
                            return $"ABP: {currentAbpText} / {maxAbpText}";
                        }
                    }
                }

                // Fallback if UI text not available
                int currentABP = data.OwnedJob.CurrentProficiency;
                return $"ABP: {currentABP}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading ABP: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadExperience(OwnedCharacterData data)
        {
            try
            {
                if (data == null) return "N/A";

                // Read experience from UI text components
                var tracker = StatusNavigationTracker.Instance;
                if (tracker?.ActiveController?.view != null)
                {
                    var detailsView = tracker.ActiveController.view;
                    if (detailsView.ExpText != null && detailsView.NextExpText != null)
                    {
                        string currentExpText = detailsView.ExpText.text;
                        string nextExpText = detailsView.NextExpText.text;

                        if (!string.IsNullOrWhiteSpace(currentExpText) && !string.IsNullOrWhiteSpace(nextExpText))
                        {
                            return $"Experience: {currentExpText} / {nextExpText} to next level";
                        }
                    }
                }

                // Fallback if UI text not available
                int currentExp = data.CurrentExp;
                int nextExp = data.GetNextExp();
                return $"Experience: {currentExp} / {nextExp} to next level";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Experience: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadAbilities(OwnedCharacterData data)
        {
            try
            {
                if (data?.OwnedAbilityList == null) return "N/A";

                // Read abilities from UI text components
                var tracker = StatusNavigationTracker.Instance;
                if (tracker?.ActiveController?.view != null)
                {
                    var detailsView = tracker.ActiveController.view;
                    if (detailsView.CurrentAbilityCountText != null && detailsView.MaxAbilityCountText != null)
                    {
                        string currentCount = detailsView.CurrentAbilityCountText.text;
                        string maxCount = detailsView.MaxAbilityCountText.text;

                        if (!string.IsNullOrWhiteSpace(currentCount) && !string.IsNullOrWhiteSpace(maxCount))
                        {
                            return $"Abilities: {currentCount} / {maxCount}";
                        }
                    }
                }

                // Fallback if UI text not available
                int count = data.OwnedAbilityList.Count;
                return $"Abilities: {count}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading abilities: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadJobs(OwnedCharacterData data)
        {
            try
            {
                if (data == null) return "N/A";

                // Read jobs from UI text components
                var tracker = StatusNavigationTracker.Instance;
                if (tracker?.ActiveController?.view != null)
                {
                    var detailsView = tracker.ActiveController.view;
                    if (detailsView.CurrentJobCountText != null && detailsView.MaxJobCountText != null)
                    {
                        string currentCount = detailsView.CurrentJobCountText.text;
                        string maxCount = detailsView.MaxJobCountText.text;

                        if (!string.IsNullOrWhiteSpace(currentCount) && !string.IsNullOrWhiteSpace(maxCount))
                        {
                            return $"Jobs: {currentCount} / {maxCount}";
                        }
                    }
                }

                return "Jobs: N/A";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading jobs: {ex.Message}");
                return "N/A";
            }
        }

        // Vitals readers
        private static string ReadHP(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                int current = data.Parameter.CurrentHP;
                int max = data.Parameter.ConfirmedMaxHp();
                return $"HP: {current} / {max}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading HP: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadMP(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                int current = data.Parameter.CurrentMP;
                int max = data.Parameter.ConfirmedMaxMp();
                return $"MP: {current} / {max}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading MP: {ex.Message}");
                return "N/A";
            }
        }

        // Attack readers
        private static string ReadAttack(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Attack: {data.Parameter.ConfirmedAttack()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Attack: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadMagic(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Magic: {data.Parameter.ConfirmedMagic()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Magic: {ex.Message}");
                return "N/A";
            }
        }

        // Defense readers
        private static string ReadDefense(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Defense: {data.Parameter.ConfirmedDefense()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Defense: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadMagicDefense(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Magic Defense: {data.Parameter.ConfirmedAbilityDefense()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Magic Defense: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadEvasion(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Evasion: {data.Parameter.ConfirmedDefenseCount()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Evasion: {ex.Message}");
                return "N/A";
            }
        }

        // Miscellaneous readers
        private static string ReadStrength(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Strength: {data.Parameter.ConfirmedPower()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Strength: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadAgility(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Agility: {data.Parameter.ConfirmedAgility()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Agility: {ex.Message}");
                return "N/A";
            }
        }

        private static string ReadStamina(OwnedCharacterData data)
        {
            try
            {
                if (data?.Parameter == null) return "N/A";
                return $"Stamina: {data.Parameter.ConfirmedVitality()}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Stamina: {ex.Message}");
                return "N/A";
            }
        }

    }
}