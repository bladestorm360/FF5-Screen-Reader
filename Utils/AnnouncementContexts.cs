namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Centralized announcement deduplication context strings.
    /// Each context represents a distinct UI element or state that tracks
    /// what was last announced to avoid repeating the same text.
    /// </summary>
    public static class AnnouncementContexts
    {
        // Battle commands/selection
        public const string BATTLE_COMMAND_CHARACTER_ID = "BattleCommand.CharacterId";
        public const string BATTLE_COMMAND_INDEX = "BattleCommand.Index";
        public const string BATTLE_COMMAND_ITEM_SELECT = "BattleCommand.ItemSelect";
        public const string BATTLE_COMMAND_ABILITY_SELECT = "BattleCommand.AbilitySelect";

        // Battle messages
        public const string BATTLE_MESSAGE = "BattleMessage";
        public const string BATTLE_MESSAGE_CONDITION = "BattleMessage.Condition";

        // Battle actions (object-based dedup for actor actions)
        public const string BATTLE_ACTION = "BattleAction";

        // Battle results (per-phase)
        public const string BATTLE_RESULT = "BattleResult";
        public const string BATTLE_RESULT_TEXT = "BattleResult.Text";
        public const string BATTLE_RESULT_POINTS = "BattleResult.Points";
        public const string BATTLE_RESULT_LEVELUP = "BattleResult.LevelUp";
        public const string BATTLE_RESULT_ABILITIES = "BattleResult.Abilities";
        public const string BATTLE_RESULT_ITEMS = "BattleResult.Items";

        // Battle targets
        public const string BATTLE_TARGET_PLAYER_INDEX = "BattleTarget.PlayerIndex";
        public const string BATTLE_TARGET_ENEMY_INDEX = "BattleTarget.EnemyIndex";
        public const string BATTLE_TARGET_ALL_PLAYERS = "BattleTarget.AllPlayers";
        public const string BATTLE_TARGET_ALL_ENEMIES = "BattleTarget.AllEnemies";

        // Config menu
        public const string CONFIG_COMMAND = "ConfigMenu.Command";
        public const string CONFIG_KEYS_SETTING = "ConfigMenu.KeysSetting";
        public const string CONFIG_ARROW_VALUE = "ConfigMenu.ArrowValue";
        public const string CONFIG_SLIDER_CONTROLLER = "ConfigMenu.SliderController";
        public const string CONFIG_SLIDER_PERCENTAGE = "ConfigMenu.SliderPercentage";
        public const string CONFIG_TOUCH_ARROW_VALUE = "ConfigMenu.TouchArrowValue";
        public const string CONFIG_TOUCH_SLIDER_CONTROLLER = "ConfigMenu.TouchSliderController";
        public const string CONFIG_TOUCH_SLIDER_PERCENTAGE = "ConfigMenu.TouchSliderPercentage";

        // Map state
        public const string GAME_STATE_MAP_ID = "GameState.MapId";

        // Item menu
        public const string ITEM_LIST = "ItemMenu.ItemList";
        public const string ITEM_EQUIP_SELECT = "ItemMenu.EquipSelect";
        public const string ITEM_EQUIP_SLOT = "ItemMenu.EquipSlot";
        public const string ITEM_USE_TARGET = "ItemMenu.UseTarget";

        // Job/Ability menus
        public const string JOB_SELECT = "JobAbility.JobSelect";
        public const string JOB_COMMAND_SLOT = "JobAbility.CommandSlot";
        public const string JOB_ABILITY_EQUIP = "JobAbility.AbilityEquip";
        public const string JOB_EQUIP_COMMAND = "JobAbility.EquipCommand";
        public const string JOB_USE_TARGET = "JobAbility.UseTarget";
        public const string JOB_SPELL_LIST = "JobAbility.SpellList";

        // Main menu
        public const string MAIN_MENU_SET_FOCUS = "MainMenu.SetFocus";

        // Messages
        public const string MESSAGE_VIEW_SPEAKER = "Message.ViewSpeaker";
        public const string MESSAGE_MANAGER_SPEAKER = "Message.ManagerSpeaker";

        // Shop
        public const string SHOP_EQUIPMENT_COMMAND = "Shop.EquipmentCommand";

        // Naming
        public const string NAMING_SELECT = "Naming.Select";

        // Title menu
        public const string TITLE_MENU_COMMAND = "TitleMenu.Command";

        // Bestiary
        public const string BESTIARY_LIST_ENTRY = "Bestiary.ListEntry";
        public const string BESTIARY_DETAIL_STAT = "Bestiary.DetailStat";
        public const string BESTIARY_FORMATION = "Bestiary.Formation";
        public const string BESTIARY_MAP = "Bestiary.Map";
        public const string BESTIARY_STATE = "Bestiary.State";

        // Music Player
        public const string MUSIC_LIST_ENTRY = "MusicPlayer.ListEntry";

        // Gallery
        public const string GALLERY_LIST_ENTRY = "Gallery.ListEntry";
    }
}
