using System;
using FFV_ScreenReader.Core;
using MelonLoader;
using UnityEngine;
using Il2CppLast.UI;
using GameCursor = Il2CppLast.UI.Cursor;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using Il2CppLast.Battle;
using Il2CppLast.UI.Message;
using Il2CppLast.Data.Master;
using Il2Cpp = Il2Cpp;
using FFV_ScreenReader.Utils;
using ConfigCommandView_Touch = Il2CppLast.UI.Touch.ConfigCommandView;
using ConfigCommandView_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandView;
using ConfigCommandController_Touch = Il2CppLast.UI.Touch.ConfigCommandController;
using ConfigCommandController_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandController;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using BattleAbilityInfomationContentView_Touch = Il2CppLast.UI.Touch.BattleAbilityInfomationContentView;
using BattleAbilityInfomationContentView_KeyInput = Il2CppLast.UI.KeyInput.BattleAbilityInfomationContentView;
using BattleAbilityInfomationContentController_Touch = Il2CppLast.UI.Touch.BattleAbilityInfomationContentController;
using BattleAbilityInfomationContentController_KeyInput = Il2CppLast.UI.KeyInput.BattleAbilityInfomationContentController;


namespace FFV_ScreenReader.Menus
{
    public static class MenuTextDiscovery
    {
        public static System.Collections.IEnumerator WaitAndReadCursor(GameCursor cursor, string direction, int count, bool isLoop)
        {
            // Small delay to let UI update, but shorter than a full frame
            yield return new UnityEngine.WaitForSeconds(0.01f);

            try
            {
                if (cursor == null)
                {
                    MelonLogger.Msg("Cursor is null, skipping");
                    yield break;
                }

                if (cursor.gameObject == null)
                {
                    MelonLogger.Msg("Cursor GameObject is null, skipping");
                    yield break;
                }

                if (cursor.transform == null)
                {
                    MelonLogger.Msg("Cursor transform is null, skipping");
                    yield break;
                }
                
                var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                MelonLogger.Msg($"=== {direction} called (delayed) ===");
                MelonLogger.Msg($"Scene: {sceneName}");
                MelonLogger.Msg($"Cursor Index: {cursor.Index}");
                MelonLogger.Msg($"Cursor GameObject: {cursor.gameObject?.name ?? "null"}");
                MelonLogger.Msg($"Count: {count}, IsLoop: {isLoop}");
                
                string menuText = TryAllStrategies(cursor);

                if (!string.IsNullOrEmpty(menuText))
                {
                    string configValue = ConfigMenuReader.FindConfigValueText(cursor.transform, cursor.Index);
                    if (configValue != null)
                    {
                        MelonLogger.Msg($"Found config value: '{configValue}'");
                        string fullText = $"{menuText}: {configValue}";
                        FFV_ScreenReaderMod.SpeakText(fullText);
                    }
                    else
                    {
                        FFV_ScreenReaderMod.SpeakText(menuText);
                    }
                }
                else if (menuText == null)
                {
                    // Only log if no strategy matched (null)
                    // Empty string means a strategy handled it silently (e.g., target selection)
                    MelonLogger.Msg("No menu text found in hierarchy");
                }

                MelonLogger.Msg("========================");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in delayed cursor read: {ex.Message}");
            }
        }
        
