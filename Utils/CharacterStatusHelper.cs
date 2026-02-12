using System;
using System.Collections.Generic;
using Il2CppLast.Data;
using Il2CppLast.Management;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Utility methods for reading character HP/MP and status conditions.
    /// Consolidates duplicate logic from multiple patch files.
    /// </summary>
    public static class CharacterStatusHelper
    {
        /// <summary>
        /// Gets the HP and MP string for a character parameter.
        /// </summary>
        /// <returns>Formatted string like "HP 100/200, MP 50/100" or empty string if parameter is null</returns>
        public static string GetVitalsString(CharacterParameterBase parameter)
        {
            if (parameter == null)
                return string.Empty;

            try
            {
                int currentHP = parameter.CurrentHP;
                int maxHP = parameter.ConfirmedMaxHp();
                int currentMP = parameter.CurrentMP;
                int maxMP = parameter.ConfirmedMaxMp();

                return $"HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"CharacterStatusHelper.GetVitalsString error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the status conditions for a character parameter.
        /// </summary>
        /// <returns>Comma-separated status conditions like "Poison, Blind" or empty string if none</returns>
        public static string GetStatusConditions(CharacterParameterBase parameter)
        {
            if (parameter == null)
                return string.Empty;

            try
            {
                var conditionList = parameter.ConfirmedConditionList();
                if (conditionList == null || conditionList.Count == 0)
                    return string.Empty;

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                    return string.Empty;

                var statusNames = new List<string>();

                foreach (var condition in conditionList)
                {
                    if (condition == null)
                        continue;

                    string conditionMesId = condition.MesIdName;

                    // Skip conditions with no message ID (internal/hidden statuses)
                    if (string.IsNullOrEmpty(conditionMesId) || conditionMesId == "None")
                        continue;

                    string localizedConditionName = messageManager.GetMessage(conditionMesId);
                    if (!string.IsNullOrEmpty(localizedConditionName))
                    {
                        statusNames.Add(localizedConditionName);
                    }
                }

                return statusNames.Count > 0 ? string.Join(", ", statusNames) : string.Empty;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"CharacterStatusHelper.GetStatusConditions error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the full status string for a character, including HP/MP and any status conditions.
        /// </summary>
        /// <returns>Formatted string like ", HP 100/200, MP 50/100, Poison, Blind" with leading comma, or empty string</returns>
        public static string GetFullStatus(CharacterParameterBase parameter)
        {
            if (parameter == null)
                return string.Empty;

            string vitals = GetVitalsString(parameter);
            if (string.IsNullOrEmpty(vitals))
                return string.Empty;

            string result = $", {vitals}";

            string conditions = GetStatusConditions(parameter);
            if (!string.IsNullOrEmpty(conditions))
            {
                result += $", {conditions}";
            }

            return result;
        }
    }
}
