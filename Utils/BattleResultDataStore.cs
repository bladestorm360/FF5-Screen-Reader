using System.Collections.Generic;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Centralized static store for battle result data.
    /// Written by BattleResultPatches, read by BattleResultNavigator.
    /// </summary>
    public static class BattleResultDataStore
    {
        /// <summary>
        /// Per-character points data (EXP earned, next level EXP, ABP).
        /// </summary>
        public class CharacterPointsData
        {
            public string Name;
            public int Exp;
            public int Abp;
            public int NextExp;
            public bool IsLevelUp;
            public int NewLevel;
            public bool IsJobLevelUp;
        }

        /// <summary>
        /// Per-character stat change data (for level-up stat screen).
        /// </summary>
        public class CharacterStatData
        {
            public string Name;
            public List<StatChange> Stats = new List<StatChange>();
        }

        /// <summary>
        /// A single stat before/after pair.
        /// </summary>
        public class StatChange
        {
            public string Category; // e.g. "HP", "MP"
            public string Before;
            public string After;
            public int Diff;
        }

        // Points screen data
        public static List<CharacterPointsData> PointsData { get; private set; }
        public static int TotalExp { get; private set; }
        public static int TotalAbp { get; private set; }
        public static int TotalGil { get; private set; }

        // Stats screen data (accumulated per SetData call)
        public static List<CharacterStatData> StatsData { get; private set; }

        /// <summary>
        /// Whether any result data is available (used to determine BattleResult context).
        /// </summary>
        public static bool HasData => PointsData != null || StatsData != null;

        /// <summary>
        /// Whether points data is available.
        /// </summary>
        public static bool HasPointsData => PointsData != null && PointsData.Count > 0;

        /// <summary>
        /// Whether stats data is available.
        /// </summary>
        public static bool HasStatsData => StatsData != null && StatsData.Count > 0;

        /// <summary>
        /// Stores points screen data from ShowPointsInit.
        /// Clears any previous stats data.
        /// </summary>
        public static void SetPointsData(List<CharacterPointsData> characters, int totalExp, int totalAbp, int totalGil)
        {
            PointsData = characters;
            TotalExp = totalExp;
            TotalAbp = totalAbp;
            TotalGil = totalGil;
            StatsData = null;
        }

        /// <summary>
        /// Adds stat data for a single character from SetData_Postfix.
        /// </summary>
        public static void AddStatData(CharacterStatData charStats)
        {
            if (StatsData == null)
                StatsData = new List<CharacterStatData>();
            StatsData.Add(charStats);
        }

        /// <summary>
        /// Clears all stored data. Call when battle results are dismissed.
        /// </summary>
        public static void Clear()
        {
            PointsData = null;
            StatsData = null;
            TotalExp = 0;
            TotalAbp = 0;
            TotalGil = 0;
        }
    }
}
