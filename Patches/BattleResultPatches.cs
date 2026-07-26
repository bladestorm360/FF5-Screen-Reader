using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Patches
{
    // Shared state across battle-result patch classes
    internal static class BattleResultState
    {
        // True only while EXP counter sound is actually playing.
        internal static bool ExpCounterPlaying;

        // Per-result-sequence one-shots. Each phase's *Init can fire more than once while its
        // screen is up, so the phase announces the first time and stays quiet afterwards.
        // All cleared by ShowPointsInit, which always fires first in a result sequence.
        internal static bool PointsAnnounced;
        internal static bool ItemsAnnounced;

        // Ability text already read this sequence. ShowGetAbilitysInit and ShowLevelUpAbilitysInit
        // read the SAME skillController transform, so the level-up phase would otherwise re-read
        // whatever the skill-point phase already announced. Keyed on the text so the level-up
        // phase announces only what is genuinely new.
        internal static readonly HashSet<string> AnnouncedAbilityTexts = new HashSet<string>();

        /// <summary>
        /// Clears every per-sequence announcement guard. Called from ShowPointsInit.
        /// </summary>
        internal static void ResetSequence()
        {
            PointsAnnounced = false;
            ItemsAnnounced = false;
            AnnouncedAbilityTexts.Clear();
        }

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
    //  Page 1: EXP / Gil / ABP totals  (always fires)
    //
    //  Totals only. Level-ups belong to the pages that actually show them:
    //  character level up on the status-up page (ResultStatusUpController.SetData),
    //  job level up on the job-proficiency page (ShowLevelUpAbilitysInit).
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

                // New result sequence — re-arm every phase one-shot and drop the old pages
                BattleResultState.ResetSequence();
                BattleResultDataStore.Clear();

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

                BuildPointsPage(data);

                if (parts.Count == 0) return;

                string announcement = string.Join(", ", parts);
                MelonLogger.Msg($"[BattleResult] Points: {announcement}");

                if (!BattleResultState.PointsAnnounced)
                {
                    BattleResultState.PointsAnnounced = true;
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }

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
        /// Builds the navigator page for this screen: characters x {EXP, Next, ABP}, game order.
        ///
        /// ABP-to-next only exists for a character in an unmastered job. A Freelancer has no
        /// job to level and a mastered job has no next level, so both yield 0 and the game
        /// shows nothing there. Drop the column outright when it applies to nobody, rather
        /// than reading "- ABP" for every character in an all-Freelancer party.
        /// </summary>
        private static void BuildPointsPage(BattleResultData data)
        {
            var charList = data.CharacterList;
            if (charList == null || charList.Count == 0) return;

            var names = new List<string>();
            var exps = new List<int>();
            var nextExps = new List<int>();
            var abps = new List<int>();

            foreach (var c in charList)
            {
                if (c?.AfterData == null) continue;

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

                names.Add(c.AfterData.Name);
                exps.Add(c.GetExp);
                nextExps.Add(nextExp);
                abps.Add(abpToNext);
            }

            if (names.Count == 0) return;

            bool anyAbp = false;
            for (int i = 0; i < abps.Count; i++)
            {
                if (abps[i] > 0) { anyAbp = true; break; }
            }

            string[] colHeaders = anyAbp
                ? new[] { "EXP", "Next", "ABP" }
                : new[] { "EXP", "Next" };

            var cells = new string[names.Count, colHeaders.Length];
            for (int i = 0; i < names.Count; i++)
            {
                cells[i, 0] = exps[i].ToString("N0");
                cells[i, 1] = nextExps[i] > 0 ? nextExps[i].ToString("N0") : "-";

                // Mixed party: the column exists because someone has a job, but this character
                // may still have no value of their own.
                if (anyAbp)
                    cells[i, 2] = abps[i] > 0 ? abps[i].ToString() : "-";
            }

            BattleResultDataStore.AddPage(
                LocalizationHelper.GetModString("battle_results"), names.ToArray(), colHeaders, cells);
        }

        /// <summary>
        /// Polls the unsafe pointer chain from ResultMenuController to detect when
        /// the EXP counting animation finishes, then stops the counter sound.
        /// Chain: instance -> +0x20 (pointController) -> +0x30 (characterListController)
        ///   -> +0x20 (contentList, count at +0x18)
        ///   -> +0x30 (perormanceEndCount)
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

            IntPtr pointControllerPtr = Marshal.ReadIntPtr(instancePtr, 0x20);
            if (pointControllerPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: pointController is null");
                yield break;
            }

            IntPtr charListCtrlPtr = Marshal.ReadIntPtr(pointControllerPtr, 0x30);
            if (charListCtrlPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: characterListController is null");
                yield break;
            }

            IntPtr contentListPtr = Marshal.ReadIntPtr(charListCtrlPtr, 0x20);
            if (contentListPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[BattleResult] MonitorExp: contentList is null");
                yield break;
            }

            // contentList.Count (List._size) at contentListPtr + 0x18
            int contentCount = Marshal.ReadInt32(contentListPtr, 0x18);
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

                // Keep the SDL Counter stream fed so the loop never drains between ticks.
                SoundPlayer.TopUpExpCounter();

                try
                {
                    int endCount = Marshal.ReadInt32(charListCtrlPtr, 0x30);

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
                    // Pointer became invalid -- bail out silently, safety nets will handle it
                    yield break;
                }
            }
        }
    }

    // ----------------------------------------------------------------
    //  Phase logging: ShowStatusUpInit  (per-character detail is in
    //  the ResultStatusUpController.SetData patch below)
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
    //  Page 2: the level-up screen, one character at a time.
    //
    //  ResultPointController.StatusUpInit builds statusupList and StatusUpAction advances
    //  it as the player presses A, calling SetData once per character page.
    //
    //  The live type is the KeyInput variant. Serial.FF5.UI.Touch.ResultStatusUpController
    //  exists but is an empty stub with no SetData at all (dump.cs:284064) -- an earlier
    //  reflection-based patch targeted it and therefore never fired, which is why this
    //  screen was silent.
    // ----------------------------------------------------------------
    [HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController),
                  nameof(Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController.SetData))]
    public static class ResultStatusUpController_SetData_Patch
    {
        // One-shot: the row layout is identical every time, so dump it once per session.
        private static bool loggedRowDump;

        /// <summary>One row of the level-up panel, as displayed.</summary>
        private class StatRow
        {
            public string Category;   // "Lv." / "HP" / "MP" / job name
            public string Before;
            public string After;
            public string Extra;      // the game's own "Master" text, when shown
        }

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController __instance,
                                   BattleResultData.BattleResultCharacterData __0)
        {
            try
            {
                MelonLogger.Msg("[BattleResult] ResultStatusUpController.SetData fired");
                CoroutineManager.StartUntracked(AnnounceCoroutine(__instance, __0));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultStatusUpController.SetData patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Waits one frame so SetData's active/inactive changes are applied, then reads the
        /// panel's real row objects (not a flat text scrape) and announces the character's
        /// changes in on-screen order.
        /// </summary>
        private static IEnumerator AnnounceCoroutine(
            Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController controller,
            BattleResultData.BattleResultCharacterData charData)
        {
            yield return null;

            string announcement = null;
            try
            {
                string name = charData?.AfterData?.Name;
                if (string.IsNullOrEmpty(name))
                    name = GetTextSafe(controller?.view?.nameText);
                if (string.IsNullOrEmpty(name)) name = "";

                var rows = ReadRows(controller);
                if (rows.Count == 0)
                    rows = BuildFallbackRows(charData);
                if (rows.Count == 0) yield break;

                // Headline reflects why this page is up
                string headline;
                if (charData != null && charData.IsLevelUp)
                    headline = string.Format(T("{0}: Level up!"), name);
                else if (charData != null && charData.IsJobLevelUp)
                    headline = string.Format(T("{0}: Job level up!"), name);
                else
                    headline = $"{name}:";

                var parts = new List<string>();
                foreach (var row in rows)
                    parts.Add(FormatRow(row));

                announcement = $"{headline} {string.Join(", ", parts)}";
                AddStatsPage(name, rows);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BattleResult] StatusUp announce failed: {ex.Message}");
                yield break;
            }

            if (string.IsNullOrEmpty(announcement)) yield break;

            MelonLogger.Msg($"[BattleResult] StatusUp: {announcement}");
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }

        /// <summary>
        /// Reads the panel's ResultStatusContentController rows. Each row knows its own
        /// ParameterType (Level / HP / MP / JobLevel) and carries its own category label,
        /// so the labels come out localized without a lookup table.
        /// </summary>
        private static List<StatRow> ReadRows(
            Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController controller)
        {
            var rows = new List<StatRow>();
            var contentList = controller?.contentList;
            if (contentList == null) return rows;

            bool dump = !loggedRowDump;

            for (int i = 0; i < contentList.Count; i++)
            {
                var ctrl = contentList[i];
                if (ctrl == null) continue;

                var view = ctrl.view;
                if (view == null) continue;

                bool active = ctrl.gameObject != null && ctrl.gameObject.activeInHierarchy;

                string category = ActiveText(view.categoryText);
                string before = ActiveText(view.beforValueText);
                string after = ActiveText(view.afterValueText);
                string jobLevel = ActiveText(view.jobLevelText);
                string master = ActiveText(view.upperJobMasterText) ?? ActiveText(view.lowerJobMasterText);
                bool arrow = view.arrowImage != null
                             && view.arrowImage.gameObject != null
                             && view.arrowImage.gameObject.activeInHierarchy;

                if (dump)
                {
                    MelonLogger.Msg(
                        $"[BattleResult] StatusUp row {i}: type={ctrl.Type} active={active} " +
                        $"cat='{category}' before='{before}' after='{after}' " +
                        $"jobLv='{jobLevel}' master='{master}' arrow={arrow}");
                }

                if (!active) continue;
                if (category == null && before == null && after == null
                    && jobLevel == null && master == null) continue;

                // The job row has no before/after when the job has no level to show
                // (the game calls DisplayOnlyCategoryText for that case).
                if (after == null && before == null && jobLevel != null)
                    after = jobLevel;

                rows.Add(new StatRow
                {
                    Category = category,
                    Before = before,
                    After = after,
                    Extra = master
                });
            }

            if (dump) loggedRowDump = true;

            return rows;
        }

        /// <summary>
        /// Data-driven rows, used only when the panel reads back empty.
        /// Uses the Confirmed* accessors; OwnedJob.Level is deliberately avoided because it
        /// reports wrong values for level-0 jobs (see docs/plan.md), which is exactly why the
        /// job row is read from the UI instead.
        /// </summary>
        private static List<StatRow> BuildFallbackRows(BattleResultData.BattleResultCharacterData data)
        {
            var rows = new List<StatRow>();

            var before = data?.BeforData?.parameter;
            var after = data?.AfterData?.parameter;
            if (before == null || after == null) return rows;

            AddIfChanged(rows, LocalizationHelper.GetModString("level"),
                before.ConfirmedLevel(), after.ConfirmedLevel());
            AddIfChanged(rows, "HP", before.ConfirmedMaxHp(), after.ConfirmedMaxHp());
            AddIfChanged(rows, "MP", before.ConfirmedMaxMp(), after.ConfirmedMaxMp());

            MelonLogger.Msg($"[BattleResult] StatusUp: panel read empty, using {rows.Count} data rows");
            return rows;
        }

        private static void AddIfChanged(List<StatRow> rows, string category, int before, int after)
        {
            if (before == after) return;
            rows.Add(new StatRow
            {
                Category = category,
                Before = before.ToString(),
                After = after.ToString()
            });
        }

        /// <summary>
        /// Separator between a before and after value. The game draws this row as
        /// "HP  44 ↗ 53", so speech mirrors that shape rather than spelling out a word.
        /// Both the announcement and the navigator's row summaries go through
        /// <see cref="Transition"/>, so changing this changes them together.
        /// </summary>
        private const string ChangeArrow = ">";

        private static string Transition(string before, string after)
            => $"{before} {ChangeArrow} {after}";

        private static string FormatRow(StatRow row)
        {
            string text;
            if (row.Before != null && row.After != null && row.Before != row.After)
                text = Join(row.Category, Transition(row.Before, row.After));
            else if (row.After != null)
                text = Join(row.Category, row.After);
            else
                text = row.Category ?? "";

            if (row.Extra != null)
                text = Join(text, row.Extra);

            return text;
        }

        private static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            return $"{a} {b}";
        }

        /// <summary>
        /// Adds this character's page to the navigator: rows x {Before, After, Change}.
        /// Change is "-" for rows with no numeric delta, such as the job row.
        ///
        /// Each row also carries a compact summary — "HP: 44 &gt; 53 (9)" — so Up/Down reads
        /// the row the way the game lays it out instead of naming all three columns. The
        /// columns themselves stay browsable with Left/Right, where naming them is useful.
        /// </summary>
        private static void AddStatsPage(string name, List<StatRow> rows)
        {
            var rowHeaders = new string[rows.Count];
            var summaries = new string[rows.Count];
            var cells = new string[rows.Count, 3];

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                string label = Join(row.Category, row.Extra);
                rowHeaders[i] = label;
                cells[i, 0] = row.Before ?? "-";
                cells[i, 1] = row.After ?? "-";

                if (int.TryParse(row.Before, out int b) && int.TryParse(row.After, out int a))
                {
                    int diff = a - b;

                    // The cell keeps an explicit sign because it can be read on its own,
                    // where a bare "9" would be ambiguous. The summary uses the bare form.
                    cells[i, 2] = diff > 0 ? $"+{diff}" : diff.ToString();
                    summaries[i] = $"{label}: {Transition(row.Before, row.After)} ({diff})";
                }
                else
                {
                    cells[i, 2] = "-";
                    summaries[i] = label;
                }
            }

            BattleResultDataStore.AddPage(
                name,
                rowHeaders,
                new[]
                {
                    LocalizationHelper.GetModString("before"),
                    LocalizationHelper.GetModString("after"),
                    LocalizationHelper.GetModString("change")
                },
                cells,
                summaries);
        }

        /// <summary>
        /// Returns a Text's content only when it is actually on screen. The panel hides rows
        /// and individual values rather than blanking them, so an inactive object still holds
        /// stale text from the previous character.
        /// </summary>
        private static string ActiveText(UnityEngine.UI.Text text)
        {
            if (text == null) return null;
            if (text.gameObject == null || !text.gameObject.activeInHierarchy) return null;
            return GetTextSafe(text);
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
                    AnnounceFromTransformCoroutine(skillCtrl.transform, "ShowGetAbilitys"));
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
            Transform root, string logTag)
        {
            yield return null; // let UI populate

            // Both ability phases read this same transform. Keep only the lines that have not
            // already been announced this sequence, so the level-up phase does not repeat the
            // skill-point phase's list when a battle produces both.
            var texts = new List<string>();
            var allTexts = new List<string>();
            ForEachTextInChildren(root, t =>
            {
                string v = GetTextSafe(t);
                if (string.IsNullOrEmpty(v)) return;
                allTexts.Add(v);
                if (BattleResultState.AnnouncedAbilityTexts.Add(v))
                    texts.Add(v);
            }, includeInactive: false);

            // Diagnostic: the job-proficiency page also arrives through ShowLevelUpAbilitysInit,
            // and its exact content is not yet confirmed. Log everything the scrape sees --
            // including lines the filter above drops -- so one battle with jobs unlocked is
            // enough to decide whether a typed SetJobProficiencyData hook is needed.
            MelonLogger.Msg($"[BattleResult] {logTag} raw texts ({allTexts.Count}): {string.Join(" | ", allTexts)}");

            if (texts.Count == 0) yield break;

            string announcement = string.Join(", ", texts);
            MelonLogger.Msg($"[BattleResult] {logTag}: {announcement}");

            BattleResultDataStore.AddListPage(LocalizationHelper.GetModString("learned"), texts);
            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
        }
    }

    // ----------------------------------------------------------------
    //  Level-up abilities / job proficiency level up
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
                        skillCtrl.transform, "ShowLevelUpAbilitys"));
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

                // Flat page -- the count is already folded into each line, so there is no
                // second axis worth arrowing across.
                BattleResultDataStore.AddListPage(received, parts);

                string announcement = $"{received}: {string.Join(", ", parts)}";
                MelonLogger.Msg($"[BattleResult] Items: {announcement}");

                if (!BattleResultState.ItemsAnnounced)
                {
                    BattleResultState.ItemsAnnounced = true;
                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }
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
}
