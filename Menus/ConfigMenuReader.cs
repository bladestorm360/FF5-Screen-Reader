using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Touch;
using UnityEngine;
using MelonLoader;
using System;
using FFV_ScreenReader.Core;
using ConfigCommandView_Touch = Il2CppLast.UI.Touch.ConfigCommandView;
using ConfigCommandView_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandView;
using ConfigCommandController_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandController;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;

namespace FFV_ScreenReader.Menus
{
    public static class ConfigMenuReader
    {
        /// <summary>
        /// Find config value directly from a ConfigCommandController instance.
        /// This is used by the controller-based patch system.
        /// </summary>
        public static string FindConfigValueFromController(ConfigCommandController_KeyInput controller)
        {
            try
            {
                if (controller == null)
                {
                    return null;
                }

                return GetValueFromKeyInputCommand(controller);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading config value from controller: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds and returns the current value text for a config option.
        /// Returns null if not in a config menu or no value found.
        /// For slider options, returns the percentage value.
        /// </summary>
        public static string FindConfigValueText(Transform transform, int index)
        {
            try
            {
                // Try KeyInput controller first (keyboard/gamepad mode)
                var keyInputController = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_KeyInput>();
                if (keyInputController != null && keyInputController.CommandList != null)
                {
                    if (index >= 0 && index < keyInputController.CommandList.Count)
                    {
                        var command = keyInputController.CommandList[index];
                        string value = GetValueFromKeyInputCommand(command);
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }

                // Try Touch controller (touch mode)
                var touchController = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_Touch>();
                if (touchController != null && touchController.CommandList != null)
                {
                    if (index >= 0 && index < touchController.CommandList.Count)
                    {
                        var command = touchController.CommandList[index];
                        string value = GetValueFromTouchCommand(command);
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }

                // Fallback: search the hierarchy for value text
                return FindValueTextInHierarchy(transform);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding config value text: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a slider value to percentage based on its min/max range.
        /// </summary>
        public static string GetSliderPercentage(UnityEngine.UI.Slider slider)
        {
            if (slider == null) return null;

            float min = slider.minValue;
            float max = slider.maxValue;
            float current = slider.value;

            // Calculate percentage based on range
            float range = max - min;
            if (range <= 0) return "0%";

            float percentage = ((current - min) / range) * 100f;
            int roundedPercentage = (int)Math.Round(percentage);

            return $"{roundedPercentage}%";
        }

        /// <summary>
        /// Gets the value from a KeyInput ConfigCommandController.
        /// </summary>
        private static string GetValueFromKeyInputCommand(ConfigCommandController_KeyInput command)
        {
            if (command == null || command.view == null)
                return null;

            var view = command.view;

            // Check arrow change text (for toggle/selection options like Battle Type)
            if (view.ArrowSelectTypeRoot != null && view.ArrowSelectTypeRoot.activeSelf)
            {
                var arrowText = GetArrowChangeTextKeyInput(view);
                if (!string.IsNullOrEmpty(arrowText))
                {
                    MelonLogger.Msg($"[ConfigMenuReader] Found arrow value: '{arrowText}'");
                    return arrowText;
                }
            }

            // Check slider value (for volume sliders) - always use percentage
            if (view.SliderTypeRoot != null && view.SliderTypeRoot.activeSelf)
            {
                if (view.Slider != null)
                {
                    string percentage = GetSliderPercentage(view.Slider);
                    if (!string.IsNullOrEmpty(percentage))
                    {
                        MelonLogger.Msg($"[ConfigMenuReader] Found slider percentage: '{percentage}'");
                        return percentage;
                    }
                }
            }

            // Check dropdown (for language selection, etc.)
            if (view.DropDownTypeRoot != null && view.DropDownTypeRoot.activeSelf)
            {
                if (view.DropDown != null)
                {
                    var dropdown = view.DropDown;
                    if (dropdown.options != null && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
                    {
                        string dropdownText = dropdown.options[dropdown.value].text;
                        if (!string.IsNullOrEmpty(dropdownText))
                        {
                            MelonLogger.Msg($"[ConfigMenuReader] Found dropdown value: '{dropdownText}'");
                            return dropdownText;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the arrow change text from a KeyInput ConfigCommandView using reflection or direct access.
        /// </summary>
        private static string GetArrowChangeTextKeyInput(ConfigCommandView_KeyInput view)
        {
            try
            {
                // Try to access the arrowChangeText field via the view's child transforms
                var arrowRoot = view.ArrowSelectTypeRoot;
                if (arrowRoot != null)
                {
                    var texts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string value = text.text.Trim();
                            // Filter out arrow characters and empty text
                            if (value != "<" && value != ">" && value != "◀" && value != "▶")
                            {
                                return value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting arrow change text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets the value from a Touch ConfigCommandController.
        /// </summary>
        private static string GetValueFromTouchCommand(Il2CppLast.UI.Touch.ConfigCommandController command)
        {
            if (command == null || command.view == null)
                return null;

            var view = command.view;

            // Check arrow button type (for toggle/selection options)
            if (view.ArrowButtonTypeRoot != null && view.ArrowButtonTypeRoot.activeSelf)
            {
                var arrowText = GetArrowChangeTextTouch(view);
                if (!string.IsNullOrEmpty(arrowText))
                {
                    MelonLogger.Msg($"[ConfigMenuReader] Found touch arrow value: '{arrowText}'");
                    return arrowText;
                }
            }

            // Check slider value - use percentage from slider component
            if (view.SliderTypeRoot != null && view.SliderTypeRoot.activeSelf)
            {
                // Try to find the slider in the slider root
                var slider = view.SliderTypeRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                if (slider != null)
                {
                    string percentage = GetSliderPercentage(slider);
                    if (!string.IsNullOrEmpty(percentage))
                    {
                        MelonLogger.Msg($"[ConfigMenuReader] Found touch slider percentage: '{percentage}'");
                        return percentage;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the arrow change text from a Touch ConfigCommandView.
        /// </summary>
        private static string GetArrowChangeTextTouch(ConfigCommandView_Touch view)
        {
            try
            {
                var arrowRoot = view.ArrowButtonTypeRoot;
                if (arrowRoot != null)
                {
                    var texts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string value = text.text.Trim();
                            if (value != "<" && value != ">" && value != "◀" && value != "▶")
                            {
                                return value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting touch arrow change text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Fallback: searches the transform hierarchy for value text.
        /// </summary>
        private static string FindValueTextInHierarchy(Transform transform)
        {
            try
            {
                Transform current = transform;
                int depth = 0;

                while (current != null && depth < 10)
                {
                    // Look for ConfigCommandView components
                    var keyInputView = current.GetComponent<ConfigCommandView_KeyInput>();
                    if (keyInputView != null)
                    {
                        // Check arrow text
                        string value = GetArrowChangeTextKeyInput(keyInputView);
                        if (!string.IsNullOrEmpty(value)) return value;

                        // Check slider with percentage
                        if (keyInputView.SliderTypeRoot != null && keyInputView.SliderTypeRoot.activeSelf)
                        {
                            if (keyInputView.Slider != null)
                            {
                                value = GetSliderPercentage(keyInputView.Slider);
                                if (!string.IsNullOrEmpty(value)) return value;
                            }
                        }
                    }

                    var touchView = current.GetComponent<ConfigCommandView_Touch>();
                    if (touchView != null)
                    {
                        // Check arrow text
                        string value = GetArrowChangeTextTouch(touchView);
                        if (!string.IsNullOrEmpty(value)) return value;

                        // Check slider with percentage
                        if (touchView.SliderTypeRoot != null && touchView.SliderTypeRoot.activeSelf)
                        {
                            var slider = touchView.SliderTypeRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                            if (slider != null)
                            {
                                value = GetSliderPercentage(slider);
                                if (!string.IsNullOrEmpty(value)) return value;
                            }
                        }
                    }

                    current = current.parent;
                    depth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in hierarchy search: {ex.Message}");
            }

            return null;
        }
    }
}