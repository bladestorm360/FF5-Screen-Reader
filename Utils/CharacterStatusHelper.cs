using System;
using System.Collections.Generic;
using Il2CppLast.Data;
using Il2CppLast.Management;
using MelonLoader;
using static FFV_ScreenReader.Utils.ModTextTranslator;

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

        // Whitelist of user-visible ConditionType values (Last.Defaine.ConditionType) → fallback
        // name, a mod_text key spoken through T(). Internal states (Defend, Escape, resistance
        // buffs, etc.) excluded by omission. Used when a condition row has no name of its own:
        // FF5's condition table gives Poison, Blind, Stone, Toad, Mini, Float and one Doom row
        // mes_id_name "None". Each key's translations are the game's own words for that status
        // (system table MSG_SYSTEM_114-126/299/334/415 and the matching spell names), so a
        // fallback reads exactly as the game names the status in every language.
        //
        // Only types FF5's condition table actually uses (checked against master_assets
        // `condition`, 2026-09-24). Haste is Heist (15) and Protect is Proteus (19); the old
        // entries 18 (Brave) and 107 (Protectra) matched no FF5 condition, so Haste and Protect
        // were never read on a target. 13, 403, 405 and 406 (Transparent, Pig, Gradual Petrify,
        // Curse) do not occur in FF5 and are dropped.
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
            { 14, "Blink" },
            { 15, "Haste" },
            { 16, "Slow" },
            { 17, "Stop" },
            { 19, "Protect" },
            { 25, "Regen" },
            { 32, "Old" },
            { 34, "Zombie" },
            { 401, "Mini" },
            { 402, "Toad" },
            { 404, "Doom" },
            { 409, "Float" },
            { 410, "Berserk" },
            { 412, "Shell" },
            { 413, "Reflect" }
        };

        /// <summary>
        /// The localized fallback name for a condition type (T() of the whitelist key), or null
        /// when the type is not a user-visible status.
        /// </summary>
        public static string GetConditionTypeName(int conditionType)
        {
            return ConditionTypeFallbackNames.TryGetValue(conditionType, out string key) ? T(key) : null;
        }

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

                return $"{T("HP")} {currentHP}/{maxHP}, {T("MP")} {currentMP}/{maxMP}";
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

                    // Fallback to the localized dictionary name
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = GetConditionTypeName(condType);
                    }
                    else
                    {
                        displayName = TextUtils.StripIconMarkup(displayName);
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
                result += $", {T("Status")}: {conditions}";
            }

            return result;
        }
    }
}
