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
    /// Patches for config/options menu accessibility in FF5.
    /// Announces option values when values change via left/right arrows.
    /// </summary>
    public static class ConfigMenuPatches
    {
    }

    /// <summary>
    /// Patch for SwitchArrowSelectTypeProcess - called when left/right arrows change toggle options.
    /// Only announces when the value actually changes.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase), "SwitchArrowSelectTypeProcess")]
    public static class ConfigActualDetails_SwitchArrowSelectType_Patch
    {
        private static string lastArrowValue = "";

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase __instance,
            ConfigCommandController controller,
            Key key)
        {
            try
            {
                if (controller == null) return;

                // Small delay to let the UI update
                CoroutineManager.StartManaged(DelayedAnnounce(controller));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SwitchArrowSelectTypeProcess patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedAnnounce(ConfigCommandController controller)
        {
            yield return new WaitForSeconds(0.05f);

            try
            {
                if (controller == null || controller.view == null) yield break;

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
                                if (textValue == lastArrowValue)
                                {
                                    yield break;
                                }

                                lastArrowValue = textValue;
                                MelonLogger.Msg($"[ConfigMenu] Arrow value changed: {textValue}");
                                FFV_ScreenReaderMod.SpeakText(textValue);
                                yield break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed arrow announce: {ex.Message}");
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
        private static string lastSliderPercentage = "";
        private static ConfigCommandController lastController = null;

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

                // Only announce if value changed for the SAME controller
                // This prevents announcements when navigating between different sliders
                if (controller == lastController && percentage == lastSliderPercentage)
                {
                    return;
                }

                // If we moved to a different controller (different option), don't announce
                // Let MenuTextDiscovery handle the full "Name: Value" announcement
                if (controller != lastController)
                {
                    lastController = controller;
                    lastSliderPercentage = percentage;
                    return;
                }

                // Same controller, value changed - announce just the new value
                lastSliderPercentage = percentage;

                MelonLogger.Msg($"[ConfigMenu] Slider value changed: {percentage}");
                FFV_ScreenReaderMod.SpeakText(percentage);
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
        private static string lastTouchArrowValue = "";

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase __instance,
            Il2CppLast.UI.Touch.ConfigCommandController controller,
            int value)
        {
            try
            {
                if (controller == null || controller.view == null) return;

                // Small delay to let the UI update
                CoroutineManager.StartManaged(DelayedAnnounceTouch(controller));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Touch SwitchArrowTypeProcess patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedAnnounceTouch(Il2CppLast.UI.Touch.ConfigCommandController controller)
        {
            yield return new WaitForSeconds(0.05f);

            try
            {
                var view = controller.view;
                if (view == null) yield break;

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
                                if (textValue == lastTouchArrowValue)
                                {
                                    yield break;
                                }

                                lastTouchArrowValue = textValue;
                                MelonLogger.Msg($"[ConfigMenu] Touch arrow value changed: {textValue}");
                                FFV_ScreenReaderMod.SpeakText(textValue);
                                yield break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed touch announce: {ex.Message}");
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
        private static string lastTouchSliderPercentage = "";
        private static Il2CppLast.UI.Touch.ConfigCommandController lastTouchController = null;

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

                // Only announce if value changed for the SAME controller
                // This prevents announcements when navigating between different sliders
                if (controller == lastTouchController && percentage == lastTouchSliderPercentage)
                {
                    return;
                }

                // If we moved to a different controller (different option), don't announce
                // Let MenuTextDiscovery handle the full "Name: Value" announcement
                if (controller != lastTouchController)
                {
                    lastTouchController = controller;
                    lastTouchSliderPercentage = percentage;
                    return;
                }

                // Same controller, value changed - announce just the new value
                lastTouchSliderPercentage = percentage;

                MelonLogger.Msg($"[ConfigMenu] Touch slider value changed: {percentage}");
                FFV_ScreenReaderMod.SpeakText(percentage);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Touch SwitchSliderTypeProcess patch: {ex.Message}");
            }
        }
    }
}
