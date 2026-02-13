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
    // Shared state across battle-result patch classes
    internal static class BattleResultState
    {
        // True only while EXP counter sound is actually playing.
        internal static bool ExpCounterPlaying;

        /// <summary>
        /// Stops the EXP counter sound if it is currently playing.
        /// Safe to call from any phase-init postfix; the flag ensures it only fires once.
        /// </summary>
        internal static void StopExpCounterIfPlaying()
        {
            if (!ExpCounterPlaying) return;
            ExpCounterPlaying = false;
            SoundPlayer.StopExpCounter();
            MelonLogger.Msg("[BattleResult] EXP counter stopped");
        }
    }

    // ----------------------------------------------------------------
    //  Screen 1: EXP / Gil / ABP totals  (always fires)
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

                // Gather totals
                int totalExp = data.GetExp;
                int totalAbp = data.GetAbp;
                int totalGil = data.GetGil;

                // Build totals-only announcement
                var parts = new List<string>();
                if (totalExp > 0)
                    parts.Add($"{totalExp:N0} EXP");
                if (totalAbp > 0)
                    parts.Add($"{totalAbp} ABP");
                if (totalGil > 0)
                    parts.Add($"{totalGil:N0} Gil");

                // Per-character level-up / job-level-up notes (still announced in speech)
                var charList = data.CharacterList;
                var pointsDataList = new List<BattleResultDataStore.CharacterPointsData>();

                if (charList != null)
                {
                    foreach (var c in charList)
                    {
                        if (c?.AfterData == null) continue;

                        string name = c.AfterData.Name;

                        // Get next EXP to level (0 = max level)
                        int nextExp = 0;
                        try { nextExp = c.AfterData.GetNextExp(); }
                        catch { /* max level or unavailable */ }

                        // Get ABP remaining to next job level (0 = mastered/no job)
                        int abpToNext = 0;
                        try
                        {
                            var ownedJob = c.BeforData.OwnedJob;
                            if (ownedJob != null)
                            {
                                abpToNext = ExpUtility.GetNextExp(
                                    ownedJob.Id, ownedJob.CurrentProficiency, Il2CppLast.Defaine.Master.ExpTableType.JobExp);
                            }
                        }
                        catch { /* no job / freelancer / mastered */ }

                        // Store per-character data for navigator
                        pointsDataList.Add(new BattleResultDataStore.CharacterPointsData
                        {
                            Name = name,
                            Exp = c.GetExp,
                            Abp = abpToNext,
                            NextExp = nextExp,
                            IsLevelUp = c.IsLevelUp,
                            NewLevel = c.AfterData.parameter?.ConfirmedLevel() ?? 0,
                            IsJobLevelUp = c.IsJobLevelUp
                        });

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

                // Store data for navigator
                BattleResultDataStore.SetPointsData(pointsDataList, totalExp, totalAbp, totalGil);

                if (parts.Count == 0) return;

                string announcement = string.Join(", ", parts);
                MelonLogger.Msg($"[BattleResult] Points: {announcement}");

                if (AnnouncementDeduplicator.ShouldAnnounce(
                        AnnouncementContexts.BATTLE_RESULT_POINTS, announcement))
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);

                // Start EXP counter sound if enabled
                if (FFV_ScreenReaderMod.ExpCounterEnabled && totalExp > 0)
                {
                    SoundPlayer.PlayExpCounter();
                    BattleResultState.ExpCounterPlaying = true;

                    // Launch coroutine to stop counter when counting animation finishes
                    CoroutineManager.StartUntracked(MonitorExpCounterAnimation(__instance.Pointer));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowPointsInit patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls the unsafe pointer chain from ResultMenuController to detect when
        /// the EXP counting animation finishes, then stops the counter sound.
        /// Chain: instance → +0x20 (pointController) → +0x30 (characterListController)
        ///   → +0x20 (contentList, count at +0x18)
        ///   → +0x30 (perormanceEndCount)
        /// Animation done when: perormanceEndCount >= contentList.Count &amp;&amp; Count > 0
        /// </summary>
        private static IEnumerator MonitorExpCounterAnimation(IntPtr instancePtr)
        {
            var wait = new WaitForSeconds(0.1f);
            bool loggedOnce = false;

            // Navigate pointer chain once with diagnostic logging
            if (instancePtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: instancePtr is null");
                yield break;
            }

            IntPtr pointControllerPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(instancePtr, 0x20);
            if (pointControllerPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: pointController is null");
                yield break;
            }

            IntPtr charListCtrlPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(pointControllerPtr, 0x30);
            if (charListCtrlPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: characterListController is null");
                yield break;
            }

            IntPtr contentListPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(charListCtrlPtr, 0x20);
            if (contentListPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: contentList is null");
                yield break;
            }

            // contentList.Count (List._size) at contentListPtr + 0x18
            int contentCount = System.Runtime.InteropServices.Marshal.ReadInt32(contentListPtr, 0x18);
            if (contentCount <= 0)
            {
                MelonLogger.Warning($"[BattleResult] MonitorExp: contentCount={contentCount}, aborting");
                yield break;
            }

            MelonLogger.Msg($"[BattleResult] MonitorExp: chain OK. charListCtrl=0x{charListCtrlPtr:X}, contentCount={contentCount}");

            // Poll until animation finishes or counter was already stopped by a safety net
            while (BattleResultState.ExpCounterPlaying)
            {
                yield return wait;

                try
                {
                    int endCount = System.Runtime.InteropServices.Marshal.ReadInt32(charListCtrlPtr, 0x30);

                    if (!loggedOnce)
                    {
                        MelonLogger.Msg($"[BattleResult] MonitorExp: first poll endCount={endCount}/{contentCount}");
                        loggedOnce = true;
                    }

                    if (endCount >= contentCount)
                    {
                        MelonLogger.Msg($"[BattleResult] MonitorExp: animation done (endCount={endCount} >= contentCount={contentCount})");
                        BattleResultState.StopExpCounterIfPlaying();
                        yield break;
                    }
                }
                catch
                {
                    // Pointer became invalid — bail out silently, safety nets will handle it
                    yield break;
                }
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
            BattleResultState.StopExpCounterIfPlaying();
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
                BattleResultState.StopExpCounterIfPlaying();
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
                BattleResultState.StopExpCounterIfPlaying();
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

    // ----------------------------------------------------------------
    //  EndWaitInit: results dismissed, clear stored data
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.EndWaitInit))]
    public static class ResultMenuController_EndWaitInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Close navigator if open
                if (BattleResultNavigator.IsOpen)
                    BattleResultNavigator.Close();

                // Stop counter sound just in case
                BattleResultState.StopExpCounterIfPlaying();

                // Clear stored data
                BattleResultDataStore.Clear();
                MelonLogger.Msg("[BattleResult] EndWaitInit fired, data cleared");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EndWaitInit patch: {ex.Message}");
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
        /// __0 is the BattleResultCharacterData parameter.
        /// </summary>
        public static void SetData_Postfix(object __instance, object __0)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ResultStatusUpController.SetData fired");

                // Try to extract stat diffs from the data parameter
                BattleResultDataStore.CharacterStatData statData = null;
                try
                {
                    statData = ExtractStatDiffs(__0);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BattleResult] SetData: could not extract stat diffs: {ex.Message}");
                }

                // Obtain the MonoBehaviour's Transform so we can read UI text
                Transform root = GetTransformFromInstance(__instance);
                if (root == null)
                {
                    MelonLogger.Warning("[BattleResult] SetData: could not get Transform from __instance");
                    // Even without UI, try to announce from data if available
                    if (statData != null)
                    {
                        BattleResultDataStore.AddStatData(statData);
                        AnnounceFromStatData(statData);
                    }
                    return;
                }

                CoroutineManager.StartUntracked(AnnounceStatusUpCoroutine(root, statData));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetData postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract HP/MP diffs from the BattleResultCharacterData parameter.
        /// </summary>
        private static BattleResultDataStore.CharacterStatData ExtractStatDiffs(object charDataObj)
        {
            if (charDataObj == null) return null;

            // Cast to BattleResultCharacterData
            var charData = charDataObj as BattleResultData.BattleResultCharacterData;
            if (charData == null) return null;

            var beforeChar = charData.BeforData;
            var afterChar = charData.AfterData;
            if (beforeChar?.parameter == null || afterChar?.parameter == null) return null;

            string charName = afterChar.Name;
            var result = new BattleResultDataStore.CharacterStatData { Name = charName };

            // Compare HP
            int beforeHp = beforeChar.parameter.ConfirmedMaxHp();
            int afterHp = afterChar.parameter.ConfirmedMaxHp();
            int diffHp = afterHp - beforeHp;
            result.Stats.Add(new BattleResultDataStore.StatChange
            {
                Category = "HP",
                Before = beforeHp.ToString(),
                After = afterHp.ToString(),
                Diff = diffHp
            });

            // Compare MP
            int beforeMp = beforeChar.parameter.ConfirmedMaxMp();
            int afterMp = afterChar.parameter.ConfirmedMaxMp();
            int diffMp = afterMp - beforeMp;
            result.Stats.Add(new BattleResultDataStore.StatChange
            {
                Category = "MP",
                Before = beforeMp.ToString(),
                After = afterMp.ToString(),
                Diff = diffMp
            });

            MelonLogger.Msg($"[BattleResult] StatDiffs for {charName}: HP {beforeHp}->{afterHp} (+{diffHp}), MP {beforeMp}->{afterMp} (+{diffMp})");

            return result;
        }

        /// <summary>
        /// Announces stat data directly (fallback when UI transform is not available).
        /// </summary>
        private static void AnnounceFromStatData(BattleResultDataStore.CharacterStatData statData)
        {
            var parts = new List<string>();
            foreach (var stat in statData.Stats)
            {
                if (stat.Diff > 0)
                    parts.Add($"{stat.Category} +{stat.Diff}");
            }

            if (parts.Count == 0) return;

            string announcement = $"{statData.Name}: {string.Join(", ", parts)}";
            MelonLogger.Msg($"[BattleResult] StatusUp (from data): {announcement}");

            if (AnnouncementDeduplicator.ShouldAnnounce(
                    AnnouncementContexts.BATTLE_RESULT_LEVELUP, announcement))
                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
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
        /// Also stores stat data for the navigator.
        /// </summary>
        private static IEnumerator AnnounceStatusUpCoroutine(Transform root, BattleResultDataStore.CharacterStatData statData)
        {
            yield return null; // let SetData finish populating the view

            // Store stat data for navigator (even if UI read fails below)
            if (statData != null)
                BattleResultDataStore.AddStatData(statData);

            // Collect every visible text value under the controller
            var allTexts = new List<string>();
            ForEachTextInChildren(root, t =>
            {
                string v = GetTextSafe(t);
                if (!string.IsNullOrEmpty(v))
                    allTexts.Add(v);
            }, includeInactive: false);

            if (allTexts.Count == 0)
            {
                // Fall back to data-based announcement
                if (statData != null)
                    AnnounceFromStatData(statData);
                yield break;
            }

            // Build a structured announcement.
            // Expected order from depth-first traversal:
            //   [0] = character name (from ResultStatusUpView.nameText)
            //   then groups of (category, beforeValue, afterValue)
            string charName = allTexts[0];

            // Prefer data-based diff format "+N" over UI "before to after"
            if (statData != null && statData.Stats.Count > 0)
            {
                var statParts = new List<string>();
                foreach (var stat in statData.Stats)
                {
                    if (stat.Diff > 0)
                        statParts.Add($"{stat.Category} +{stat.Diff}");
                }

                if (statParts.Count > 0)
                {
                    string announcement = $"{charName}: {string.Join(", ", statParts)}";
                    MelonLogger.Msg($"[BattleResult] StatusUp: {announcement}");

                    if (AnnouncementDeduplicator.ShouldAnnounce(
                            AnnouncementContexts.BATTLE_RESULT_LEVELUP, announcement))
                        FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                    yield break;
                }
            }

            // Fallback: use UI text with "before to after" format
            string to = LocalizationHelper.GetModString("to");

            var uiStatParts = new List<string>();
            int i = 1;
            while (i < allTexts.Count)
            {
                // Try to detect a (category, before, after) triple
                if (i + 2 < allTexts.Count && IsNumeric(allTexts[i + 1]) && IsNumeric(allTexts[i + 2]))
                {
                    uiStatParts.Add($"{allTexts[i]} {allTexts[i + 1]} {to} {allTexts[i + 2]}");
                    i += 3;
                }
                else
                {
                    // Unknown text — include as-is
                    uiStatParts.Add(allTexts[i]);
                    i++;
                }
            }

            string fallbackAnnouncement;
            if (uiStatParts.Count > 0)
                fallbackAnnouncement = $"{charName}: {string.Join(", ", uiStatParts)}";
            else
                fallbackAnnouncement = charName;

            MelonLogger.Msg($"[BattleResult] StatusUp: {fallbackAnnouncement}");

            if (AnnouncementDeduplicator.ShouldAnnounce(
                    AnnouncementContexts.BATTLE_RESULT_LEVELUP, fallbackAnnouncement))
                FFV_ScreenReaderMod.SpeakText(fallbackAnnouncement, interrupt: false);
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