        private static string TryAllStrategies(GameCursor cursor)
        {
            string menuText = null;

            menuText = TryReadBattleEnemyTarget(cursor);
            if (menuText != null) return menuText;

            menuText = SaveSlotReader.TryReadSaveSlot(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            menuText = CharacterSelectionReader.TryReadCharacterSelection(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            menuText = TryReadFromConfigController(cursor);
            if (menuText != null) return menuText;

            menuText = TryReadFromKeysSettingController(cursor);
            if (menuText != null) return menuText;

            menuText = TryDirectTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            menuText = TryConfigCommandView(cursor);
            if (menuText != null) return menuText;

            menuText = TryIconTextView(cursor);
            if (menuText != null) return menuText;

            menuText = KeyboardGamepadReader.TryReadSettings(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            menuText = TryInGameConfigMenu(cursor);
            if (menuText != null) return menuText;

            menuText = TryFallbackTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            return null;
        }
        
        private static string TryReadBattleEnemyTarget(GameCursor cursor)
        {
            try
            {
                // Check if target selection is active via the flag set by BattleTargetPatches
                // This is more reliable than checking for controller existence
                if (FFV_ScreenReader.Patches.BattleTargetPatches.IsTargetSelectionActive)
                {
                    // Return empty string to indicate we handled it (prevents fallback strategies)
                    // The actual announcement is done by BattleTargetPatches
                    MelonLogger.Msg("[Battle Target] Target selection active (via flag) - handled by BattleTargetPatches");
                    return "";
                }

                // Also check for item use targeting mode (both KeyInput and Touch versions)
                var keyInputItemUseController = UnityEngine.Object.FindObjectOfType<Il2CppLast.UI.KeyInput.ItemUseController>();
                if (keyInputItemUseController != null)
                {
                    MelonLogger.Msg("[Battle Target] KeyInput item use targeting active - handled by BattleTargetPatches");
                    return "";
                }

                var touchItemUseController = UnityEngine.Object.FindObjectOfType<Il2CppLast.UI.Touch.ItemUseController>();
                if (touchItemUseController != null)
                {
                    MelonLogger.Msg("[Battle Target] Touch item use targeting active - handled by BattleTargetPatches");
                    return "";
                }

                var enemyEntities = UnityEngine.Object.FindObjectsOfType<BattleEnemyEntity>();
                if (enemyEntities == null || enemyEntities.Length == 0)
                {
                    return null;
                }

                MelonLogger.Msg($"[Battle Enemy] In battle with {enemyEntities.Length} enemies, cursor at index {cursor.Index}");

                Transform current = cursor.transform;
                for (int depth = 0; depth < 10 && current != null; depth++)
                {
                    var texts = current.GetComponentsInChildren<UnityEngine.UI.Text>();
                    if (texts != null && texts.Length > 0)
                    {
                        foreach (var text in texts)
                        {
                            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                            {
                                MelonLogger.Msg($"[Battle Enemy] Depth {depth}, Text on '{text.gameObject.name}': '{text.text}'");
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading battle enemy target: {ex.Message}");
            }

            return null;
        }
        
        private static string TryReadFromConfigController(GameCursor cursor)
        {
            try
            {
                if (IsCursorInDialog(cursor.transform))
                {
                    MelonLogger.Msg("Cursor is in dialog, skipping config controller");
                    return null;
                }

                int cursorIndex = cursor.Index;

                var controllerTouch = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_Touch>();
                if (controllerTouch != null && controllerTouch.CommandList != null)
                {
                    MelonLogger.Msg($"Found Touch ConfigActualDetailsControllerBase with {controllerTouch.CommandList.Count} commands");

                    if (cursorIndex >= 0 && cursorIndex < controllerTouch.CommandList.Count)
                    {
                        var command = controllerTouch.CommandList[cursorIndex];
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                MelonLogger.Msg($"Read name from Touch controller at index {cursorIndex}: '{menuText}'");
                                return menuText;
                            }
                        }
                    }
                }
                
                var controllerKeyInput = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_KeyInput>();
                if (controllerKeyInput != null && controllerKeyInput.CommandList != null)
                {
                    MelonLogger.Msg($"Found KeyInput ConfigActualDetailsControllerBase with {controllerKeyInput.CommandList.Count} commands");

                    if (cursorIndex >= 0 && cursorIndex < controllerKeyInput.CommandList.Count)
                    {
                        var command = controllerKeyInput.CommandList[cursorIndex];
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                MelonLogger.Msg($"Read name from KeyInput controller at index {cursorIndex}: '{menuText}'");
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
        
        private static bool IsCursorInDialog(Transform cursorTransform)
        {
            try
            {
                Transform current = cursorTransform;
                int depth = 0;
                while (current != null && depth < 15)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("popup") || name.Contains("dialog") || name.Contains("prompt") ||
                        name.Contains("message_window") || name.Contains("yesno") || name.Contains("confirm"))
                    {
                        MelonLogger.Msg($"Cursor is inside dialog: {current.name}");
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
        
        private static string TryReadFromKeysSettingController(GameCursor cursor)
        {
            try
            {
                if (IsCursorInDialog(cursor.transform))
                {
                    MelonLogger.Msg("Cursor is in dialog, skipping keys setting controller");
                    return null;
                }

                int cursorIndex = cursor.Index;
                
                var keysController = UnityEngine.Object.FindObjectOfType<ConfigKeysSettingController>();
                if (keysController == null)
                {
                    return null;
                }

                MelonLogger.Msg("Found ConfigKeysSettingController");
                
                if (keysController.keyboardCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.keyboardCommandList.Count)
                {
                    var command = keysController.keyboardCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from keyboard command list at index {cursorIndex}: '{text}'");
                            return text;
                        }
                    }
                }
                
                if (keysController.gamepadCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.gamepadCommandList.Count)
                {
                    var command = keysController.gamepadCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from gamepad command list at index {cursorIndex}: '{text}'");
                            return text;
                        }
                    }
                }
                
                if (keysController.mouseCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.mouseCommandList.Count)
                {
                    var command = keysController.mouseCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from mouse command list at index {cursorIndex}: '{text}'");
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
        
        private static string ReadKeyCommandText(ConfigControllCommandController command)
        {
            try
            {
                var textParts = new System.Collections.Generic.List<string>();
                
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
                                MelonLogger.Msg($"Localized MessageId '{command.MessageId}' to '{localizedText}'");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Could not localize MessageId '{command.MessageId}': {ex.Message}");
                    }
                }
                
                if (command.messageTexts != null && command.messageTexts.Count > 0)
                {
                    foreach (var textComponent in command.messageTexts)
                    {
                        if (textComponent != null && !string.IsNullOrWhiteSpace(textComponent.text))
                        {
                            string text = textComponent.text.Trim();
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
                        MelonLogger.Msg("Current gameObject is null, breaking hierarchy walk");
                        break;
                    }
                    
                    var text = current.GetComponent<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        string menuText = text.text;
                        MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (direct)");
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
        
        private static string TryConfigCommandView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    var configViewTouch = current.GetComponent<ConfigCommandView_Touch>();
                    if (configViewTouch != null && configViewTouch.nameText?.text != null)
                    {
                        string menuText = configViewTouch.nameText.text.Trim();
                        MelonLogger.Msg($"Found menu text: '{menuText}' from Touch ConfigCommandView.nameText");
                        return menuText;
                    }
                    
                    var configViewKeyInput = current.GetComponent<ConfigCommandView_KeyInput>();
                    if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                    {
                        string menuText = configViewKeyInput.nameText.text.Trim();
                        MelonLogger.Msg($"Found menu text: '{menuText}' from KeyInput ConfigCommandView.nameText");
                        return menuText;
                    }
                    
                    if (current.parent != null)
                    {
                        configViewTouch = current.parent.GetComponent<ConfigCommandView_Touch>();
                        if (configViewTouch != null && configViewTouch.nameText?.text != null)
                        {
                            string menuText = configViewTouch.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from parent Touch ConfigCommandView.nameText");
                            return menuText;
                        }

                        configViewKeyInput = current.parent.GetComponent<ConfigCommandView_KeyInput>();
                        if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                        {
                            string menuText = configViewKeyInput.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from parent KeyInput ConfigCommandView.nameText");
                            return menuText;
                        }
                    }
                    
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
        
        private static string TryConfigRootMenu(Transform configRoot, int cursorIndex)
        {
            try
            {
                var content = configRoot.GetComponentInChildren<Transform>()?.Find("MaskObject/Scroll View/Viewport/Content");
                if (content != null && cursorIndex >= 0 && cursorIndex < content.childCount)
                {
                    MelonLogger.Msg($"In-game config: Found content with {content.childCount} items, cursor at {cursorIndex}");

                    var configItem = content.GetChild(cursorIndex);
                    if (configItem != null && configItem.gameObject != null)
                    {
                        MelonLogger.Msg($"Config item name: {configItem.name}");

                        var rootChild = configItem.Find("root");
                        if (rootChild != null)
                        {
                            var rootConfigViewTouch = rootChild.GetComponent<ConfigCommandView_Touch>();
                            if (rootConfigViewTouch != null && rootConfigViewTouch.nameText?.text != null)
                            {
                                string menuText = rootConfigViewTouch.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text from root Touch ConfigCommandView: '{menuText}'");
                                return menuText;
                            }

                            var rootConfigViewKeyInput = rootChild.GetComponent<ConfigCommandView_KeyInput>();
                            if (rootConfigViewKeyInput != null && rootConfigViewKeyInput.nameText?.text != null)
                            {
                                string menuText = rootConfigViewKeyInput.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text from root KeyInput ConfigCommandView: '{menuText}'");
                                return menuText;
                            }
                        }
                        
                        var itemConfigViewTouch = configItem.GetComponentInChildren<ConfigCommandView_Touch>();
                        if (itemConfigViewTouch != null && itemConfigViewTouch.nameText?.text != null)
                        {
                            string menuText = itemConfigViewTouch.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item Touch ConfigCommandView");
                            return menuText;
                        }
                        
                        var itemConfigViewKeyInput = configItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                        if (itemConfigViewKeyInput != null && itemConfigViewKeyInput.nameText?.text != null)
                        {
                            string menuText = itemConfigViewKeyInput.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item KeyInput ConfigCommandView");
                            return menuText;
                        }
                        
                        var allTexts = configItem.GetComponentsInChildren<UnityEngine.UI.Text>();
                        MelonLogger.Msg($"Found {allTexts.Length} text components in config item:");
                        foreach (var text in allTexts)
                        {
                            if (!string.IsNullOrEmpty(text.text?.Trim()))
                            {
                                MelonLogger.Msg($"  - {text.name}: '{text.text}'");
                            }
                        }
                        
                        foreach (var text in allTexts)
                        {
                            if (text.text == "Battle Type" && cursorIndex > 0)
                                continue;
                            
                            if (text.name.Contains("command_name") || text.name.Contains("nameText") || text.name == "last_text")
                            {
                                if (!string.IsNullOrEmpty(text.text?.Trim()))
                                {
                                    string menuText = text.text.Trim();
                                    MelonLogger.Msg($"Found menu text from {text.name}: '{menuText}'");
                                    return menuText;
                                }
                            }
                        }
                        
                        var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                        {
                            string menuText = configText.text;
                            MelonLogger.Msg($"Found menu text (fallback): '{menuText}'");
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
        
        private static string TryIconTextView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                if (current == null) return null;

                // robust check: If we are in a menu handled by ItemMenuPatches, skip MenuTextDiscovery
                // ItemListController handles KeyInput item lists (items, magic, abilities, etc.)
                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.ItemListController>() != null)
                {
                    MelonLogger.Msg($"Skipping IconTextView - handled by ItemListController (ItemMenuPatches)");
                    return null;
                }

                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.EquipmentInfoWindowController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by EquipmentInfoWindowController (ItemMenuPatches)");
                     return null;
                }

                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.EquipmentSelectWindowController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by EquipmentSelectWindowController (ItemMenuPatches)");
                     return null;
                }

                // Battle Controllers exclusions
                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleCommandSelectController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by BattleCommandSelectController (BattleCommandPatches)");
                     return null;
                }
                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleItemInfomationController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by BattleItemInfomationController (BattleCommandPatches)");
                     return null;
                }
                if (current.GetComponentInParent<Il2CppSerial.FF5.UI.KeyInput.BattleQuantityAbilityInfomationController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by BattleQuantityAbilityInfomationController (BattleCommandPatches)");
                     return null;
                }
                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.ItemUseController>() != null)
                {
                     MelonLogger.Msg($"Skipping IconTextView - handled by ItemUseController (BattleCommandPatches)");
                     return null;
                }
                


                int hierarchyDepth = 0;
                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        MelonLogger.Msg("Current gameObject is null in IconTextView check");
                        break;
                    }
                    
                    var iconTextView = current.GetComponent<IconTextView>();
                    if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                    {
                        string menuText = iconTextView.nameText.text.Trim();
                        if (!string.IsNullOrEmpty(menuText))
                        {
                            MelonLogger.Msg($"Found menu text: '{menuText}' from IconTextView.nameText");
                            return menuText;
                        }
                    }
                    
                    Transform contentList = FindContentList(current);
                    if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                    {
                        MelonLogger.Msg($"Found Content list with {contentList.childCount} children, cursor at index {cursor.Index}");
                        Transform selectedChild = contentList.GetChild(cursor.Index);

                        if (selectedChild != null)
                        {
                            iconTextView = selectedChild.GetComponentInChildren<IconTextView>();
                            if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                            {
                                string menuText = iconTextView.nameText.text.Trim();
                                if (!string.IsNullOrEmpty(menuText))
                                {
                                    MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] IconTextView.nameText");
                                    return menuText;
                                }
                            }
                            
                            var battleAbilityView = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentView_KeyInput>();
                            if (battleAbilityView != null)
                            {
                                if (battleAbilityView.IconTextView != null &&
                                    battleAbilityView.IconTextView.nameText != null &&
                                    battleAbilityView.IconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityView.IconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityView.iconTextView");
                                        return menuText;
                                    }
                                }

                                //if (battleAbilityView.abilityIconText != null &&
                                //    battleAbilityView.abilityIconText.nameText != null &&
                                //    battleAbilityView.abilityIconText.nameText.text != null)
                                //{
                                //    string menuText = battleAbilityView.abilityIconText.nameText.text.Trim();
                                //    if (!string.IsNullOrEmpty(menuText))
                                //    {
                                //        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityView.abilityIconText");
                                //        return menuText;
                                //    }
                                //}
                            }
                            
                            var battleAbilityController = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentController_KeyInput>();
                            if (battleAbilityController != null && battleAbilityController.view != null)
                            {
                                if (battleAbilityController.view.IconTextView != null &&
                                    battleAbilityController.view.IconTextView.nameText != null &&
                                    battleAbilityController.view.IconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityController.view.IconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityController.view.iconTextView");
                                        return menuText;
                                    }
                                }

                                //if (battleAbilityController.view.abilityIconText != null &&
                                //    battleAbilityController.view.abilityIconText.nameText != null &&
                                //    battleAbilityController.view.abilityIconText.nameText.text != null)
                                //{
                                //    string menuText = battleAbilityController.view.abilityIconText.nameText.text.Trim();
                                //    if (!string.IsNullOrEmpty(menuText))
                                //    {
                                //        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityController.view.abilityIconText");
                                //        return menuText;
                                //    }
                                //}
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
        
        private static string TryInGameConfigMenu(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                    {
                        MelonLogger.Msg($"Found in-game list structure: {current.name}");
                        
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                        {
                            MelonLogger.Msg($"Found content list with {contentList.childCount} items, cursor at {cursor.Index}");

                            var menuItem = contentList.GetChild(cursor.Index);
                            MelonLogger.Msg($"Menu item at index {cursor.Index}: {menuItem.name}");
                            
                            var commandController = menuItem.GetComponent<ConfigCommandController_KeyInput>();
                            if (commandController == null)
                            {
                                commandController = menuItem.GetComponentInChildren<ConfigCommandController_KeyInput>();
                            }

                            if (commandController != null)
                            {
                                MelonLogger.Msg("Found ConfigCommandController");
                                
                                if (commandController.view != null && commandController.view.nameText != null)
                                {
                                    string menuText = commandController.view.nameText.text.Trim();
                                    MelonLogger.Msg($"Got text from ConfigCommandController.view.nameText: '{menuText}'");
                                    return menuText;
                                }
                            }
                            
                            var commandViewTouch = menuItem.GetComponentInChildren<ConfigCommandView_Touch>();
                            if (commandViewTouch != null && commandViewTouch.nameText != null)
                            {
                                string menuText = commandViewTouch.nameText.text.Trim();
                                MelonLogger.Msg($"Got text from Touch ConfigCommandView.nameText: '{menuText}'");
                                return menuText;
                            }
                            
                            var commandViewKeyInput = menuItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                            if (commandViewKeyInput != null && commandViewKeyInput.nameText != null)
                            {
                                string menuText = commandViewKeyInput.nameText.text.Trim();
                                MelonLogger.Msg($"Got text from KeyInput ConfigCommandView.nameText: '{menuText}'");
                                return menuText;
                            }
                            
                            var foundText = TextUtils.FindFirstText(menuItem, t =>
                            {
                                if (string.IsNullOrEmpty(t.text?.Trim()))
                                    return false;
                                var textValue = t.text.Trim();
                                return !System.Text.RegularExpressions.Regex.IsMatch(textValue, @"^\d+%?$|^On$|^Off$|^Active$|^Wait$");
                            });

                            if (foundText != null)
                            {
                                string menuText = foundText.text.Trim();
                                MelonLogger.Msg($"Got text from Text component: '{menuText}'");
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
        
        private static string TryFallbackTextSearch(Transform cursorTransform)
        {
            try
            {
                Transform current = cursorTransform;
                if (current == null) return null;

                // robust check: If we are in a menu handled by ItemMenuPatches or BattleCommandPatches, skip MenuTextDiscovery
                if (current.GetComponentInParent<Il2CppLast.UI.KeyInput.ItemListController>() != null ||
                    current.GetComponentInParent<Il2CppLast.UI.KeyInput.EquipmentInfoWindowController>() != null ||
                    current.GetComponentInParent<Il2CppLast.UI.KeyInput.EquipmentSelectWindowController>() != null ||
                    current.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleCommandSelectController>() != null ||
                    current.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleItemInfomationController>() != null ||
                    current.GetComponentInParent<Il2CppSerial.FF5.UI.KeyInput.BattleQuantityAbilityInfomationController>() != null ||
                    current.GetComponentInParent<Il2CppLast.UI.KeyInput.ItemUseController>() != null)
                {
                    MelonLogger.Msg($"Skipping fallback search - handled by specialized controller");
                    return null;
                }

                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        MelonLogger.Msg("Current gameObject is null in fallback check");
                        break;
                    }

                    var text = current.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        string menuText = text.text;
                        MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (fallback)");
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
        
        private static Transform FindContentList(Transform root)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name == "Content" && t.parent != null &&
                    (t.parent.name == "Viewport" || t.parent.parent?.name == "Scroll View"))
                {
                    return t;
                }
            }
            return null;
        }
    }
}