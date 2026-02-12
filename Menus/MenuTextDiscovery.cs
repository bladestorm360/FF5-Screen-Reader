using System;
using FFV_ScreenReader.Core;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using MelonLoader;
using UnityEngine;
using GameCursor = Il2CppLast.UI.Cursor;
using ConfigCommandView_Touch = Il2CppLast.UI.Touch.ConfigCommandView;
using ConfigCommandView_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandView;
using ConfigCommandController_Touch = Il2CppLast.UI.Touch.ConfigCommandController;
using ConfigCommandController_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandController;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigKeysSettingController = Il2CppLast.UI.KeyInput.ConfigKeysSettingController;
using ConfigControllCommandController = Il2CppLast.UI.KeyInput.ConfigControllCommandController;
using MessageManager = Il2CppLast.Management.MessageManager;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Core text discovery system that tries multiple strategies to find menu text.
    /// This is the heart of the mod's menu reading capability.
    /// </summary>
    public static class MenuTextDiscovery
    {
        /// <summary>
        /// Coroutine to wait one frame then read cursor position.
        /// This delay is critical because the game updates cursor position asynchronously.
        /// Ported from FF6 screen reader.
        /// </summary>
        public static System.Collections.IEnumerator WaitAndReadCursor(GameCursor cursor, string direction, int count, bool isLoop)
        {
            yield return null; // Wait one frame

            // Suppress if battle target selection became active during the delay
            if (FFV_ScreenReader.Patches.BattleTargetPatches.IsTargetSelectionActive)
                yield break;

            try
            {
                // Safety checks to prevent crashes
                if (cursor == null)
                {
                    yield break;
                }

                if (cursor.gameObject == null)
                {
                    yield break;
                }

                if (cursor.transform == null)
                {
                    yield break;
                }

                // Try multiple strategies to find menu text
                string menuText = TryAllStrategies(cursor);

                // Check for config menu values
                if (menuText != null)
                {
                    string configValue = ConfigMenuReader.FindConfigValueText(cursor.transform, cursor.Index);
                    if (configValue != null)
                    {
                        // Combine option name and value
                        string fullText = $"{menuText}: {configValue}";
                        FFV_ScreenReaderMod.SpeakText(fullText);
                    }
                    else
                    {
                        FFV_ScreenReaderMod.SpeakText(menuText);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in delayed cursor read: {ex.Message}");
            }
        }

        /// <summary>
        /// Try all text discovery strategies in sequence until one succeeds.
        /// </summary>
        private static string TryAllStrategies(GameCursor cursor)
        {
            string menuText = null;

            // Strategy: Main menu (Items/Magic/Equip/Status/etc.) — read directly from CommandMenuController
            menuText = TryReadMainMenu(cursor);
            if (menuText != null) return menuText;

            // Strategy 0: Battle enemy targeting (check first as it's very specific)
            menuText = TryReadBattleEnemyTarget(cursor);
            if (menuText != null) return menuText;

            // Note: Save/Load slot reading is now handled by SaveLoadPatches via Harmony patch on SaveListController.SelectContent

            // Strategy 1: Character selection (formation, status, equipment, etc.)
            menuText = CharacterSelectionReader.TryReadCharacterSelection(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 3: Try to read directly from ConfigActualDetailsControllerBase (most reliable for config menus)
            menuText = TryReadFromConfigController(cursor);
            if (menuText != null) return menuText;

            // Strategy 4: Try to read directly from ConfigKeysSettingController (keyboard/gamepad settings)
            menuText = TryReadFromKeysSettingController(cursor);
            if (menuText != null) return menuText;

            // Strategy 5: Title-style approach (cursor moves in hierarchy)
            menuText = TryDirectTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            // Strategy 5: Config-style menus (ConfigCommandView)
            menuText = TryConfigCommandView(cursor);
            if (menuText != null) return menuText;

            // Strategy 5: Battle menus with IconTextView (ability/item lists)
            menuText = TryIconTextView(cursor);
            if (menuText != null) return menuText;

            // Strategy 6: Keyboard/Gamepad settings
            menuText = KeyboardGamepadReader.TryReadSettings(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 7: In-game config menu structure
            menuText = TryInGameConfigMenu(cursor);
            if (menuText != null) return menuText;

            // Strategy 8: Fallback with GetComponentInChildren
            menuText = TryFallbackTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            return null;
        }

        /// <summary>
        /// Strategy: Read main menu item name from CommandMenuController.contents.
        /// When MAIN_MENU is active, reads directly from the authoritative source
        /// to avoid stale text from inactive sub-menus.
        /// Only reads when cursor is actually under "command_menu" in the hierarchy,
        /// preventing sub-menu command bars from returning main menu text.
        /// </summary>
        private static string TryReadMainMenu(GameCursor cursor)
        {
            try
            {
                if (!MenuStateRegistry.IsActive(MenuStateRegistry.MAIN_MENU))
                    return null;

                // Only read from CommandMenuController if cursor is in the actual command_menu hierarchy
                if (!IsCursorInCommandMenu(cursor.transform))
                    return null;

                var controller = GameObjectCache.Get<Il2CppLast.UI.CommandMenuController>();
                if (controller == null)
                    controller = GameObjectCache.Refresh<Il2CppLast.UI.CommandMenuController>();

                // Skip stale cached controllers whose GameObjects are inactive
                if (controller != null && controller.gameObject != null &&
                    !controller.gameObject.activeInHierarchy)
                    controller = null;

                if (controller == null || controller.contents == null)
                    return null;

                int index = cursor.Index;
                if (index < 0 || index >= controller.contents.Count)
                    return null;

                var content = controller.contents[index];
                if (content != null && content.NameText != null)
                {
                    string text = content.NameText.text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Register with shared dedup so SetFocus won't re-announce on confirm
                        AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.MAIN_MENU_SET_FOCUS, text);
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading main menu: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if the cursor is inside the main command_menu hierarchy.
        /// Returns false for sub-menu command bars (equipment, etc.).
        /// </summary>
        private static bool IsCursorInCommandMenu(Transform cursorTransform)
        {
            Transform current = cursorTransform;
            int depth = 0;
            while (current != null && depth < 15)
            {
                if (current.name.ToLower().Contains("comand_menu"))
                    return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        /// <summary>
        /// Strategy 0: Try to read enemy name during battle targeting.
        /// Uses BattleState.IsInBattle flag instead of expensive FindObjectsOfType.
        /// </summary>
        private static string TryReadBattleEnemyTarget(GameCursor cursor)
        {
            try
            {
                // Check if we're in battle using cached state flag
                // This avoids expensive FindObjectsOfType<BattleEnemyEntity> call
                if (!FFV_ScreenReader.Patches.BattleState.IsInBattle)
                {
                    // Not in battle
                    return null;
                }

                // For now, return null to let fallback handle it
                // Once we see the logs we'll know where to find the enemy name
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading battle enemy target: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Try to read menu item name directly from ConfigActualDetailsControllerBase.CommandList.
        /// This is the most reliable method for config menus.
        /// IMPORTANT: Only use this if cursor is actually in the config menu context.
        /// </summary>
        private static string TryReadFromConfigController(GameCursor cursor)
        {
            try
            {
                // Only read config controllers when config menu is actually active
                if (!MenuStateRegistry.IsActive(MenuStateRegistry.CONFIG_MENU))
                    return null;

                // Check if cursor is inside a dialog - if so, skip config controller
                if (IsCursorInDialog(cursor.transform))
                {
                    return null;
                }

                int cursorIndex = cursor.Index;

                // Try Touch version (title screen)
                // Use GameObjectCache to avoid expensive FindObjectOfType
                var controllerTouch = FFV_ScreenReader.Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_Touch>();
                if (controllerTouch == null)
                    controllerTouch = FFV_ScreenReader.Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_Touch>();
                // Skip stale cached controllers whose GameObjects are inactive
                if (controllerTouch != null && controllerTouch.gameObject != null &&
                    !controllerTouch.gameObject.activeInHierarchy)
                    controllerTouch = null;
                if (controllerTouch != null && controllerTouch.CommandList != null)
                {
                    if (cursorIndex >= 0 && cursorIndex < controllerTouch.CommandList.Count)
                    {
                        var command = controllerTouch.CommandList[cursorIndex];
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                return menuText;
                            }
                        }
                    }
                }

                // Try KeyInput version (in-game)
                // Use GameObjectCache to avoid expensive FindObjectOfType
                var controllerKeyInput = FFV_ScreenReader.Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_KeyInput>();
                if (controllerKeyInput == null)
                    controllerKeyInput = FFV_ScreenReader.Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_KeyInput>();
                // Skip stale cached controllers whose GameObjects are inactive
                if (controllerKeyInput != null && controllerKeyInput.gameObject != null &&
                    !controllerKeyInput.gameObject.activeInHierarchy)
                    controllerKeyInput = null;
                if (controllerKeyInput != null && controllerKeyInput.CommandList != null)
                {
                    if (cursorIndex >= 0 && cursorIndex < controllerKeyInput.CommandList.Count)
                    {
                        var command = controllerKeyInput.CommandList[cursorIndex];
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                return menuText;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from config controller: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if the cursor is inside a dialog/popup context.
        /// </summary>
        private static bool IsCursorInDialog(Transform cursorTransform)
        {
            try
            {
                // Walk up the cursor's parent hierarchy looking for dialog-related objects
                Transform current = cursorTransform;
                int depth = 0;
                while (current != null && depth < 15)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("popup") || name.Contains("dialog") || name.Contains("prompt") ||
                        name.Contains("message_window") || name.Contains("yesno") || name.Contains("confirm"))
                    {
                        return true;
                    }
                    current = current.parent;
                    depth++;
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking cursor dialog context: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to read menu item name from ConfigKeysSettingController.
        /// Handles keyboard, gamepad, and mouse settings menus.
        /// </summary>
        private static string TryReadFromKeysSettingController(GameCursor cursor)
        {
            try
            {
                // Check if cursor is in a dialog
                if (IsCursorInDialog(cursor.transform))
                {
                    return null;
                }

                int cursorIndex = cursor.Index;

                // Find the ConfigKeysSettingController
                // Use GameObjectCache to avoid expensive FindObjectOfType
                var keysController = FFV_ScreenReader.Utils.GameObjectCache.Get<ConfigKeysSettingController>();
                if (keysController == null)
                    keysController = FFV_ScreenReader.Utils.GameObjectCache.Refresh<ConfigKeysSettingController>();
                // Skip stale cached controllers whose GameObjects are inactive
                if (keysController != null && keysController.gameObject != null &&
                    !keysController.gameObject.activeInHierarchy)
                    keysController = null;
                if (keysController == null)
                {
                    return null;
                }

                // Determine which list to use based on which is populated and matches cursor index
                // Try keyboard list first (most common)
                if (keysController.keyboardCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.keyboardCommandList.Count)
                {
                    var command = keysController.keyboardCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            return text;
                        }
                    }
                }

                // Try gamepad list
                if (keysController.gamepadCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.gamepadCommandList.Count)
                {
                    var command = keysController.gamepadCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            return text;
                        }
                    }
                }

                // Try mouse list
                if (keysController.mouseCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.mouseCommandList.Count)
                {
                    var command = keysController.mouseCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from keys setting controller: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read text from a ConfigControllCommandController.
        /// Returns both the action name and the key binding (e.g., "Confirm Enter").
        /// </summary>
        private static string ReadKeyCommandText(ConfigControllCommandController command)
        {
            try
            {
                var textParts = new System.Collections.Generic.List<string>();

                // First, get the localized action name from MessageId
                if (!string.IsNullOrWhiteSpace(command.MessageId))
                {
                    try
                    {
                        var messageManager = MessageManager.Instance;
                        if (messageManager != null)
                        {
                            string localizedText = messageManager.GetMessage(command.MessageId, false);
                            if (!string.IsNullOrWhiteSpace(localizedText))
                            {
                                textParts.Add(localizedText.Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Could not localize MessageId '{command.MessageId}': {ex.Message}");
                    }
                }

                // Then, read all text components from messageTexts (includes key bindings)
                if (command.messageTexts != null && command.messageTexts.Count > 0)
                {
                    foreach (var textComponent in command.messageTexts)
                    {
                        if (textComponent != null && !string.IsNullOrWhiteSpace(textComponent.text))
                        {
                            string text = textComponent.text.Trim();
                            // Skip if it's the same as what we already got from MessageId
                            // or if it's a localization key placeholder
                            if (!text.StartsWith("MENU_") && !textParts.Contains(text))
                            {
                                textParts.Add(text);
                            }
                        }
                    }
                }

                if (textParts.Count > 0)
                {
                    return string.Join(" ", textParts);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading key command text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 2: Walk up parent hierarchy looking for direct text components.
        /// </summary>
        private static string TryDirectTextSearch(Transform cursorTransform)
        {
            Transform current = cursorTransform;
            int hierarchyDepth = 0;

            while (current != null && hierarchyDepth < 10)
            {
                try
                {
                    if (current.gameObject == null)
                    {
                        break;
                    }

                    // Look for text directly on this object (not children)
                    var text = current.GetComponent<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        string menuText = text.text;
                        return menuText;
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error walking hierarchy at depth {hierarchyDepth}: {ex.Message}");
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Strategy 3: Look for ConfigCommandView components (both Touch and KeyInput versions).
        /// </summary>
        private static string TryConfigCommandView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    // Try Touch version (title screen config)
                    var configViewTouch = current.GetComponent<ConfigCommandView_Touch>();
                    if (configViewTouch != null && configViewTouch.nameText?.text != null)
                    {
                        string menuText = configViewTouch.nameText.text.Trim();
                        return menuText;
                    }

                    // Try KeyInput version (in-game config)
                    var configViewKeyInput = current.GetComponent<ConfigCommandView_KeyInput>();
                    if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                    {
                        string menuText = configViewKeyInput.nameText.text.Trim();
                        return menuText;
                    }

                    // Check parent too
                    if (current.parent != null)
                    {
                        configViewTouch = current.parent.GetComponent<ConfigCommandView_Touch>();
                        if (configViewTouch != null && configViewTouch.nameText?.text != null)
                        {
                            string menuText = configViewTouch.nameText.text.Trim();
                            return menuText;
                        }

                        configViewKeyInput = current.parent.GetComponent<ConfigCommandView_KeyInput>();
                        if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                        {
                            string menuText = configViewKeyInput.nameText.text.Trim();
                            return menuText;
                        }
                    }

                    // Look for config_root which indicates config-style menu
                    if (current.name == "config_root")
                    {
                        return TryConfigRootMenu(current, cursor.Index);
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in config menu check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Handle config_root menu structure.
        /// </summary>
        private static string TryConfigRootMenu(Transform configRoot, int cursorIndex)
        {
            try
            {
                // Find the Content object that contains all config_tool_command items
                var content = configRoot.GetComponentInChildren<Transform>()?.Find("MaskObject/Scroll View/Viewport/Content");
                if (content != null && cursorIndex >= 0 && cursorIndex < content.childCount)
                {
                    var configItem = content.GetChild(cursorIndex);
                    if (configItem != null && configItem.gameObject != null)
                    {
                        // Check if this has the same structure as title config
                        var rootChild = configItem.Find("root");
                        if (rootChild != null)
                        {
                            // Try Touch version
                            var rootConfigViewTouch = rootChild.GetComponent<ConfigCommandView_Touch>();
                            if (rootConfigViewTouch != null && rootConfigViewTouch.nameText?.text != null)
                            {
                                string menuText = rootConfigViewTouch.nameText.text.Trim();
                                return menuText;
                            }

                            // Try KeyInput version
                            var rootConfigViewKeyInput = rootChild.GetComponent<ConfigCommandView_KeyInput>();
                            if (rootConfigViewKeyInput != null && rootConfigViewKeyInput.nameText?.text != null)
                            {
                                string menuText = rootConfigViewKeyInput.nameText.text.Trim();
                                return menuText;
                            }
                        }

                        // Look for ConfigCommandView anywhere in the item (Touch version)
                        var itemConfigViewTouch = configItem.GetComponentInChildren<ConfigCommandView_Touch>();
                        if (itemConfigViewTouch != null && itemConfigViewTouch.nameText?.text != null)
                        {
                            string menuText = itemConfigViewTouch.nameText.text.Trim();
                            return menuText;
                        }

                        // Look for ConfigCommandView anywhere in the item (KeyInput version)
                        var itemConfigViewKeyInput = configItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                        if (itemConfigViewKeyInput != null && itemConfigViewKeyInput.nameText?.text != null)
                        {
                            string menuText = itemConfigViewKeyInput.nameText.text.Trim();
                            return menuText;
                        }

                        // Try to find the correct text (not "Battle Type")
                        var allTexts = configItem.GetComponentsInChildren<UnityEngine.UI.Text>();
                        foreach (var text in allTexts)
                        {
                            // Skip if it's "Battle Type" and we're not on the first item
                            if (text.text == "Battle Type" && cursorIndex > 0)
                                continue;

                            // Look for text that seems like a menu option name
                            if (text.name.Contains("command_name") || text.name.Contains("nameText") || text.name == "last_text")
                            {
                                if (!string.IsNullOrEmpty(text.text?.Trim()))
                                {
                                    string menuText = text.text.Trim();
                                    return menuText;
                                }
                            }
                        }

                        // Final fallback
                        var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                        {
                            string menuText = configText.text;
                            return menuText;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in config root menu: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 4: Battle menus with IconTextView (ability/item lists).
        /// Battle menus use IconTextView components which wrap the actual Text component.
        /// </summary>
        private static string TryIconTextView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        break;
                    }

                    // Look for IconTextView components directly on cursor
                    var iconTextView = current.GetComponent<IconTextView>();
                    if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                    {
                        string menuText = iconTextView.nameText.text.Trim();
                        if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                        {
                            return menuText;
                        }
                    }

                    // Try to find a Content list with indexed children (common in scrollable lists)
                    Transform contentList = FindContentList(current);
                    if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                    {
                        Transform selectedChild = contentList.GetChild(cursor.Index);

                        if (selectedChild != null)
                        {
                            // Look for IconTextView in this specific child
                            iconTextView = selectedChild.GetComponentInChildren<IconTextView>();
                            if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                            {
                                string menuText = iconTextView.nameText.text.Trim();
                                if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                                {
                                    return menuText;
                                }
                            }

                            // Look for BattleAbilityInfomationContentView in this child
                            var battleAbilityView = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentView>();
                            if (battleAbilityView != null)
                            {
                                if (battleAbilityView.iconTextView != null &&
                                    battleAbilityView.iconTextView.nameText != null &&
                                    battleAbilityView.iconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityView.iconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                                    {
                                        return menuText;
                                    }
                                }

                                if (battleAbilityView.abilityIconText != null &&
                                    battleAbilityView.abilityIconText.nameText != null &&
                                    battleAbilityView.abilityIconText.nameText.text != null)
                                {
                                    string menuText = battleAbilityView.abilityIconText.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                                    {
                                        return menuText;
                                    }
                                }
                            }

                            // Look for BattleAbilityInfomationContentController in this child
                            var battleAbilityController = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentController>();
                            if (battleAbilityController != null && battleAbilityController.view != null)
                            {
                                if (battleAbilityController.view.iconTextView != null &&
                                    battleAbilityController.view.iconTextView.nameText != null &&
                                    battleAbilityController.view.iconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityController.view.iconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                                    {
                                        return menuText;
                                    }
                                }

                                if (battleAbilityController.view.abilityIconText != null &&
                                    battleAbilityController.view.abilityIconText.nameText != null &&
                                    battleAbilityController.view.abilityIconText.nameText.text != null)
                                {
                                    string menuText = battleAbilityController.view.abilityIconText.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText) && !ContainsCJK(menuText))
                                    {
                                        return menuText;
                                    }
                                }
                            }
                        }
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IconTextView check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 6: In-game config menu structure.
        /// </summary>
        private static string TryInGameConfigMenu(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    // In-game config uses command_list_root or similar structure
                    if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                    {
                        // Try to find the content list with menu items
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                        {
                            var menuItem = contentList.GetChild(cursor.Index);

                            // Look for ConfigCommandController on this item
                            var commandController = menuItem.GetComponent<ConfigCommandController>();
                            if (commandController == null)
                            {
                                commandController = menuItem.GetComponentInChildren<ConfigCommandController>();
                            }

                            if (commandController != null)
                            {
                                // Get the view which has the text
                                if (commandController.view != null && commandController.view.nameText != null)
                                {
                                    string menuText = commandController.view.nameText.text.Trim();
                                    return menuText;
                                }
                            }

                            // Alternative: Look for ConfigCommandView directly (Touch version)
                            var commandViewTouch = menuItem.GetComponentInChildren<ConfigCommandView_Touch>();
                            if (commandViewTouch != null && commandViewTouch.nameText != null)
                            {
                                string menuText = commandViewTouch.nameText.text.Trim();
                                return menuText;
                            }

                            // Alternative: Look for ConfigCommandView directly (KeyInput version)
                            var commandViewKeyInput = menuItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                            if (commandViewKeyInput != null && commandViewKeyInput.nameText != null)
                            {
                                string menuText = commandViewKeyInput.nameText.text.Trim();
                                return menuText;
                            }

                            // Last resort: Get text components but avoid values
                            // Use non-allocating search for first valid text that isn't a value
                            var foundText = FindFirstText(menuItem, t =>
                            {
                                if (string.IsNullOrEmpty(t.text?.Trim()))
                                    return false;
                                var textValue = t.text.Trim();
                                // Skip if it looks like a value (number, percentage, On/Off)
                                return !System.Text.RegularExpressions.Regex.IsMatch(textValue, @"^\d+%?$|^On$|^Off$|^Active$|^Wait$");
                            });

                            if (foundText != null)
                            {
                                string menuText = foundText.text.Trim();
                                return menuText;
                            }
                        }
                        break;
                    }
                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in in-game config menu check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 7: Final fallback with GetComponentInChildren.
        /// </summary>
        private static string TryFallbackTextSearch(Transform cursorTransform)
        {
            try
            {
                Transform current = cursorTransform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        break;
                    }

                    var text = current.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        // Skip inactive text components (stale from previous menus)
                        if (!text.gameObject.activeInHierarchy)
                        {
                            current = current.parent;
                            hierarchyDepth++;
                            continue;
                        }

                        string menuText = text.text.Trim();

                        // Skip text that contains CJK characters (likely untranslated Japanese)
                        if (ContainsCJK(menuText))
                        {
                            current = current.parent;
                            hierarchyDepth++;
                            continue;
                        }

                        return menuText;
                    }
                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in fallback text search: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Returns true if the text contains CJK characters (Chinese/Japanese/Korean).
        /// Used to filter out untranslated raw text from inactive menu components.
        /// </summary>
        private static bool ContainsCJK(string text)
        {
            foreach (char c in text)
            {
                // CJK Unified Ideographs, Hiragana, Katakana
                if ((c >= '\u4E00' && c <= '\u9FFF') || // CJK Unified Ideographs
                    (c >= '\u3040' && c <= '\u309F') || // Hiragana
                    (c >= '\u30A0' && c <= '\u30FF'))   // Katakana
                {
                    return true;
                }
            }
            return false;
        }

    }
}
