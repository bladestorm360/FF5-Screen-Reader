using System;
using System.Collections.Generic;
using Il2CppSerial.FF5.UI.KeyInput;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using MelonLoader;
using UnityEngine.UI;

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
        /// Safely get text from a Text component, returning null if invalid.
        /// </summary>
        private static string GetTextSafe(Text textComponent)
        {
            if (textComponent == null)
            {
                return null;
            }

            try
            {
                string text = textComponent.text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                // Trim and return
                return text.Trim();
            }
            catch
            {
                return null;
            }
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
}