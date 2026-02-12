using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
using Il2CppLast.Data.Master;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using Il2CppLast.Systems;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using UnityEngine;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Patches
{
    // ----------------------------------------------------------------
    //  Screen 1: EXP / Gil / ABP summary  (always fires)
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowPointsInit))]
    public static class ResultMenuController_ShowPointsInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ShowPointsInit fired");

                var data = __instance.targetData;
                if (data == null) return;

                // Reset all result-phase dedup for the new result sequence
                AnnouncementDeduplicator.Reset(
                    AnnouncementContexts.BATTLE_RESULT_POINTS,
                    AnnouncementContexts.BATTLE_RESULT_LEVELUP,
                    AnnouncementContexts.BATTLE_RESULT_ABILITIES,
                    AnnouncementContexts.BATTLE_RESULT_ITEMS);

                var parts = new List<string>();

                // Gil
                int gil = data.GetGil;
                if (gil > 0)
                    parts.Add($"{gil:N0} Gil");

                // Per-character XP / ABP / brief level / job-level notes
                var charList = data.CharacterList;
                if (charList != null)
                {
                    string earned = LocalizationHelper.GetModString("earned");

                    foreach (var c in charList)
                    {
                        if (c?.AfterData == null) continue;

                        string name = c.AfterData.Name;
                        int exp  = c.GetExp;
                        int abp  = c.GetABP;

                        if (abp > 0)
                            parts.Add($"{name} {earned} {exp:N0} XP, {abp} A B P");
                        else
                            parts.Add($"{name} {earned} {exp:N0} XP");

                        if (c.IsLevelUp)
                        {
                            int lv = c.AfterData.parameter?.ConfirmedLevel() ?? 0;
                            if (lv > 0)
                                parts.Add($"{name}: {LocalizationHelper.GetModString("level")} {lv}!");
                        }
                        if (c.IsJobLevelUp)
                            parts.Add($"{name}: Job level up!");
                    }
                }

                if (parts.Count == 0) return;

                string announcement = string.Join(", ", parts);
                if (AnnouncementDeduplicator.ShouldAnnounce(
                        AnnouncementContexts.BATTLE_RESULT_POINTS, announcement))
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowPointsInit patch: {ex.Message}");
            }
        }
    }

    // ----------------------------------------------------------------
    //  Phase logging: ShowStatusUpInit  (per-character detail is in
    //  the manual SetData patch below)
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowStatusUpInit))]
    public static class ResultMenuController_ShowStatusUpInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            MelonLogger.Msg("[BattleResult] ShowStatusUpInit fired");
        }
    }

    // ----------------------------------------------------------------
    //  Abilities learned (from skill points)
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowGetAbilitysInit))]
    public static class ResultMenuController_ShowGetAbilitysInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ShowGetAbilitysInit fired");
                var skillCtrl = __instance.skillController;
                if (skillCtrl == null) return;
                CoroutineManager.StartUntracked(
                    AnnounceFromTransformCoroutine(skillCtrl.transform,
                        AnnouncementContexts.BATTLE_RESULT_ABILITIES,
                        "ShowGetAbilitys"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowGetAbilitysInit patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Shared coroutine: waits 1 frame, reads all active Text
        /// components under <paramref name="root"/>, announces once.
        /// </summary>
        internal static IEnumerator AnnounceFromTransformCoroutine(
            Transform root, string dedupContext, string logTag)
        {
            yield return null; // let UI populate

            var texts = new List<string>();
            ForEachTextInChildren(root, t =>
            {
                string v = GetTextSafe(t);
                if (!string.IsNullOrEmpty(v))
                    texts.Add(v);
            }, includeInactive: false);

            if (texts.Count == 0) yield break;

            string announcement = string.Join(", ", texts);
            MelonLogger.Msg($"[BattleResult] {logTag}: {announcement}");

            if (AnnouncementDeduplicator.ShouldAnnounce(dedupContext, announcement))
                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }
    }

    // ----------------------------------------------------------------
    //  Level-up abilities
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowLevelUpAbilitysInit))]
    public static class ResultMenuController_ShowLevelUpAbilitysInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ShowLevelUpAbilitysInit fired");
                var skillCtrl = __instance.skillController;
                if (skillCtrl == null) return;
                CoroutineManager.StartUntracked(
                    ResultMenuController_ShowGetAbilitysInit_Patch.AnnounceFromTransformCoroutine(
                        skillCtrl.transform,
                        AnnouncementContexts.BATTLE_RESULT_ABILITIES,
                        "ShowLevelUpAbilitys"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowLevelUpAbilitysInit patch: {ex.Message}");
            }
        }
    }

    // ----------------------------------------------------------------
    //  Item drops
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowGetItemsInit))]
    public static class ResultMenuController_ShowGetItemsInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ShowGetItemsInit fired");

                var data = __instance.targetData;
                if (data?.ItemList == null || data.ItemList.Count == 0) return;

                var mm = MessageManager.Instance;
                if (mm == null) return;

                var itemContentList = ListItemFormatter.GetContentDataList(data.ItemList, mm);
                if (itemContentList == null || itemContentList.Count == 0) return;

                var parts = new List<string>();
                foreach (var item in itemContentList)
                {
                    if (item == null) continue;
                    string name = StripIconMarkup(item.Name);
                    if (string.IsNullOrEmpty(name)) continue;
                    parts.Add(item.Count > 1 ? $"{name} x{item.Count}" : name);
                }

                if (parts.Count == 0) return;

                string received = LocalizationHelper.GetModString("received");
                string announcement = $"{received}: {string.Join(", ", parts)}";
                MelonLogger.Msg($"[BattleResult] Items: {announcement}");

                if (AnnouncementDeduplicator.ShouldAnnounce(
                        AnnouncementContexts.BATTLE_RESULT_ITEMS, announcement))
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowGetItemsInit patch: {ex.Message}");
            }
        }
    }

    // ================================================================
    //  Manual patches (types in non-standard IL2CPP namespaces)
    // ================================================================
    public static class BattleResultManualPatches
    {
        /// <summary>
        /// Apply manual Harmony patches for result types that live
        /// outside the standard Last.UI.KeyInput namespace.
        /// Call from FFV_ScreenReaderMod.OnInitializeMelon().
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                PatchStatusUpSetData(harmony);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BattleResult] Error applying manual patches: {ex.Message}");
            }
        }

        // ---------- helpers ----------

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                        if (t.FullName == fullName) return t;
                }
                catch { /* assembly may throw on GetTypes() */ }
            }
            return null;
        }

        // ---------- ResultStatusUpController.SetData ----------

        private static void PatchStatusUpSetData(HarmonyLib.Harmony harmony)
        {
            // The type lives in Serial.FF5.UI.Touch — try IL2CPP-prefixed name first
            string[] candidates =
            {
                "Il2CppSerial.FF5.UI.Touch.ResultStatusUpController",
                "Serial.FF5.UI.Touch.ResultStatusUpController",
            };

            Type controllerType = null;
            foreach (var name in candidates)
            {
                controllerType = FindType(name);
                if (controllerType != null)
                {
                    MelonLogger.Msg($"[BattleResult] Found ResultStatusUpController: {name}");
                    break;
                }
            }

            if (controllerType == null)
            {
                MelonLogger.Warning(
                    "[BattleResult] Could not find ResultStatusUpController type. " +
                    "Per-character level-up detail will not be announced.");
                return;
            }

            var setDataMethod = AccessTools.Method(controllerType, "SetData");
            if (setDataMethod == null)
            {
                MelonLogger.Warning("[BattleResult] SetData method not found on ResultStatusUpController");
                return;
            }

            var postfix = typeof(BattleResultManualPatches).GetMethod(
                nameof(SetData_Postfix), BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(setDataMethod, postfix: new HarmonyMethod(postfix));
            MelonLogger.Msg("[BattleResult] Patched ResultStatusUpController.SetData");
        }

        /// <summary>
        /// Postfix for ResultStatusUpController.SetData —
        /// fires once per character who leveled up.
        /// </summary>
        public static void SetData_Postfix(object __instance)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ResultStatusUpController.SetData fired");

                // Obtain the MonoBehaviour's Transform so we can read UI text
                Transform root = GetTransformFromInstance(__instance);
                if (root == null)
                {
                    MelonLogger.Warning("[BattleResult] SetData: could not get Transform from __instance");
                    return;
                }

                CoroutineManager.StartUntracked(AnnounceStatusUpCoroutine(root));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetData postfix: {ex.Message}");
            }
        }

        private static Transform GetTransformFromInstance(object instance)
        {
            // ResultStatusUpController extends MonoBehaviour extends Component
            if (instance is Component comp)
                return comp.transform;

            return null;
        }

        /// <summary>
        /// Waits 1 frame, then reads the status-up screen text and
        /// announces per-character stat changes.
        /// </summary>
        private static IEnumerator AnnounceStatusUpCoroutine(Transform root)
        {
            yield return null; // let SetData finish populating the view

            // Collect every visible text value under the controller
            var allTexts = new List<string>();
            ForEachTextInChildren(root, t =>
            {
                string v = GetTextSafe(t);
                if (!string.IsNullOrEmpty(v))
                    allTexts.Add(v);
            }, includeInactive: false);

            if (allTexts.Count == 0) yield break;

            // Build a structured announcement.
            // Expected order from depth-first traversal:
            //   [0] = character name (from ResultStatusUpView.nameText)
            //   then groups of (category, beforeValue, afterValue)
            string charName = allTexts[0];
            string to = LocalizationHelper.GetModString("to");

            var statParts = new List<string>();
            int i = 1;
            while (i < allTexts.Count)
            {
                // Try to detect a (category, before, after) triple
                if (i + 2 < allTexts.Count && IsNumeric(allTexts[i + 1]) && IsNumeric(allTexts[i + 2]))
                {
                    statParts.Add($"{allTexts[i]} {allTexts[i + 1]} {to} {allTexts[i + 2]}");
                    i += 3;
                }
                else
                {
                    // Unknown text — include as-is
                    statParts.Add(allTexts[i]);
                    i++;
                }
            }

            string announcement;
            if (statParts.Count > 0)
                announcement = $"{charName}: {string.Join(", ", statParts)}";
            else
                announcement = charName;

            MelonLogger.Msg($"[BattleResult] StatusUp: {announcement}");

            if (AnnouncementDeduplicator.ShouldAnnounce(
                    AnnouncementContexts.BATTLE_RESULT_LEVELUP, announcement))
                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }

        /// <summary>
        /// Returns true when the string looks like a number
        /// (digits, commas, periods, spaces allowed for locale formatting).
        /// </summary>
        private static bool IsNumeric(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char ch in text)
            {
                if (!char.IsDigit(ch) && ch != ',' && ch != '.' && ch != ' ' && ch != '-')
                    return false;
            }
            return true;
        }
    }
}
