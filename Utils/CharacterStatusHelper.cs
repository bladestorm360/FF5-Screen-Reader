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
        // One-shot diagnostic flag — fires once per session to reveal root cause in logs
        private static bool _hasLoggedConditionDiag = false;

        // Whitelist of user-visible ConditionType values → readable English fallback names.
        // Internal states (Defend, Escape, resistance buffs, etc.) excluded by omission.
        private static readonly Dictionary<int, string> ConditionTypeFallbackNames = new Dictionary<int, string>
        {
            { 4, "Critical" },
            { 5, "KO" },
            { 6, "Silence" },
            { 7, "Sleep" },
            { 8, "Paralysis" },
            { 9, "Blind" },
            { 10, "Poison" },
            { 11, "Stone" },
            { 12, "Confusion" },
            { 13, "Transparent" },
            { 14, "Blink" },
            { 16, "Slow" },
            { 17, "Stop" },
            { 18, "Haste" },
            { 25, "Regen" },
            { 32, "Old" },
            { 34, "Zombie" },
            { 107, "Protect" },
            { 401, "Mini" },
            { 402, "Toad" },
            { 403, "Pig" },
            { 404, "Doom" },
            { 405, "Gradual Petrify" },
            { 406, "Curse" },
            { 409, "Float" },
            { 410, "Berserk" },
            { 412, "Shell" },
            { 413, "Reflect" }
        };

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

            bool shouldLog = !_hasLoggedConditionDiag;

            try
            {
                var conditionList = parameter.ConfirmedConditionList();

                if (shouldLog)
                {
                    MelonLogger.Msg($"[ConditionDiag] ConfirmedConditionList: {(conditionList == null ? "null" : $"count={conditionList.Count}")}");
                }

                if (conditionList == null || conditionList.Count == 0)
                    return string.Empty;

                // MessageManager is optional — we have fallback names
                MessageManager messageManager = null;
                try { messageManager = MessageManager.Instance; }
                catch { /* OK — will use fallback names */ }

                var statusNames = new List<string>();

                foreach (var condition in conditionList)
                {
                    if (condition == null)
                        continue;

                    int condType;
                    try { condType = (int)condition.ConditionType; }
                    catch
                    {
                        if (shouldLog) MelonLogger.Msg("[ConditionDiag] Failed to read ConditionType, skipping");
                        continue;
                    }

                    // Skip conditions not in our whitelist (internal/hidden states)
                    if (!ConditionTypeFallbackNames.ContainsKey(condType))
                    {
                        if (shouldLog) MelonLogger.Msg($"[ConditionDiag] ConditionType {condType} not in whitelist, skipping");
                        continue;
                    }

                    // Try localized name first
                    string displayName = null;
                    try
                    {
                        string mesId = condition.MesIdName;
                        if (shouldLog) MelonLogger.Msg($"[ConditionDiag] type={condType}, MesIdName=\"{mesId}\"");

                        if (!string.IsNullOrEmpty(mesId) && mesId != "None" && messageManager != null)
                        {
                            displayName = messageManager.GetMessage(mesId);
                        }
                    }
                    catch
                    {
                        // MesIdName or GetMessage failed — fall through to fallback
                        if (shouldLog) MelonLogger.Msg($"[ConditionDiag] type={condType}, MesIdName/GetMessage threw, using fallback");
                    }

                    // Fallback to dictionary name
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = ConditionTypeFallbackNames[condType];
                    }

                    statusNames.Add(displayName);
                }

                if (shouldLog)
                {
                    _hasLoggedConditionDiag = true;
                    MelonLogger.Msg($"[ConditionDiag] Final status names: [{string.Join(", ", statusNames)}]");
                }

                return statusNames.Count > 0 ? string.Join(", ", statusNames) : string.Empty;
            }
            catch (Exception ex)
            {
                if (shouldLog)
                {
                    _hasLoggedConditionDiag = true;
                    MelonLogger.Warning($"[ConditionDiag] GetStatusConditions outer error: {ex.Message}\n{ex.StackTrace}");
                }
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
                result += $", status: {conditions}";
            }

            return result;
        }
    }
}
