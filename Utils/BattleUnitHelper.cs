using Il2CppLast.Battle;
using Il2CppLast.Management;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Helper for resolving battle unit names (players and enemies).
    /// Consolidates duplicate TryCast logic from battle patch files.
    /// </summary>
    public static class BattleUnitHelper
    {
        /// <summary>
        /// Gets the display name for a battle unit (player or enemy).
        /// </summary>
        /// <param name="unitData">The battle unit data</param>
        /// <returns>Localized name, or null if unable to resolve</returns>
        public static string GetUnitName(BattleUnitData unitData)
        {
            if (unitData == null)
                return null;

            // Try player character first
            var playerData = unitData.TryCast<Il2Cpp.BattlePlayerData>();
            if (playerData?.ownedCharacterData != null)
                return playerData.ownedCharacterData.Name;

            // Try enemy
            var enemyData = unitData.TryCast<BattleEnemyData>();
            if (enemyData != null)
            {
                string mesIdName = enemyData.GetMesIdName();
                if (!string.IsNullOrEmpty(mesIdName))
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null)
                    {
                        string localizedName = messageManager.GetMessage(mesIdName);
                        if (!string.IsNullOrEmpty(localizedName))
                            return localizedName;
                    }
                }
            }

            return null;
        }
    }
}
