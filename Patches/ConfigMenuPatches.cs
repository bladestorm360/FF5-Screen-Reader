using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Menus;
using FFV_ScreenReader.Utils;
using UnityEngine;
using Key = Il2CppSystem.Input.Key;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Tracks config menu active state. Prevents stale config controllers
    /// from leaking text into other menus (e.g., main menu after returning from config).
    /// </summary>
    public static class ConfigMenuState
    {
        public static bool IsActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.CONFIG_MENU);
            set => MenuStateRegistry.SetActive(MenuStateRegistry.CONFIG_MENU, value);
        }

        public static void ClearState()
        {
            IsActive = false;
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_COMMAND);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_KEYS_SETTING);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_ARROW_VALUE);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_SLIDER_CONTROLLER);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_SLIDER_PERCENTAGE);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_TOUCH_ARROW_VALUE);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_TOUCH_SLIDER_CONTROLLER);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_TOUCH_SLIDER_PERCENTAGE);
        }
    }

    /// <summary>
    /// Controller-based patches for config menu navigation.
    /// Announces menu items directly from ConfigCommandController when navigating with up/down arrows.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigCommandController), nameof(Il2CppLast.UI.KeyInput.ConfigCommandController.SetFocus))]
    public static class ConfigCommandController_SetFocus_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.ConfigCommandController __instance, bool isFocus)
        {
            try
            {
                // Only announce when gaining focus (not losing it)
                if (!isFocus)
                {
                    return;
                }

                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Don't announce if controller is not active (prevents announcements during scene loading)
                if (!__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Mark config menu as active so TryReadFromConfigController works
                ConfigMenuState.IsActive = true;

                // Clear stale popup state (popup may have been dismissed without Close())
                if (PopupState.IsConfirmationPopupActive)
                {
                    PopupState.Clear();
                    AnnouncementDeduplicator.Reset(AnnouncementContexts.CONFIG_COMMAND);
                }

                // Clear all other menu trackers so I key falls through to config tooltip
                JobAbilityTrackerHelper.ClearAllTrackers();
                ItemMenuTracker.ClearState();

                // Get the view which contains the localized text
                var view = __instance.view;
                if (view == null)
                {
                    return;
                }

                // Get the name text (localized)
                var nameText = view.nameText;
                if (nameText == null || string.IsNullOrWhiteSpace(nameText.text))
                {
                    return;
                }

                string menuText = nameText.text.Trim();

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_COMMAND, menuText))
                {
                    return;
                }

                // Also try to get the current value for this config option
                string configValue = ConfigMenuReader.FindConfigValueFromController(__instance);

                string announcement = menuText;
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    announcement = $"{menuText}: {configValue}";
                }

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigCommandController.SetFocus patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for keyboard/gamepad/mouse control settings.
    /// Announces action name and current key binding when navigating.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigKeysSettingController), nameof(Il2CppLast.UI.KeyInput.ConfigKeysSettingController.SelectContent),
        new Type[] { typeof(int), typeof(Il2CppLast.UI.CustomScrollView), typeof(Il2CppLast.UI.Cursor),
                     typeof(Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.UI.KeyInput.ConfigControllCommandController>),
                     typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType) })]
    public static class ConfigKeysSettingController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance, int index,
            Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.UI.KeyInput.ConfigControllCommandController> contentList)
        {
            try
            {
                // Safety checks
                if (__instance == null || contentList == null)
                {
                    return;
                }

                var command = SelectContentHelper.TryGetItem(
                    contentList.TryCast<Il2CppSystem.Collections.Generic.List<Il2CppLast.UI.KeyInput.ConfigControllCommandController>>(),
                    index);
                if (command == null) return;

                var textParts = new System.Collections.Generic.List<string>();

                // Read action name from the view's nameTexts
                if (command.view != null && command.view.nameTexts != null && command.view.nameTexts.Count > 0)
                {
                    foreach (var textComp in command.view.nameTexts)
                    {
                        if (textComp != null && !string.IsNullOrWhiteSpace(textComp.text))
                        {
                            string text = textComp.text.Trim();
                            if (!text.StartsWith("MENU_") && !textParts.Contains(text))
                            {
                                textParts.Add(text);
                            }
                        }
                    }
                }

                // Read key bindings from keyboardIconController.view (only works for keyboard settings)
                if (command.keyboardIconController != null && command.keyboardIconController.view != null)
                {
                    // Read from iconTextList - contains the actual key names (e.g., "Enter", "Backspace")
                    if (command.keyboardIconController.view.iconTextList != null)
                    {
                        for (int i = 0; i < command.keyboardIconController.view.iconTextList.Count; i++)
                        {
                            var iconText = command.keyboardIconController.view.iconTextList[i];
                            if (iconText != null && !string.IsNullOrWhiteSpace(iconText.text))
                            {
                                string text = iconText.text.Trim();
                                if (!textParts.Contains(text))
                                {
                                    textParts.Add(text);
                                }
                            }
                        }
                    }
                }

                // Note: Gamepad button bindings are not readable as text (displayed as button sprites)
                // We only announce the action name for gamepad settings

                if (textParts.Count == 0)
                {
                    return;
                }

                string announcement = string.Join(" ", textParts);

                // Skip duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_KEYS_SETTING, announcement))
                {
                    return;
                }

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigKeysSettingController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for SwitchArrowSelectTypeProcess - called when left/right arrows change toggle options.
    /// Only announces when the value actually changes.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase), "SwitchArrowSelectTypeProcess")]
    public static class ConfigActualDetails_SwitchArrowSelectType_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase __instance,
            ConfigCommandController controller,
            Key key)
        {
            try
            {
                if (controller == null || controller.view == null) return;

                var view = controller.view;

                // Get arrow select value
                if (view.ArrowSelectTypeRoot != null && view.ArrowSelectTypeRoot.activeSelf)
                {
                    var arrowRoot = view.ArrowSelectTypeRoot;
                    var texts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string textValue = text.text.Trim();
                            // Filter out arrow characters
                            if (textValue != "<" && textValue != ">" && textValue != "◀" && textValue != "▶" &&
                                textValue != "←" && textValue != "→")
                            {
                                // Only announce if value changed
                                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_ARROW_VALUE, textValue)) return;

                                FFV_ScreenReaderMod.SpeakText(textValue, interrupt: true);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SwitchArrowSelectTypeProcess patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for SwitchSliderTypeProcess - called when left/right arrows change slider values.
    /// Only announces when the value actually changes for the SAME option.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase), "SwitchSliderTypeProcess")]
    public static class ConfigActualDetails_SwitchSliderType_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase __instance,
            ConfigCommandController controller,
            Key key)
        {
            try
            {
                if (controller == null || controller.view == null) return;

                var view = controller.view;
                if (view.Slider == null) return;

                // Calculate percentage using proper min/max range
                string percentage = ConfigMenuReader.GetSliderPercentage(view.Slider);
                if (string.IsNullOrEmpty(percentage)) return;

                // Track controller and percentage separately
                bool controllerChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_SLIDER_CONTROLLER, controller);
                bool percentageChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_SLIDER_PERCENTAGE, percentage);

                // Both unchanged - skip
                if (!controllerChanged && !percentageChanged) return;

                // If we moved to a different controller (different option), don't announce
                // Let MenuTextDiscovery handle the full "Name: Value" announcement
                if (controllerChanged) return;

                // Same controller, value changed - announce just the new value
                FFV_ScreenReaderMod.SpeakText(percentage, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SwitchSliderTypeProcess patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Touch mode arrow button handling.
    /// Only announces when the value actually changes.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase), "SwitchArrowTypeProcess")]
    public static class ConfigActualDetailsTouch_SwitchArrowType_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase __instance,
            Il2CppLast.UI.Touch.ConfigCommandController controller,
            int value)
        {
            try
            {
                if (controller == null || controller.view == null) return;

                var view = controller.view;

                // Check arrow button type
                if (view.ArrowButtonTypeRoot != null && view.ArrowButtonTypeRoot.activeSelf)
                {
                    var texts = view.ArrowButtonTypeRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string textValue = text.text.Trim();
                            if (textValue != "<" && textValue != ">" && textValue != "◀" && textValue != "▶" &&
                                textValue != "←" && textValue != "→")
                            {
                                // Only announce if value changed
                                if (!AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_TOUCH_ARROW_VALUE, textValue)) return;

                                FFV_ScreenReaderMod.SpeakText(textValue, interrupt: true);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Touch SwitchArrowTypeProcess patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Touch mode slider handling.
    /// Only announces when the value actually changes for the SAME option.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase), "SwitchSliderTypeProcess")]
    public static class ConfigActualDetailsTouch_SwitchSliderType_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase __instance,
            Il2CppLast.UI.Touch.ConfigCommandController controller,
            float value)
        {
            try
            {
                if (controller == null || controller.view == null) return;

                var view = controller.view;
                if (view.SliderTypeRoot == null) return;

                // Find the slider in the slider root
                var slider = view.SliderTypeRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                if (slider == null) return;

                // Calculate percentage using proper min/max range
                string percentage = ConfigMenuReader.GetSliderPercentage(slider);
                if (string.IsNullOrEmpty(percentage)) return;

                // Track controller and percentage separately
                bool controllerChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_TOUCH_SLIDER_CONTROLLER, controller);
                bool percentageChanged = AnnouncementDeduplicator.ShouldAnnounce(AnnouncementContexts.CONFIG_TOUCH_SLIDER_PERCENTAGE, percentage);

                // Both unchanged - skip
                if (!controllerChanged && !percentageChanged) return;

                // If we moved to a different controller (different option), don't announce
                // Let MenuTextDiscovery handle the full "Name: Value" announcement
                if (controllerChanged) return;

                // Same controller, value changed - announce just the new value
                FFV_ScreenReaderMod.SpeakText(percentage, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Touch SwitchSliderTypeProcess patch: {ex.Message}");
            }
        }
    }
}
