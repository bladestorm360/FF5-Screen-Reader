using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Battle;
using Il2CppLast.Management;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using UnityEngine;
using BattlePlayerData = Il2Cpp.BattlePlayerData;

// Use the KeyInput version of BattleTargetSelectController (for keyboard/controller input)
using BattleTargetSelectController = Il2CppLast.UI.KeyInput.BattleTargetSelectController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for battle target selection.
    /// Announces target names with HP/MP when navigating through targets.
    /// For party members: "Name: HP: current/max. MP: current/max"
    /// For enemies: "Name: HP: current/max"
    /// </summary>
    public static class BattleTargetPatches
    {
        /// <summary>
        /// Indicates whether target selection is currently active.
        /// Used by MenuTextDiscovery to suppress reading "Attack" etc. during target selection.
        /// </summary>
        public static bool IsTargetSelectionActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.BATTLE_TARGET);
            private set => MenuStateRegistry.SetActive(MenuStateRegistry.BATTLE_TARGET, value);
        }

        /// <summary>
        /// Which kind of target the cursor is currently on. Single-target and all-target
        /// announcements are mutually exclusive, so one mode + index describes the whole
        /// selection: moving single-&gt;all-&gt;single changes the mode and re-announces.
        /// </summary>
        private enum TargetMode { None, SinglePlayer, SingleEnemy, AllPlayers, AllEnemies }

        // The game re-invokes SelectContent / PlayerAllInit / EnemyAllInit on every refresh while
        // the target window is up, not just on cursor movement. Announce only when (mode, index)
        // actually changes. Reset on every ShowWindow (open AND close) so a new selection always speaks.
        private static TargetMode _mode = TargetMode.None;
        private static int _index = -1;

        static BattleTargetPatches()
        {
            MenuStateRegistry.RegisterResetHandler(MenuStateRegistry.BATTLE_TARGET, ResetState);
        }

        /// <summary>
        /// Resets the announcement state. Call this when entering/exiting target selection.
        /// </summary>
        public static void ResetState()
        {
            _mode = TargetMode.None;
            _index = -1;
        }

        /// <summary>
        /// Returns true if the focused target changed since the last announcement, recording the
        /// new position. Index is ignored for the all-target modes (they have no cursor).
        /// </summary>
        private static bool TargetChanged(TargetMode mode, int index = -1)
        {
            if (_mode == mode && _index == index) return false;
            _mode = mode;
            _index = index;
            return true;
        }

        /// <summary>
        /// Sets the target selection active state.
        /// </summary>
        public static void SetTargetSelectionActive(bool active)
        {
            IsTargetSelectionActive = active;
        }

        /// <summary>
        /// Builds the announcement string for a player target.
        /// Format: "Name: HP: current/max. MP: current/max"
        /// </summary>
        public static string BuildPlayerAnnouncement(BattlePlayerData playerData)
        {
            try
            {
                if (playerData == null) return null;

                string name = "Unknown";
                int currentHp = 0;
                int maxHp = 0;
                int currentMp = 0;
                int maxMp = 0;

                // Get the character name
                var ownedCharData = playerData.ownedCharacterData;
                if (ownedCharData != null)
                {
                    name = ownedCharData.Name;

                    // Try to get max HP/MP from OwnedCharacterData's Parameter
                    var charParam = ownedCharData.Parameter;
                    if (charParam != null)
                    {
                        try
                        {
                            maxHp = charParam.ConfirmedMaxHp();
                            maxMp = charParam.ConfirmedMaxMp();
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Error getting confirmed max HP/MP: {ex.Message}");
                        }
                    }
                }

                // Get current HP/MP from battle data
                var battleInfo = playerData.BattleUnitDataInfo;
                if (battleInfo != null)
                {
                    var param = battleInfo.Parameter;
                    if (param != null)
                    {
                        currentHp = param.CurrentHP;
                        currentMp = param.CurrentMP;

                        // Fallback: if maxHp is still 0, try ConfirmedMaxHp from battle parameter
                        if (maxHp == 0)
                        {
                            try
                            {
                                maxHp = param.ConfirmedMaxHp();
                                maxMp = param.ConfirmedMaxMp();
                            }
                            catch
                            {
                                // Use BaseMaxHp as last resort
                                maxHp = param.BaseMaxHp;
                                maxMp = param.BaseMaxMp;
                            }
                        }
                    }
                }

                string result = $"{name}: HP: {currentHp}/{maxHp}. MP: {currentMp}/{maxMp}";

                // Append status effects if any
                var statusParam = playerData.BattleUnitDataInfo?.Parameter;
                if (statusParam != null)
                {
                    string conditions = CharacterStatusHelper.GetStatusConditions(statusParam);
                    if (!string.IsNullOrEmpty(conditions))
                        result += $". status: {conditions}";
                }

                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building player announcement: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns " A", " B", ... when the enemy at <paramref name="index"/> shares its name with
        /// at least one other enemy in the formation, or "" otherwise. FF5 itself has no such
        /// label — BattleEnemyData exposes only GetMesIdName() — so the mod derives it, matching
        /// FF1. Opt-in, because it matters most when Enemy HP Display is Hidden and the position
        /// is otherwise the only thing separating three identical targets.
        /// </summary>
        private static string GetEnemyLetter(Il2CppSystem.Collections.Generic.List<BattleEnemyData> enemyList, int index)
        {
            if (!FFV_ScreenReaderMod.EnemyLettersEnabled) return "";
            if (enemyList == null || index < 0 || index >= enemyList.Count) return "";

            try
            {
                var messageManager = MessageManager.Instance;
                if (messageManager == null) return "";

                string selectedName = ResolveEnemyName(enemyList[index], messageManager);
                if (string.IsNullOrEmpty(selectedName)) return "";

                int sameNameCount = 0;
                int positionInGroup = 0;

                for (int i = 0; i < enemyList.Count; i++)
                {
                    if (ResolveEnemyName(enemyList[i], messageManager) != selectedName) continue;

                    sameNameCount++;
                    if (i < index) positionInGroup++;
                }

                if (sameNameCount <= 1) return "";

                return $" {(char)('A' + positionInGroup)}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error deriving enemy letter: {ex.Message}");
                return "";
            }
        }

        /// <summary>Resolves an enemy's localized name, or null.</summary>
        private static string ResolveEnemyName(BattleEnemyData enemy, MessageManager messageManager)
        {
            if (enemy == null) return null;

            try
            {
                string mesIdName = enemy.GetMesIdName();
                if (string.IsNullOrEmpty(mesIdName)) return null;

                string name = messageManager.GetMessage(mesIdName);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the announcement string for an enemy target.
        /// Format: "Name[ letter]: HP: current/max"
        /// </summary>
        public static string BuildEnemyAnnouncement(BattleEnemyData enemyData, string letterSuffix = "")
        {
            try
            {
                if (enemyData == null) return null;

                string name = "Unknown";
                int currentHp = 0;
                int maxHp = 0;

                // Get enemy name from message system
                try
                {
                    string mesIdName = enemyData.GetMesIdName();
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                    {
                        string localizedName = messageManager.GetMessage(mesIdName);
                        if (!string.IsNullOrEmpty(localizedName))
                        {
                            name = localizedName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error getting enemy name: {ex.Message}");
                }

                // Get HP from battle data
                var battleInfo = enemyData.BattleUnitDataInfo;
                if (battleInfo != null)
                {
                    var param = battleInfo.Parameter;
                    if (param != null)
                    {
                        currentHp = param.CurrentHP;
                        try
                        {
                            maxHp = param.ConfirmedMaxHp();
                        }
                        catch
                        {
                            maxHp = param.BaseMaxHp;
                        }
                    }
                }

                // Honour the enemy HP display preference (F5 / mod menu). Exact enemy HP is a
                // cheat-adjacent disclosure, so "Hidden" must reveal nothing at all.
                string result = name + letterSuffix;
                switch (PreferencesManager.EnemyHPDisplay)
                {
                    case 0: // Numbers (default)
                        result += string.Format(T(": HP: {0}/{1}"), currentHp, maxHp);
                        break;
                    case 1: // Percentage
                        int pct = maxHp > 0 ? (currentHp * 100 / maxHp) : 0;
                        result += $": {pct}%";
                        break;
                    case 2: // Hidden — no HP disclosed
                        break;
                }

                // Append status effects if any
                var statusParam = battleInfo?.Parameter;
                if (statusParam != null)
                {
                    string conditions = CharacterStatusHelper.GetStatusConditions(statusParam);
                    if (!string.IsNullOrEmpty(conditions))
                        result += $". status: {conditions}";
                }

                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building enemy announcement: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a player at the specified index from an IL2CPP IEnumerable.
        /// </summary>
        public static BattlePlayerData GetPlayerAtIndex(Il2CppSystem.Collections.Generic.IEnumerable<BattlePlayerData> list, int index)
        {
            try
            {
                if (list == null) return null;

                // Cast to List to access by index
                var asList = list.TryCast<Il2CppSystem.Collections.Generic.List<BattlePlayerData>>();
                if (asList != null && index >= 0 && index < asList.Count)
                {
                    return asList[index];
                }

                MelonLogger.Warning($"Could not cast player list or index {index} out of range");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting player at index {index}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets an enemy at the specified index from an IL2CPP IEnumerable.
        /// </summary>
        public static BattleEnemyData GetEnemyAtIndex(Il2CppSystem.Collections.Generic.IEnumerable<BattleEnemyData> list, int index)
        {
            try
            {
                if (list == null) return null;

                // Cast to List to access by index
                var asList = list.TryCast<Il2CppSystem.Collections.Generic.List<BattleEnemyData>>();
                if (asList != null && index >= 0 && index < asList.Count)
                {
                    return asList[index];
                }

                MelonLogger.Warning($"Could not cast enemy list or index {index} out of range");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting enemy at index {index}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Announces a player target selection.
        /// </summary>
        public static void AnnouncePlayerTarget(Il2CppSystem.Collections.Generic.IEnumerable<BattlePlayerData> list, int index)
        {
            try
            {
                if (!TargetChanged(TargetMode.SinglePlayer, index)) return;

                var selectedPlayer = GetPlayerAtIndex(list, index);
                if (selectedPlayer == null) return;

                string announcement = BuildPlayerAnnouncement(selectedPlayer);
                if (!string.IsNullOrEmpty(announcement))
                {
                    // Append target position last (index within the selectable player list).
                    var asList = list.TryCast<Il2CppSystem.Collections.Generic.List<BattlePlayerData>>();
                    announcement = MenuPosition.Format(announcement, index, asList != null ? asList.Count : 0);
                    FFV_ScreenReaderMod.SpeakText(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing player target: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces an enemy target selection.
        /// </summary>
        public static void AnnounceEnemyTarget(Il2CppSystem.Collections.Generic.IEnumerable<BattleEnemyData> list, int index)
        {
            try
            {
                if (!TargetChanged(TargetMode.SingleEnemy, index)) return;

                var selectedEnemy = GetEnemyAtIndex(list, index);
                if (selectedEnemy == null) return;

                var asList = list.TryCast<Il2CppSystem.Collections.Generic.List<BattleEnemyData>>();

                string announcement = BuildEnemyAnnouncement(selectedEnemy, GetEnemyLetter(asList, index));
                if (!string.IsNullOrEmpty(announcement))
                {
                    // Append target position last (index within the selectable enemy list).
                    announcement = MenuPosition.Format(announcement, index, asList != null ? asList.Count : 0);
                    FFV_ScreenReaderMod.SpeakText(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing enemy target: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces all players being targeted.
        /// </summary>
        public static void AnnounceAllPlayers()
        {
            if (!TargetChanged(TargetMode.AllPlayers)) return;

            FFV_ScreenReaderMod.SpeakText(T("All allies"));
        }

        /// <summary>
        /// Announces all enemies being targeted.
        /// </summary>
        public static void AnnounceAllEnemies()
        {
            if (!TargetChanged(TargetMode.AllEnemies)) return;

            FFV_ScreenReaderMod.SpeakText(T("All enemies"));
        }
    }

    /// <summary>
    /// Patch for when player target selection changes.
    /// KeyInput version uses: SelectContent(IEnumerable&lt;BattlePlayerData&gt; list, int index)
    /// </summary>
    [HarmonyPatch(typeof(BattleTargetSelectController), "SelectContent",
        new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<BattlePlayerData>), typeof(int) })]
    public static class BattleTargetSelectController_SelectContent_Player_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleTargetSelectController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<BattlePlayerData> list, int index)
        {
            try
            {
                BattleTargetPatches.AnnouncePlayerTarget(list, index);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SelectContent(Player) patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for when enemy target selection changes.
    /// KeyInput version uses: SelectContent(IEnumerable&lt;BattleEnemyData&gt; list, int index)
    /// </summary>
    [HarmonyPatch(typeof(BattleTargetSelectController), "SelectContent",
        new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<BattleEnemyData>), typeof(int) })]
    public static class BattleTargetSelectController_SelectContent_Enemy_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleTargetSelectController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<BattleEnemyData> list, int index)
        {
            try
            {
                BattleTargetPatches.AnnounceEnemyTarget(list, index);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SelectContent(Enemy) patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for PlayerAllInit - called when all players are targeted.
    /// </summary>
    [HarmonyPatch(typeof(BattleTargetSelectController), "PlayerAllInit")]
    public static class BattleTargetSelectController_PlayerAllInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleTargetSelectController __instance)
        {
            try
            {
                BattleTargetPatches.AnnounceAllPlayers();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in PlayerAllInit patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for EnemyAllInit - called when all enemies are targeted.
    /// </summary>
    [HarmonyPatch(typeof(BattleTargetSelectController), "EnemyAllInit")]
    public static class BattleTargetSelectController_EnemyAllInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleTargetSelectController __instance)
        {
            try
            {
                BattleTargetPatches.AnnounceAllEnemies();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EnemyAllInit patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch to reset state when target selection is shown/hidden.
    /// </summary>
    [HarmonyPatch(typeof(BattleTargetSelectController), nameof(BattleTargetSelectController.ShowWindow))]
    public static class BattleTargetSelectController_ShowWindow_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleTargetSelectController __instance, bool isShow)
        {
            try
            {
                // Set target selection active state based on visibility
                BattleTargetPatches.SetTargetSelectionActive(isShow);
                // Reset announcement tracking when target selection opens or closes
                BattleTargetPatches.ResetState();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ShowWindow patch: {ex.Message}");
            }
        }
    }

}
