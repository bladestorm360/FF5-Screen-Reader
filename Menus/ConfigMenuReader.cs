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

        // Self-contained Language(enum int) → display name. The game's own GetLanguageMessage returns
        // EMPTY for the CURRENT language (it blanks the current item's label), so we map here instead.
        // Keyed on FF5's Language enum (Ja=1 … Pt=12).
        private static readonly System.Collections.Generic.Dictionary<int, string> LanguageNames = new()
        {
            { 1, "Japanese" }, { 2, "English" }, { 3, "French" }, { 4, "Italian" },
            { 5, "German" }, { 6, "Spanish" }, { 7, "Korean" }, { 8, "Chinese (Traditional)" },
            { 9, "Chinese (Simplified)" }, { 10, "Russian" }, { 11, "Thai" }, { 12, "Brazilian Portuguese" },
        };

        /// <summary>
        /// Current game language display name, mapped from MessageManager.currentLanguage. Used for the
        /// Language config row and the open dropdown's current item (whose LabelText is blank).
        /// Deliberately does NOT use the game's GetLanguageMessage, which returns empty for the current language.
        /// </summary>
        public static string GetCurrentLanguageDisplayName()
        {
            try
            {
                var mgr = Il2CppLast.Management.MessageManager.Instance;
                if (mgr == null) return null;
                int langId = (int)mgr.currentLanguage;
                return LanguageNames.TryGetValue(langId, out string name) ? name : null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Config Menu] GetCurrentLanguageDisplayName failed: {ex.Message}");
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
                // Use GameObjectCache to avoid expensive FindObjectOfType
                var keyInputController = FFV_ScreenReader.Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_KeyInput>();
                if (keyInputController == null)
                    keyInputController = FFV_ScreenReader.Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_KeyInput>();

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
                var touchController = FFV_ScreenReader.Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_Touch>();
                if (touchController == null)
                    touchController = FFV_ScreenReader.Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_Touch>();

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
        /// Gets the display value for a slider, preferring the game's sliderValueText
        /// (which shows raw values like "5" for Music/SFX, "50%" for Master Volume/Brightness).
        /// Falls back to calculating percentage from the slider range.
        /// </summary>
        public static string GetSliderDisplayValue(UnityEngine.UI.Slider slider, UnityEngine.UI.Text sliderValueText)
        {
            if (sliderValueText != null && !string.IsNullOrWhiteSpace(sliderValueText.text))
            {
                return sliderValueText.text.Trim();
            }
            return GetSliderPercentage(slider);
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
        /// True when this config row is the Language selector. Checks the command's config type
        /// first, then falls back to LanguageContentData, which only the language row carries and
        /// which survives on screens where ConfigCommandsData is not wired up.
        /// </summary>
        private static bool IsLanguageRow(ConfigCommandController_KeyInput command)
        {
            try
            {
                var cmdData = command.ConfigCommandsData;
                if (cmdData != null && cmdData.ConfigCommandType == Il2CppLast.UI.ConfigCommandType.Language)
                    return true;
            }
            catch { }

            try
            {
                return command.LanguageContentData != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the value from a KeyInput ConfigCommandController.
        /// </summary>
        private static string GetValueFromKeyInputCommand(ConfigCommandController_KeyInput command)
        {
            if (command == null || command.view == null)
                return null;

            var view = command.view;

            // Language row: its dropdown OptionData text is EMPTY for the currently-selected
            // language (the game blanks the current item's label), so read the current language
            // straight from MessageManager → "Language: <current>".
            //
            // Identified two ways, never by a localized string match. ConfigCommandsData is the
            // primary signal, but it is a separate MonoBehaviour reference that is not always
            // wired up — notably on the title-screen Options list — so LanguageContentData (which
            // only the language row carries) is checked as well. Without the second check the row
            // fell through to the dropdown branch below, read the blank label, and announced just
            // "Language" with no value.
            if (IsLanguageRow(command))
            {
                string lang = GetCurrentLanguageDisplayName();
                if (!string.IsNullOrEmpty(lang))
                    return lang;
            }

            // Check arrow change text (for toggle/selection options like Battle Type)
            if (view.ArrowSelectTypeRoot != null && view.ArrowSelectTypeRoot.activeSelf)
            {
                var arrowText = GetArrowChangeTextKeyInput(view);
                if (!string.IsNullOrEmpty(arrowText))
                {
                    return arrowText;
                }
            }

            // Check slider value (for volume sliders)
            if (view.SliderTypeRoot != null && view.SliderTypeRoot.activeSelf)
            {
                if (view.Slider != null)
                {
                    string sliderValue = GetSliderDisplayValue(view.Slider, view.sliderValueText);
                    if (!string.IsNullOrEmpty(sliderValue))
                    {
                        return sliderValue;
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
                            return dropdownText;
                        }
                    }
                }

                // A dropdown row whose selected label is blank is the language selector — the game
                // blanks the CURRENT item's label. Safety net for the IsLanguageRow checks above.
                string currentLang = GetCurrentLanguageDisplayName();
                if (!string.IsNullOrEmpty(currentLang))
                {
                    return currentLang;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the arrow change text from a KeyInput ConfigCommandView.
        /// </summary>
        private static string GetArrowChangeTextKeyInput(ConfigCommandView_KeyInput view)
        {
            return GetArrowTextFromRoot(view.ArrowSelectTypeRoot);
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
                    return arrowText;
                }
            }

            // Check slider value
            if (view.SliderTypeRoot != null && view.SliderTypeRoot.activeSelf)
            {
                // Try to find the slider in the slider root
                var slider = view.SliderTypeRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                if (slider != null)
                {
                    string sliderValue = GetSliderDisplayValue(slider, view.sliderValueText);
                    if (!string.IsNullOrEmpty(sliderValue))
                    {
                        return sliderValue;
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
            return GetArrowTextFromRoot(view.ArrowButtonTypeRoot);
        }

        /// <summary>
        /// Extracts the value text from an arrow root GameObject, filtering out arrow characters.
        /// </summary>
        private static string GetArrowTextFromRoot(GameObject arrowRoot)
        {
            try
            {
                if (arrowRoot != null)
                {
                    var texts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string value = text.text.Trim();
                            if (value != "<" && value != ">" && value != "\u25c0" && value != "\u25b6")
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

                        // Check slider
                        if (keyInputView.SliderTypeRoot != null && keyInputView.SliderTypeRoot.activeSelf)
                        {
                            if (keyInputView.Slider != null)
                            {
                                value = GetSliderDisplayValue(keyInputView.Slider, keyInputView.sliderValueText);
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

                        // Check slider
                        if (touchView.SliderTypeRoot != null && touchView.SliderTypeRoot.activeSelf)
                        {
                            var slider = touchView.SliderTypeRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                            if (slider != null)
                            {
                                value = GetSliderDisplayValue(slider, touchView.sliderValueText);
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
