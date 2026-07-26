using System.Collections.Generic;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// The Jobs screen's "Equippable" row, which the game draws as sprite icons with no text.
    ///
    /// Source of truth is a hardcoded Dictionary&lt;int, List&lt;int&gt;&gt; built in the constructor of
    /// Serial.FF5.Management.JobInfomationData and read at runtime through
    /// ProviderManager.Instance.JobInfomationProvider.GetEquipIconList(jobId). dump.cs is
    /// signature-only, so it was extracted offline by decompiling that constructor out of the
    /// FF5_Analysis Ghidra project (see docs/debug.md) and cross-checked against the master
    /// tables in master_assets_all_*.bundle.
    ///
    /// Mirrored here as a static table rather than called at runtime for three reasons: it is
    /// build-time constant, it avoids a 22-job x 190-item scan on a key press, and the provider
    /// returns opaque icon ids that still need naming — the game ships no message id for any
    /// weapon category, so the names below are mod-authored.
    ///
    /// Verified against the live screen: job 7 (Knight) renders exactly four icons — knife,
    /// sword, knight sword, shield.
    /// </summary>
    public static class JobEquipData
    {
        // Icon ids are indices into the `icon` master table (tag names in comments).
        // Only the 16 that actually appear in a job's row are named; the table has 42 entries.
        private static readonly Dictionary<int, string> IconNames = new Dictionary<int, string>
        {
            {  1, "Sword" },        // IC_SRD    Broadsword, Long Sword, Rune Blade
            {  3, "Knight Sword" }, // IC_NSRD   Excalibur, Ragnarok, Defender, Flametongue
            {  4, "Katana" },       // IC_KTN    Ashura, Osafune, Kotetsu, Murasame
            {  5, "Knife" },        // IC_NIF    Knife, Dagger, Mage Masher, Main Gauche
            {  6, "Spear" },        // IC_SPR    Spear, Trident, Heavy Lance
            {  8, "Axe" },          // IC_AX     Battle Axe, Ogre Killer, Titan's Axe
            {  9, "Hammer" },       // IC_HMR    Mythril Hammer, War Hammer, Thor's Hammer
            { 10, "Staff" },        // IC_WND    Staff, Healing Staff, Sage's Staff
            { 11, "Rod" },          // IC_ROD    Rod, Flame Rod, Magus Rod
            { 12, "Bow" },          // IC_BOW    Silver Bow, Killer Bow
            { 14, "Boomerang" },    // IC_TRW    Moonring Blade, Rising Sun
            { 16, "Harp" },         // IC_HRP    Silver Harp, Apollo's Harp
            { 17, "Bell" },         // IC_BEL    Diamond Bell, Rune Chime
            { 19, "Whip" },         // IC_WHP    Whip, Chain Whip, Dragon's Whisker
            { 20, "Flail" },        // IC_FLL    Flail, Morning Star
            { 29, "Shield" },       // IC_SHIELD
        };

        // jobId -> icon ids, in the order the game renders them.
        //
        // Job 1 (Freelancer) and job 3 (Monk) are intentionally empty: the game routes those
        // through IsAll / IsNothingAllEquip and prints text instead of icons. Freelancer shows
        // its "Any" message; Monk shows nothing, being bare-handed.
        //
        // Note IC_AX and IC_HMR are separate icons even though the master data files axes and
        // hammers under one category_type, which is why Berserker shows both.
        private static readonly Dictionary<int, int[]> JobIcons = new Dictionary<int, int[]>
        {
            {  1, new int[0] },                     // Freelancer   (Any)
            {  2, new[] {  5, 14 } },               // Thief
            {  3, new int[0] },                     // Monk         (none)
            {  4, new[] {  5,  1, 11, 10, 20 } },   // Red Mage
            {  5, new[] { 10, 20 } },               // White Mage
            {  6, new[] {  5, 11 } },               // Black Mage
            {  7, new[] {  5,  1,  3, 29 } },       // Knight
            {  8, new[] {  5, 14 } },               // Ninja
            {  9, new[] {  5, 12 } },               // Ranger
            { 10, new[] {  5, 17 } },               // Geomancer
            { 11, new[] {  5,  6, 29 } },           // Dragoon
            { 12, new[] {  5, 16 } },               // Bard
            { 13, new[] {  5, 11 } },               // Summoner
            { 14, new[] {  5,  8,  9, 29 } },       // Berserker
            { 15, new[] {  5,  4, 29 } },           // Samurai
            { 16, new[] {  5, 11, 10, 20 } },       // Time Mage
            { 17, new[] {  5, 10, 20 } },           // Chemist
            { 18, new[] {  5 } },                   // Dancer
            { 19, new[] {  5,  1, 11, 29 } },       // Blue Mage
            { 20, new[] {  5,  1, 29 } },           // Mystic Knight
            { 21, new[] {  5, 19 } },               // Beastmaster
            { 22, new[] {  5, 11, 10, 20, 14, 29 } },// Mime
        };

        /// <summary>
        /// True when this job has icons to read. False for Freelancer and Monk, whose rows are
        /// text ("Any" / nothing) — those callers should read the on-screen text instead.
        /// </summary>
        public static bool HasIcons(int jobId)
            => JobIcons.TryGetValue(jobId, out var ids) && ids.Length > 0;

        /// <summary>
        /// Localized, comma-joined equippable categories in render order, or null when the job
        /// has none. Unknown ids are skipped rather than spoken as a number — a game update that
        /// adds an icon should go quiet, not read "17".
        /// </summary>
        public static string GetEquippableText(int jobId)
        {
            if (!JobIcons.TryGetValue(jobId, out var ids) || ids.Length == 0)
                return null;

            var parts = new List<string>(ids.Length);
            foreach (int id in ids)
            {
                if (IconNames.TryGetValue(id, out string name))
                    parts.Add(T(name));
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
