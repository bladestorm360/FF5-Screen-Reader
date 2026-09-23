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

            // Clear every config re-fire guard so re-entering the menu re-announces the focused row.
            ConfigCommandController_SetFocus_Patch.ResetLastCommand();
            ConfigKeysSettingController_SelectContent_Patch.ResetLastRow();
            ConfigActualDetails_SwitchArrowSelectType_Patch.ResetLastValue();
            ConfigActualDetails_SwitchSliderType_Patch.ResetLastSlider();
            ConfigActualDetailsTouch_SwitchArrowType_Patch.ResetLastValue();
            ConfigActualDetailsTouch_SwitchSliderType_Patch.ResetLastSlider();
        }
    }

    /// <summary>
    /// Controller-based patches for config menu navigation.
    /// Announces menu items directly from ConfigCommandController when navigating with up/down arrows.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ConfigCommandController), nameof(Il2CppLast.UI.KeyInput.ConfigCommandController.SetFocus))]
    public static class ConfigCommandController_SetFocus_Patch
    {
        // SetFocus is re-asserted on the focused controller rather than fired once per cursor
        // move, so hold the last option name spoken. Cleared on menu exit (ConfigMenuState) and
        // when a confirmation popup closes, so the same row can be re-announced.
        private static string _lastCommand;

        /// <summary>Clears the last announced option so it can be re-announced.</summary>
        public static void ResetLastCommand() => _lastCommand = null;

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.UI.KeyInput.ConfigCommandController __instance, bool isFocus)
        {
            // Only announce when gaining focus (not losing it)
            if (!isFocus) return;
            Announce(__instance);
        }

        /// <summary>
        /// Announces one config row as "Name: Value, (X of Y)". Shared by SetFocus (navigation) and
        /// the title-screen Options initial-focus path; returns true when it spoke and false when
        /// the row isn't readable yet, so a settle loop can retry.
        /// </summary>
        public static bool Announce(Il2CppLast.UI.KeyInput.ConfigCommandController __instance)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return false;
                }

                // Don't announce if controller is not active (prevents announcements during scene loading)
                if (!__instance.gameObject.activeInHierarchy)
                {
                    return false;
                }

                // Mark config menu as active so TryReadFromConfigController works
                ConfigMenuState.IsActive = true;

                // Clear stale popup state (popup may have been dismissed without Close())
                if (PopupState.IsConfirmationPopupActive)
                {
                    PopupState.Clear();
                    ResetLastCommand();
                }

                // Clear all other menu trackers so I key falls through to config tooltip
                JobAbilityTrackerHelper.ClearAllTrackers();
                ItemMenuTracker.ClearState();

                // Get the view which contains the localized text
                var view = __instance.view;
                if (view == null)
                {
                    return false;
                }

                // Get the name text (localized)
                var nameText = view.nameText;
                if (nameText == null || string.IsNullOrWhiteSpace(nameText.text))
                {
                    return false;
                }

                string menuText = nameText.text.Trim();

                if (menuText == _lastCommand) return false;
                _lastCommand = menuText;

                // Also try to get the current value for this config option
                string configValue = ConfigMenuReader.FindConfigValueFromController(__instance);

                string announcement = menuText;
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    announcement = $"{menuText}: {configValue}";
                }

                // Append list position last (after the value). SetFocus gives no index, so locate
                // this command within the active details controller's CommandList by pointer.
                var (cfgIndex, cfgCount) = GetConfigCommandPosition(__instance);
                announcement = MenuPosition.Format(announcement, cfgIndex, cfgCount);

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigCommandController.SetFocus patch: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Finds the focused config command's (index, count) within the active in-game config
        /// details controller's CommandList, matching by IL2CPP pointer. Returns (-1, 0) when the
        /// list can't be resolved so MenuPosition.Format emits no position suffix.
        /// </summary>
        private static (int index, int count) GetConfigCommandPosition(Il2CppLast.UI.KeyInput.ConfigCommandController controller)
        {
            try
            {
                if (controller == null) return (-1, 0);

                var details = GameObjectCache.Get<Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase>();
                if (details == null)
                    details = GameObjectCache.Refresh<Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase>();

                if (details != null && details.CommandList != null)
                {
                    var list = details.CommandList;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].Pointer == controller.Pointer)
                            return (i, list.Count);
                    }
                }
            }
            catch { }
            return (-1, 0);
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
        // SelectContent carries a WithinRangeType — the scroll view re-invokes it on range
        // recalculation with an unchanged row.
        private static string _lastRow;

        /// <summary>Clears the last announced remap row.</summary>
        public static void ResetLastRow() => _lastRow = null;

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

                var listCast = contentList.TryCast<Il2CppSystem.Collections.Generic.List<Il2CppLast.UI.KeyInput.ConfigControllCommandController>>();
                var command = SelectContentHelper.TryGetItem(listCast, index);

                string announcement = BuildCommandAnnouncement(__instance, command);
                if (string.IsNullOrWhiteSpace(announcement)) return;

                // Append list position last (index within the remappable-action list).
                announcement = MenuPosition.Format(announcement, index, listCast != null ? listCast.Count : 0);

                if (announcement == _lastRow) return;
                _lastRow = announcement;

                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigKeysSettingController.SelectContent patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the controls-screen announcement for one row: action name + binding. Shared by the
        /// navigation read (SelectContent), the rebind read (ChangeKeySetting) and the read-only
        /// Gamepad/Keyboard Controls list (isHelpList), so all three say the same thing.
        /// </summary>
        internal static string BuildCommandAnnouncement(
            Il2CppLast.UI.KeyInput.ConfigKeysSettingController owner,
            Il2CppLast.UI.KeyInput.ConfigControllCommandController command,
            bool isHelpList = false)
        {
            if (command == null) return null;

            var textParts = new System.Collections.Generic.List<string>();

            AppendCommandName(textParts, command, isHelpList);

            // Binding text. Mouse rows render their button as a glyph with no readable text, so
            // translate the bound mouse button/wheel. Keyboard rows already carry readable key names
            // in the keyboard icon controller's iconTextList (empty on the gamepad section).
            if (command.IsMouseKey)
            {
                string mouse = ResolveMouseButtonText(command);
                if (!string.IsNullOrEmpty(mouse))
                    textParts.Add($"({mouse})");
            }
            else
            {
                AppendIconTexts(textParts, command.keyboardIconController);
            }

            // Gamepad binding — ONLY when the row actually shows a gamepad binding icon
            // (view.gamePadIconsRoot active). That excludes keyboard-section rows and non-binding rows
            // like "Reset to Defaults" / "Gamepad Controls", whose key defaults to Action and would
            // otherwise read the Confirm button.
            var gpRoot = command.view != null ? command.view.gamePadIconsRoot : null;
            if (gpRoot != null && gpRoot.activeSelf)
            {
                // Face buttons are remappable → the LIVE binding (remap- and controller-aware). The
                // fixed buttons (shoulders, triggers, sticks, Start, movement) are not in the remap
                // dictionary, so fall back to the rendered glyph sprite.
                string btn = ResolveGamepadButtonText(owner, command);
                if (string.IsNullOrEmpty(btn))
                    btn = GetGamepadGlyphLabel(command);
                if (!string.IsNullOrEmpty(btn))
                    textParts.Add($"({btn})");
            }

            return textParts.Count == 0 ? null : string.Join(" ", textParts);
        }

        /// <summary>
        /// Appends a row's action name. Remap rows carry the localized name in view.nameTexts. HELP
        /// rows (the read-only Controls list) leave nameTexts as a "New Text" placeholder and render
        /// the real name into the controller's own messageTexts, so read those, falling back to
        /// resolving MessageId through the game's localization.
        /// </summary>
        private static void AppendCommandName(
            System.Collections.Generic.List<string> textParts,
            Il2CppLast.UI.KeyInput.ConfigControllCommandController command,
            bool isHelpList)
        {
            if (isHelpList)
            {
                var msgTexts = command.messageTexts;
                if (msgTexts != null)
                {
                    for (int i = 0; i < msgTexts.Count; i++)
                    {
                        var t = msgTexts[i];
                        if (t != null && IsRealName(t.text))
                        {
                            string s = t.text.Trim();
                            if (!textParts.Contains(s)) textParts.Add(s);
                        }
                    }
                }

                // The rendered text wasn't ready — resolve the message id directly.
                if (textParts.Count == 0)
                {
                    string loc = TextUtils.StripIconMarkup(LocalizationHelper.GetGameMessage(command.MessageId));
                    if (IsRealName(loc)) textParts.Add(loc);
                }
                return;
            }

            if (command.view != null && command.view.nameTexts != null && command.view.nameTexts.Count > 0)
            {
                foreach (var textComp in command.view.nameTexts)
                {
                    if (textComp != null && !string.IsNullOrWhiteSpace(textComp.text))
                    {
                        string text = textComp.text.Trim();
                        if (!text.StartsWith("MENU_") && !textParts.Contains(text))
                            textParts.Add(text);
                    }
                }
            }
        }

        /// <summary>True if the text is a usable name (not blank or an editor placeholder).</summary>
        private static bool IsRealName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim();
            return t != "New Text" && t != "NewText" && t != "Text" && t != "Name" && t != "Label";
        }

        /// <summary>Appends an icon controller's binding labels (iconTextList) to textParts, deduped.</summary>
        private static void AppendIconTexts(System.Collections.Generic.List<string> textParts, ConfigKeyIconController icon)
        {
            var iconView = icon?.view;
            if (iconView == null || iconView.iconTextList == null) return;
            for (int i = 0; i < iconView.iconTextList.Count; i++)
            {
                var iconText = iconView.iconTextList[i];
                if (iconText != null && !string.IsNullOrWhiteSpace(iconText.text))
                {
                    string text = iconText.text.Trim();
                    // Gamepad help rows leave the keyboard icon text as a "New Text" placeholder.
                    if (IsRealName(text) && !textParts.Contains(text))
                        textParts.Add(text);
                }
            }
        }

        /// <summary>
        /// Reads a row's rendered gamepad glyph sprite (under view.gamePadIconsRoot) and maps it to a
        /// controller-aware label. Only for the FIXED buttons, which the live remap read can't resolve.
        /// </summary>
        private static string GetGamepadGlyphLabel(Il2CppLast.UI.KeyInput.ConfigControllCommandController command)
        {
            try
            {
                var gpRoot = command?.view != null ? command.view.gamePadIconsRoot : null;
                if (gpRoot == null || !gpRoot.activeSelf) return null;
                var images = gpRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                if (images == null) return null;
                for (int i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null || !img.gameObject.activeInHierarchy || img.sprite == null) continue;
                    string label = GamepadGlyphSpriteToLabel(img.sprite.name);
                    if (!string.IsNullOrEmpty(label)) return label;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Maps a controls-screen glyph sprite name ("UI_Common_&lt;Button&gt;button01") to a
        /// controller-aware label. Covers ONLY the fixed buttons; the remappable face buttons and
        /// unknowns return null so they are left to the live remap read and never locked in.
        /// </summary>
        private static string GamepadGlyphSpriteToLabel(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            if (Has(spriteName, "LBbutton")) return ControllerLabels.GetButtonLabel(SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER);
            if (Has(spriteName, "RBbutton")) return ControllerLabels.GetButtonLabel(SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER);
            if (Has(spriteName, "LTbutton")) return ControllerLabels.GetLeftTriggerLabel();
            if (Has(spriteName, "RTbutton")) return ControllerLabels.GetRightTriggerLabel();
            // Stick clicks are phrased as a CLICK ("L3"), not "LS", which reads like moving the stick.
            if (Has(spriteName, "L3button")) return ControllerLabels.GetLeftStickClickLabel();
            if (Has(spriteName, "R3button")) return ControllerLabels.GetRightStickClickLabel();
            // The mod repurposes the Menu (Start) button for the mod menu, and it can't be remapped.
            if (Has(spriteName, "Menubutton")) return ModTextTranslator.T("used for mod menu");
            if (Has(spriteName, "Backbutton") || Has(spriteName, "Selectbutton") || Has(spriteName, "Viewbutton"))
                return ControllerLabels.GetButtonLabel(SDL3.SDL_GAMEPAD_BUTTON_BACK);
            // The movement glyph. The mod repurposes the D-pad and right stick for its own
            // navigation, so only the left stick still moves the character.
            if (Has(spriteName, "Tenkeybutton") || Has(spriteName, "Dpadbutton")
                || Has(spriteName, "Crossbutton") || Has(spriteName, "Directionbutton"))
                return ModTextTranslator.T("Left Stick");

            return null;
        }

        private static bool Has(string s, string token)
            => s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Resolves a mouse row's bound button/wheel from its rendered glyph sprite
        /// ("UI_Common_mouse_l/_r/_rad", under view.keyboardIconsRoot). The glyph tracks the LIVE
        /// binding, so this stays right after a rebind. Null on failure (no binding spoken).
        /// </summary>
        private static string ResolveMouseButtonText(Il2CppLast.UI.KeyInput.ConfigControllCommandController command)
        {
            try
            {
                var root = command.view != null ? command.view.keyboardIconsRoot : null;
                UnityEngine.UI.Image[] imgs = root != null
                    ? root.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                    : (command.gameObject != null ? command.gameObject.GetComponentsInChildren<UnityEngine.UI.Image>(true) : null);
                if (imgs == null) return null;
                for (int i = 0; i < imgs.Length; i++)
                {
                    var img = imgs[i];
                    if (img == null || !img.gameObject.activeInHierarchy || img.sprite == null) continue;
                    string label = MouseSpriteToLabel(img.sprite.name);
                    if (!string.IsNullOrEmpty(label)) return label;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Maps a mouse glyph sprite name to text. "mouse_rad" (the wheel) contains "mouse_r", so the
        /// wheel is checked before the right button.
        /// </summary>
        private static string MouseSpriteToLabel(string s)
        {
            if (string.IsNullOrEmpty(s) || !Has(s, "mouse")) return null;
            if (Has(s, "mouse_rad") || Has(s, "wheel") || Has(s, "scroll")) return ModTextTranslator.T("Mouse Wheel");
            if (Has(s, "mouse_l")) return ModTextTranslator.T("Left Mouse Button");
            if (Has(s, "mouse_r")) return ModTextTranslator.T("Right Mouse Button");
            if (Has(s, "mouse_c") || Has(s, "mouse_m")) return ModTextTranslator.T("Middle Mouse Button");
            return ModTextTranslator.T("Mouse Button");   // an extra (gaming-mouse) button
        }

        /// <summary>
        /// Resolves a remap row's CURRENT (remappable) gamepad button to family-aware text. Reads the
        /// live binding from the screen's KeyConfigData (GameKey → Unity KeyCode), maps the KeyCode to
        /// an SDL button index, and lets ControllerLabels pick the text for the connected controller.
        /// Returns null if it can't be resolved.
        /// </summary>
        private static string ResolveGamepadButtonText(
            Il2CppLast.UI.KeyInput.ConfigKeysSettingController owner,
            Il2CppLast.UI.KeyInput.ConfigControllCommandController command)
        {
            try
            {
                if (owner == null || command == null) return null;
                var kd = owner.keydata;
                if (kd == null) return null;
                var dict = kd.GetGamePadKeyConfigtDictionary();
                if (dict == null || !dict.ContainsKey(command.key)) return null;
                int sdl = JoystickKeyCodeToSdlButton((int)dict[command.key]);
                if (sdl < 0) return null;
                return ControllerLabels.GetButtonLabel(sdl);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Maps a Unity legacy KeyCode.JoystickButtonN (330+) to an SDL gamepad button index using the
        /// XInput-standard layout. ControllerLabels then yields the right family text for whichever
        /// controller is connected. Returns -1 if not a mapped button.
        /// </summary>
        internal static int JoystickKeyCodeToSdlButton(int keyCode)
        {
            switch (keyCode)
            {
                // FFPR keeps the bottom/right face buttons in the Japanese arrangement: Confirm is
                // stored on JoystickButton1 and Cancel on JoystickButton0. The American build confirms
                // with the BOTTOM button (A/Cross), so JB1→SOUTH and JB0→EAST — i.e. swapped from
                // Unity's XInput default. (X/Y below are unaffected.)
                case 330: return SDL3.SDL_GAMEPAD_BUTTON_EAST;           // JoystickButton0 — Cancel (B/Circle)
                case 331: return SDL3.SDL_GAMEPAD_BUTTON_SOUTH;          // JoystickButton1 — Confirm (A/Cross)
                case 332: return SDL3.SDL_GAMEPAD_BUTTON_WEST;           // JoystickButton2 — X/Square
                case 333: return SDL3.SDL_GAMEPAD_BUTTON_NORTH;          // JoystickButton3 — Y/Triangle
                case 334: return SDL3.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER;  // JoystickButton4 — LB
                case 335: return SDL3.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER; // JoystickButton5 — RB
                case 336: return SDL3.SDL_GAMEPAD_BUTTON_BACK;           // JoystickButton6 — Back/View
                case 337: return SDL3.SDL_GAMEPAD_BUTTON_START;          // JoystickButton7 — Start/Menu
                case 338: return SDL3.SDL_GAMEPAD_BUTTON_LEFT_STICK;     // JoystickButton8 — LS
                case 339: return SDL3.SDL_GAMEPAD_BUTTON_RIGHT_STICK;    // JoystickButton9 — RS
                default: return -1;
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
        // Fires on EVERY Left/Right press, including at the ends of the option range where the
        // value does not actually change — hold the last value so the ends stay quiet.
        private static string _lastValue;

        /// <summary>Clears the last announced arrow value.</summary>
        public static void ResetLastValue() => _lastValue = null;

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
                            if (textValue != "<" && textValue != ">" && textValue != "\u25c0" && textValue != "\u25b6" &&
                                textValue != "\u2190" && textValue != "\u2192")
                            {
                                // Only announce if value changed
                                if (textValue == _lastValue) return;
                                _lastValue = textValue;

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
        // Not a plain guard — these two ARE the logic. The game re-invokes this on the focused
        // slider to keep the visual in sync, so: same slider + same value = silent; a DIFFERENT
        // slider = the row just gained focus and ConfigCommandController.SetFocus already said
        // "Name: Value", so stay quiet; same slider + new value = speak the value alone.
        private static object _lastSliderController;
        private static string _lastSliderValue;

        /// <summary>Clears the tracked slider so the next adjustment announces fresh.</summary>
        public static void ResetLastSlider()
        {
            _lastSliderController = null;
            _lastSliderValue = null;
        }

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

                // Read display value from game's sliderValueText, fallback to percentage
                string displayValue = ConfigMenuReader.GetSliderDisplayValue(view.Slider, view.sliderValueText);
                if (string.IsNullOrEmpty(displayValue)) return;

                // Track controller and value separately
                bool controllerChanged = !ReferenceEquals(controller, _lastSliderController);
                bool valueChanged = displayValue != _lastSliderValue;
                _lastSliderController = controller;
                _lastSliderValue = displayValue;

                // Both unchanged - skip
                if (!controllerChanged && !valueChanged) return;

                // Newly focused slider: ConfigCommandController.SetFocus already announced the
                // full "Name: Value", so only adjustments after this point should speak.
                if (controllerChanged) return;

                // Same controller, value changed - announce just the new value
                FFV_ScreenReaderMod.SpeakText(displayValue, interrupt: true);
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
        // Touch-mode clone of the arrow guard above. Never fires on a keyboard/gamepad build,
        // kept so both input paths stay structurally identical.
        private static string _lastValue;

        /// <summary>Clears the last announced arrow value.</summary>
        public static void ResetLastValue() => _lastValue = null;

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
                            if (textValue != "<" && textValue != ">" && textValue != "\u25c0" && textValue != "\u25b6" &&
                                textValue != "\u2190" && textValue != "\u2192")
                            {
                                // Only announce if value changed
                                if (textValue == _lastValue) return;
                                _lastValue = textValue;

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
        // Touch-mode clone of the slider state machine above. Never fires on a keyboard/gamepad
        // build, kept so both input paths stay structurally identical.
        private static object _lastSliderController;
        private static string _lastSliderValue;

        /// <summary>Clears the tracked slider so the next adjustment announces fresh.</summary>
        public static void ResetLastSlider()
        {
            _lastSliderController = null;
            _lastSliderValue = null;
        }

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

                // Read display value from game's sliderValueText, fallback to percentage
                string displayValue = ConfigMenuReader.GetSliderDisplayValue(slider, view.sliderValueText);
                if (string.IsNullOrEmpty(displayValue)) return;

                // Track controller and value separately
                bool controllerChanged = !ReferenceEquals(controller, _lastSliderController);
                bool valueChanged = displayValue != _lastSliderValue;
                _lastSliderController = controller;
                _lastSliderValue = displayValue;

                // Both unchanged - skip
                if (!controllerChanged && !valueChanged) return;

                // Newly focused slider: ConfigCommandController.SetFocus already announced the
                // full "Name: Value", so only adjustments after this point should speak.
                if (controllerChanged) return;

                // Same controller, value changed - announce just the new value
                FFV_ScreenReaderMod.SpeakText(displayValue, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Touch SwitchSliderTypeProcess patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Drives ConfigMenuState.IsActive for the title-screen options menu. The KeyInput OptionController
    /// hosts language / screen / key / sound settings on the title screen via the same
    /// ConfigActualDetailsControllerBase pipeline as the in-game config. Without this, the title-screen
    /// language dropdown announcement (gated on IsActive) would never fire, and SetDropDownItemFocus
    /// fires during title load before the menu opens — gating on IsActive kills that regression.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.OptionController), nameof(Il2CppLast.UI.KeyInput.OptionController.SetActive), new Type[] { typeof(bool) })]
    public static class OptionController_SetActive_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(bool isActive)
        {
            if (isActive)
                ConfigMenuState.IsActive = true;
            else
                ConfigMenuState.ClearState();
        }
    }

    /// <summary>
    /// Manual patch application for config-menu features that can't be expressed as a single declarative
    /// [HarmonyPatch]: the title-screen Language dropdown focus announcement and the keyboard/gamepad
    /// remap assign-flow (overloaded ChangeKeySetting + the SettingInit prompts).
    /// </summary>
    public static class ConfigMenuPatches
    {
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            // Title-screen Language dropdown (keyboard/gamepad uses a Unity Dropdown driven by the
            // KeyInput OptionController). SetDropDownItemFocus is the discrete, event-driven hook —
            // it announces the focused language directly, gated on the config menu being open.
            // DO NOT hook OptionController.UpdateSelectLanguage — it is an EMPTY method whose body is
            // the shared empty stub (RVA 0x2715A0 in FF5); detouring it corrupts every method that
            // shares that body → launch crash.
            void PatchOption(string method, string postfixName)
            {
                try
                {
                    var m = AccessTools.Method(typeof(Il2CppLast.UI.KeyInput.OptionController), method);
                    if (m != null)
                    {
                        harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ConfigMenuPatches), postfixName)));
                        MelonLogger.Msg($"[Config Menu] OptionController.{method} patch applied");
                    }
                    else
                    {
                        MelonLogger.Warning($"[Config Menu] OptionController.{method} not found");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Config Menu] Error patching OptionController.{method}: {ex.Message}");
                }
            }

            PatchOption("SetDropDownItemFocus", nameof(SetDropDownItemFocus_Postfix));

            // NOTE: OptionController.ShowConfig / InitializeConfigList are deliberately NOT hooked
            // for initial focus. The config settings screen already announces its focused row on
            // entry via ConfigCommandController.SetFocus. Worse than merely duplicating, an entry
            // hook that cleared the SetFocus guard let the game's own second SetFocus through, so
            // entering Config spoke "Language: English" twice. Same call FF1 makes for its in-game
            // ConfigController.

            // Remap assign-flow speaking (ConfigKeysSettingController, all real-bodied methods).
            // KeyboardSettingInit / GamePadSettingInit fire on entering assign mode → "press a
            // key/button" prompt. ChangeKeySetting (overloaded keyboard + gamepad) fires when the
            // binding is applied → announce the new mapping.
            void PatchKeysSetting(string method, string postfixName)
            {
                try
                {
                    var m = AccessTools.Method(typeof(Il2CppLast.UI.KeyInput.ConfigKeysSettingController), method);
                    if (m != null)
                    {
                        harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ConfigMenuPatches), postfixName)));
                        MelonLogger.Msg($"[Config Menu] ConfigKeysSettingController.{method} patch applied");
                    }
                    else
                    {
                        MelonLogger.Warning($"[Config Menu] ConfigKeysSettingController.{method} not found");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Config Menu] Error patching ConfigKeysSettingController.{method}: {ex.Message}");
                }
            }

            PatchKeysSetting("KeyboardSettingInit", nameof(KeyboardSettingInit_Postfix));
            PatchKeysSetting("GamePadSettingInit", nameof(GamePadSettingInit_Postfix));

            // Gamepad/Keyboard "Controls" list (read-only list of every control) → KeyHelpReader.
            // Entering the GamePad/Keyboard Help state shows helpContentList/keyboardHelpContentList;
            // render it once so arrows/WASD can step the entries. Leaving the Help state (back to the
            // select list, or closing the controls screen) tears it down. All five are real bodies
            // (unique RVAs) — MouseSettInit, by contrast, is the folded empty stub and must not be hooked.
            PatchKeysSetting("GamePadHelpInit", nameof(GamePadHelpInit_Postfix));
            PatchKeysSetting("KeyboardHelpInit", nameof(KeyboardHelpInit_Postfix));
            PatchKeysSetting("GamePadSelectInit", nameof(ControlsHelpClose_Postfix));
            PatchKeysSetting("KeyboardSelectInit", nameof(ControlsHelpClose_Postfix));
            PatchKeysSetting("Close", nameof(ControlsHelpClose_Postfix));

            // ChangeKeySetting is overloaded — patch every overload with the same __instance-only
            // postfix (avoids AmbiguousMatchException without needing an exact Type[]).
            try
            {
                var changePostfix = new HarmonyMethod(AccessTools.Method(typeof(ConfigMenuPatches), nameof(ChangeKeySetting_Postfix)));
                int changeCount = 0;
                foreach (var m in typeof(Il2CppLast.UI.KeyInput.ConfigKeysSettingController).GetMethods(
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                {
                    if (m.Name == "ChangeKeySetting") { harmony.Patch(m, postfix: changePostfix); changeCount++; }
                }
                MelonLogger.Msg($"[Config Menu] ConfigKeysSettingController.ChangeKeySetting patched ({changeCount} overload(s))");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Config Menu] Error patching ChangeKeySetting: {ex.Message}");
            }
        }

        // ── Title-screen Language dropdown ──────────────────────────────────────────────
        // SetDropDownItemFocus is the EVENT-DRIVEN announce, gated to when the config menu is actually
        // open so it cannot speak the current language at the title "Press any button."

        /// <summary>Focused language label: prefer the tracked dropdown item, else the dropdown value.</summary>
        private static string GetFocusedLanguageLabel(Il2CppLast.UI.KeyInput.OptionController inst)
        {
            var item = inst.selectedItem;
            if (item == null) return null;

            if (item.view != null && item.view.LabelText != null)
            {
                string t = item.view.LabelText.text;
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
            var dd = inst.selectedDoropDown;
            if (dd != null && dd.options != null && dd.value >= 0 && dd.value < dd.options.Count)
            {
                var opt = dd.options[dd.value];
                if (opt != null && !string.IsNullOrWhiteSpace(opt.text)) return opt.text;
            }
            // The CURRENT language's item has an empty LabelText (its native name is a sprite; the
            // parenthetical English name is omitted for the current language). A focused item with no
            // readable label is therefore the current language → name it from the game's MessageManager.
            return ConfigMenuReader.GetCurrentLanguageDisplayName();
        }

        /// <summary>
        /// EVENT-DRIVEN announce. OptionController.SetDropDownItemFocus (KeyInput) is the discrete
        /// "focused dropdown item changed" hook — speaks the focused language directly.
        /// </summary>
        public static void SetDropDownItemFocus_Postfix(Il2CppLast.UI.KeyInput.OptionController __instance)
        {
            try
            {
                if (__instance == null) return;
                string label = GetFocusedLanguageLabel(__instance);
                // Gate on the config menu being genuinely OPEN — the same lifecycle flag the SetFocus
                // patch uses (now also driven by OptionController.SetActive). The title screen
                // instantiates the language OptionController during load and fires SetDropDownItemFocus
                // BEFORE the menu is opened (IsActive=false). Real dropdown navigation happens with the
                // menu open (IsActive=true), so this kills the title regression without muting the menu.
                if (!ConfigMenuState.IsActive) return;
                if (string.IsNullOrWhiteSpace(label)) return;
                FFV_ScreenReaderMod.SpeakText(label.Trim(), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetDropDownItemFocus patch: {ex.Message}");
            }
        }

        // ── Remap assign-flow (ConfigKeysSettingController) ──────────────────────────────
        // Entering assign mode → announce the "press a key/button" prompt. Init methods fire once on
        // state entry → event-driven.
        public static void KeyboardSettingInit_Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance)
            => AnnounceAssignPrompt(__instance, gamepad: false);

        public static void GamePadSettingInit_Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance)
            => AnnounceAssignPrompt(__instance, gamepad: true);

        private static void AnnounceAssignPrompt(Il2CppLast.UI.KeyInput.ConfigKeysSettingController inst, bool gamepad)
        {
            try
            {
                if (inst == null) return;
                FFV_ScreenReaderMod.SpeakText(
                    gamepad ? ModTextTranslator.T("Press a button.") : ModTextTranslator.T("Press a key."),
                    interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in assign-prompt patch: {ex.Message}");
            }
        }

        // ── Gamepad/Keyboard Controls list (read-only) → KeyHelpReader ──────────────────
        // The list is ConfigKeysSettingController's GamePad/Keyboard Help state, not the always-present
        // KeyHelpController hint bar. On state entry, render the list once and hand the strings over.

        public static void GamePadHelpInit_Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance)
            => OpenControlsHelp(__instance, gamepad: true);

        public static void KeyboardHelpInit_Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance)
            => OpenControlsHelp(__instance, gamepad: false);

        /// <summary>Returning to the select list or closing the controls screen tears the list down.</summary>
        public static void ControlsHelpClose_Postfix() => KeyHelpReader.CloseControlsHelp();

        private static void OpenControlsHelp(Il2CppLast.UI.KeyInput.ConfigKeysSettingController inst, bool gamepad)
        {
            if (inst == null) return;
            // One frame so each row's binding text and glyph are populated before rendering.
            CoroutineManager.StartManaged(DelayedOpenControlsHelp(inst, gamepad));
        }

        private static IEnumerator DelayedOpenControlsHelp(Il2CppLast.UI.KeyInput.ConfigKeysSettingController inst, bool gamepad)
        {
            yield return null;

            System.Collections.Generic.List<string> entries = null;
            try
            {
                // The state machine can cycle its Help-state Init callbacks while the controls screen
                // isn't shown (scene construction), so only build when it is genuinely on screen.
                if (inst != null && inst.gameObject != null && inst.gameObject.activeInHierarchy)
                {
                    var list = gamepad ? inst.HelpContentList : inst.KeyboardHelpContentList;
                    if (list != null)
                    {
                        entries = new System.Collections.Generic.List<string>();
                        for (int i = 0; i < list.Count; i++)
                        {
                            string row = ConfigKeysSettingController_SelectContent_Patch.BuildCommandAnnouncement(inst, list[i], isHelpList: true);
                            if (!string.IsNullOrWhiteSpace(row)) entries.Add(row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading controls help list: {ex.Message}");
            }

            KeyHelpReader.OpenControlsHelp(inst, entries);
        }

        /// <summary>
        /// Postfix for ConfigKeysSettingController.ChangeKeySetting (all overloads). Fires when a
        /// binding is applied — re-reads the just-edited command and announces the new mapping.
        /// </summary>
        public static void ChangeKeySetting_Postfix(Il2CppLast.UI.KeyInput.ConfigKeysSettingController __instance)
        {
            try
            {
                if (__instance == null) return;
                string announcement = ConfigKeysSettingController_SelectContent_Patch.BuildCommandAnnouncement(__instance, __instance.selectedCommand);
                if (string.IsNullOrWhiteSpace(announcement)) return;
                FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ChangeKeySetting patch: {ex.Message}");
            }
        }
    }

}
