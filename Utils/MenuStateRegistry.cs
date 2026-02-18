using System;
using System.Collections.Generic;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Centralized registry for menu state tracking.
    /// Replaces scattered IsActive booleans across state classes.
    ///
    /// Usage:
    /// - MenuStateRegistry.SetActive("ItemMenu", true)
    /// - MenuStateRegistry.IsActive("ItemMenu")
    /// - MenuStateRegistry.SetActiveExclusive("EquipMenu") // clears all others
    /// </summary>
    public static class MenuStateRegistry
    {
        // Menu state keys (must match usage in state classes)
        public const string ITEM_MENU = "ItemMenu";
        public const string ITEM_USE = "ItemUse";
        public const string JOB_MENU = "JobMenu";
        public const string ABILITY_MENU = "AbilityMenu";
        public const string ABILITY_SLOT_MENU = "AbilitySlotMenu";
        public const string ABILITY_EQUIP_MENU = "AbilityEquipMenu";
        public const string SHOP_MENU = "ShopMenu";
        public const string SAVE_LOAD_MENU = "SaveLoadMenu";
        public const string NAMING_MENU = "NamingMenu";
        public const string STATUS_MENU = "StatusMenu";
        public const string POPUP = "Popup";
        public const string BATTLE_TARGET = "BattleTarget";
        public const string CONFIG_MENU = "ConfigMenu";
        public const string MAIN_MENU = "MainMenu";
        public const string BESTIARY_LIST = "BestiaryList";
        public const string BESTIARY_DETAIL = "BestiaryDetail";
        public const string BESTIARY_FORMATION = "BestiaryFormation";
        public const string BESTIARY_MAP = "BestiaryMap";
        public const string MUSIC_PLAYER = "MusicPlayer";
        public const string GALLERY = "Gallery";

        // Central state storage
        private static readonly Dictionary<string, bool> _states = new Dictionary<string, bool>();

        // Reset handlers for each menu (called when state is cleared)
        private static readonly Dictionary<string, Action> _resetHandlers = new Dictionary<string, Action>();

        /// <summary>
        /// Sets a menu's active state.
        /// </summary>
        public static void SetActive(string key, bool active)
        {
            _states[key] = active;

            if (!active && _resetHandlers.TryGetValue(key, out var handler))
            {
                handler?.Invoke();
            }
        }

        /// <summary>
        /// Sets a menu as active and clears all other menu states.
        /// Use when entering a menu that should be the only active one.
        /// </summary>
        public static void SetActiveExclusive(string key)
        {
            // Clear all states first
            var keys = new List<string>(_states.Keys);
            foreach (var k in keys)
            {
                if (k != key && _states.TryGetValue(k, out var wasActive) && wasActive)
                {
                    SetActive(k, false);
                }
            }

            // Now set the requested state
            SetActive(key, true);
        }

        /// <summary>
        /// Gets a menu's active state.
        /// </summary>
        public static bool IsActive(string key)
        {
            return _states.TryGetValue(key, out var active) && active;
        }

        /// <summary>
        /// Checks if any menu state is active.
        /// </summary>
        public static bool AnyActive()
        {
            foreach (var kvp in _states)
            {
                if (kvp.Value) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets list of all currently active menu states.
        /// </summary>
        public static List<string> GetActiveStates()
        {
            var result = new List<string>();
            foreach (var kvp in _states)
            {
                if (kvp.Value) result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Registers a reset handler for a menu.
        /// Called when that menu's state is set to false.
        /// </summary>
        public static void RegisterResetHandler(string key, Action handler)
        {
            _resetHandlers[key] = handler;
        }

        /// <summary>
        /// Unregisters a reset handler for a menu.
        /// </summary>
        public static void UnregisterResetHandler(string key)
        {
            _resetHandlers.Remove(key);
        }

        /// <summary>
        /// Resets a specific menu state.
        /// </summary>
        public static void Reset(string key)
        {
            SetActive(key, false);
        }

        /// <summary>
        /// Resets multiple menu states.
        /// </summary>
        public static void Reset(params string[] keys)
        {
            foreach (var key in keys)
            {
                SetActive(key, false);
            }
        }

        /// <summary>
        /// Resets all menu states.
        /// </summary>
        public static void ResetAll()
        {
            var keys = new List<string>(_states.Keys);
            foreach (var key in keys)
            {
                SetActive(key, false);
            }
        }

        /// <summary>
        /// Resets all menu states except the specified one.
        /// </summary>
        public static void ResetAllExcept(string exceptKey)
        {
            var keys = new List<string>(_states.Keys);
            foreach (var key in keys)
            {
                if (key != exceptKey)
                {
                    SetActive(key, false);
                }
            }
        }
    }
}
