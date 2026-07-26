using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using FFV_ScreenReader.Utils;
using MelonLoader;
using Il2CppSerial.FF5.UI.KeyInput;
using FFV_ScreenReader.Menus;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using LibraryInfoController_KeyInput = Il2CppLast.UI.KeyInput.LibraryInfoController;

namespace FFV_ScreenReader.Core
{
    public class InputManager
    {
        private readonly FFV_ScreenReaderMod mod;
        private StatusDetailsController cachedStatusController;
        private LibraryInfoController_KeyInput cachedBestiaryInfoController;
        private readonly KeyBindingRegistry registry = new KeyBindingRegistry();

        public InputManager(FFV_ScreenReaderMod mod)
        {
            this.mod = mod;
            InitializeBindings();
        }

        /// <summary>
        /// Registers a field-only binding. Off-field (menu/battle/title/dialogue) the active
        /// context is never Field, so the binding has no match and dispatch silently does nothing.
        /// </summary>
        private void RegisterFieldWithBattleFeedback(KeyCode key, KeyModifier modifier, System.Action action, string description)
        {
            registry.Register(key, modifier, KeyContext.Field, action, description);
        }

        private void InitializeBindings()
        {
            // --- Status screen: arrow key navigation ---
            registry.Register(KeyCode.DownArrow, KeyModifier.Ctrl, KeyContext.Status, StatusNavigationReader.JumpToBottom, "Jump to bottom stat");
            registry.Register(KeyCode.DownArrow, KeyModifier.Shift, KeyContext.Status, StatusNavigationReader.JumpToNextGroup, "Jump to next stat group");
            registry.Register(KeyCode.DownArrow, KeyModifier.None, KeyContext.Status, StatusNavigationReader.NavigateNext, "Next stat");
            registry.Register(KeyCode.UpArrow, KeyModifier.Ctrl, KeyContext.Status, StatusNavigationReader.JumpToTop, "Jump to top stat");
            registry.Register(KeyCode.UpArrow, KeyModifier.Shift, KeyContext.Status, StatusNavigationReader.JumpToPreviousGroup, "Jump to previous stat group");
            registry.Register(KeyCode.UpArrow, KeyModifier.None, KeyContext.Status, StatusNavigationReader.NavigatePrevious, "Previous stat");

            // --- Bestiary detail: arrow key navigation ---
            registry.Register(KeyCode.DownArrow, KeyModifier.Ctrl, KeyContext.Bestiary, BestiaryNavigationReader.JumpToBottom, "Jump to bottom bestiary stat");
            registry.Register(KeyCode.DownArrow, KeyModifier.Shift, KeyContext.Bestiary, BestiaryNavigationReader.JumpToNextGroup, "Jump to next bestiary group");
            registry.Register(KeyCode.DownArrow, KeyModifier.None, KeyContext.Bestiary, BestiaryNavigationReader.NavigateNext, "Next bestiary stat");
            registry.Register(KeyCode.UpArrow, KeyModifier.Ctrl, KeyContext.Bestiary, BestiaryNavigationReader.JumpToTop, "Jump to top bestiary stat");
            registry.Register(KeyCode.UpArrow, KeyModifier.Shift, KeyContext.Bestiary, BestiaryNavigationReader.JumpToPreviousGroup, "Jump to previous bestiary group");
            registry.Register(KeyCode.UpArrow, KeyModifier.None, KeyContext.Bestiary, BestiaryNavigationReader.NavigatePrevious, "Previous bestiary stat");

            // --- Field: entity navigation (brackets + backslash) — with battle feedback ---
            RegisterFieldWithBattleFeedback(KeyCode.LeftBracket, KeyModifier.Shift, mod.CyclePreviousCategory, "Previous entity category");
            RegisterFieldWithBattleFeedback(KeyCode.LeftBracket, KeyModifier.None, mod.CyclePrevious, "Previous entity");
            RegisterFieldWithBattleFeedback(KeyCode.RightBracket, KeyModifier.Shift, mod.CycleNextCategory, "Next entity category");
            RegisterFieldWithBattleFeedback(KeyCode.RightBracket, KeyModifier.None, mod.CycleNext, "Next entity");
            RegisterFieldWithBattleFeedback(KeyCode.Backslash, KeyModifier.Ctrl, mod.ToggleToLayerFilter, "Toggle layer filter");
            RegisterFieldWithBattleFeedback(KeyCode.Backslash, KeyModifier.Shift, mod.TogglePathfindingFilter, "Toggle pathfinding filter");
            RegisterFieldWithBattleFeedback(KeyCode.Backslash, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity");

            // --- Field: pathfinding alternate keys (J/K/L/P) — with battle feedback ---
            RegisterFieldWithBattleFeedback(KeyCode.J, KeyModifier.Shift, mod.CyclePreviousCategory, "Previous entity category (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.J, KeyModifier.None, mod.CyclePrevious, "Previous entity (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.K, KeyModifier.Shift, mod.AnnounceCurrentEntity, "Announce current entity (alt shift)");
            RegisterFieldWithBattleFeedback(KeyCode.K, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.L, KeyModifier.Shift, mod.CycleNextCategory, "Next entity category (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.L, KeyModifier.None, mod.CycleNext, "Next entity (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.P, KeyModifier.Shift, mod.TogglePathfindingFilter, "Toggle pathfinding filter (alt)");
            RegisterFieldWithBattleFeedback(KeyCode.P, KeyModifier.None, mod.AnnounceCurrentEntity, "Announce current entity (alt)");

            // --- Field: waypoint keys (field-only; silent no-op off-field) ---
            registry.Register(KeyCode.Comma, KeyModifier.Shift, KeyContext.Field, mod.CyclePreviousWaypointCategory, "Previous waypoint category");
            registry.Register(KeyCode.Comma, KeyModifier.None, KeyContext.Field, mod.CyclePreviousWaypoint, "Previous waypoint");
            registry.Register(KeyCode.Period, KeyModifier.Ctrl, KeyContext.Field, mod.RenameCurrentWaypoint, "Rename waypoint");
            registry.Register(KeyCode.Period, KeyModifier.Shift, KeyContext.Field, mod.CycleNextWaypointCategory, "Next waypoint category");
            registry.Register(KeyCode.Period, KeyModifier.None, KeyContext.Field, mod.CycleNextWaypoint, "Next waypoint");
            registry.Register(KeyCode.Slash, KeyModifier.CtrlShift, KeyContext.Field, mod.ClearAllWaypointsForMap, "Clear all waypoints for map");
            registry.Register(KeyCode.Slash, KeyModifier.Ctrl, KeyContext.Field, mod.RemoveCurrentWaypoint, "Remove current waypoint");
            registry.Register(KeyCode.Slash, KeyModifier.Shift, KeyContext.Field, mod.AddNewWaypointWithNaming, "Add waypoint with name");
            registry.Register(KeyCode.Slash, KeyModifier.None, KeyContext.Field, mod.PathfindToCurrentWaypoint, "Pathfind to waypoint");

            // --- Field: teleport (Ctrl+Arrow, not on status screen — handled by context) ---
            float t = GameConstants.TILE_SIZE;
            registry.Register(KeyCode.UpArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(0, t)), "Teleport north");
            registry.Register(KeyCode.DownArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(0, -t)), "Teleport south");
            registry.Register(KeyCode.LeftArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(-t, 0)), "Teleport west");
            registry.Register(KeyCode.RightArrow, KeyModifier.Ctrl, KeyContext.Field, () => mod.TeleportInDirection(new Vector2(t, 0)), "Teleport east");

            // --- Global: info/announcements ---
            registry.Register(KeyCode.G, KeyContext.Global, mod.AnnounceGilAmount, "Announce Gil");
            registry.Register(KeyCode.H, KeyContext.Global, mod.AnnounceActiveCharacterStatus, "Announce character status");
            registry.Register(KeyCode.M, KeyModifier.Shift, KeyContext.Global, mod.ToggleMapExitFilter, "Toggle map exit filter");
            registry.Register(KeyCode.M, KeyModifier.None, KeyContext.Global, mod.AnnounceCurrentMap, "Announce current map");
            registry.Register(KeyCode.T, KeyModifier.Shift, KeyContext.Global, Patches.TimerHelper.ToggleTimerFreeze, "Toggle timer freeze");
            registry.Register(KeyCode.T, KeyModifier.None, KeyContext.Global, () => Patches.TimerHelper.AnnounceActiveTimers(), "Announce active timers");

            registry.Register(KeyCode.V, KeyContext.Global, HandleMovementStateKey, "Announce vehicle state");
            registry.Register(KeyCode.I, KeyModifier.Shift, KeyContext.Global, KeyHelpReader.AnnounceKeyHelp, "Read control tooltips");
            registry.Register(KeyCode.I, KeyContext.Global, HandleItemInfoKey, "Item details");
            registry.Register(KeyCode.U, KeyContext.Global, Menus.UsableByAnnouncer.AnnounceForCurrentContext, "Usable by jobs");

            // Repeat the current dialogue page. Silent no-op outside a message window, so the
            // key never speaks over anything on the field. Controller equivalent: mod + Square.
            registry.Register(KeyCode.R, KeyContext.Global, () =>
            {
                if (Patches.DialogueTracker.IsInDialogue)
                    Patches.DialogueTracker.RepeatCurrentPage();
            }, "Repeat dialogue");

            // --- Field-only toggles (blocked in battle with feedback) ---
            RegisterFieldWithBattleFeedback(KeyCode.Quote, KeyModifier.None, mod.ToggleFootsteps, "Toggle footsteps");
            RegisterFieldWithBattleFeedback(KeyCode.Semicolon, KeyModifier.Shift, mod.ToggleLandingPings, "Toggle landing pings");
            RegisterFieldWithBattleFeedback(KeyCode.Semicolon, KeyModifier.None, mod.ToggleWallTones, "Toggle wall tones");
            RegisterFieldWithBattleFeedback(KeyCode.Alpha9, KeyModifier.None, mod.ToggleAudioBeacons, "Toggle audio beacons");
            RegisterFieldWithBattleFeedback(KeyCode.Alpha0, KeyModifier.None, EntityTranslator.EntityDump.DumpCurrentMap, "Dump entity names");
            RegisterFieldWithBattleFeedback(KeyCode.Equals, KeyModifier.None, mod.CycleNextCategory, "Next entity category (global)");
            RegisterFieldWithBattleFeedback(KeyCode.Minus, KeyModifier.None, mod.CyclePreviousCategory, "Previous entity category (global)");

            // --- Battle result navigator (L) ---
            registry.Register(KeyCode.L, KeyModifier.None, KeyContext.BattleResult, OpenBattleResultNavigator, "Open battle result details");

            // Sort for correct modifier precedence
            registry.FinalizeRegistration();
        }

        public void Update()
        {
            // Poll SDL3 gamepad + GetAsyncKeyState keyboard once per frame.
            // Must come before any mod input handling so edge-detection state is fresh.
            GamepadManager.Update();

            // Suppress Unity legacy Input when the mod is consuming. Safe because the mod reads
            // keyboard via GetAsyncKeyState (unaffected by ResetInputAxes). This + the
            // InputPassthroughPatches = complete game keyboard suppression, and it works even
            // when no gamepad is connected (the passthrough patches early-return without one).
            if (ControllerRouter.SuppressGameInput)
                Input.ResetInputAxes();

            // Determine context AFTER polling so the router (and dispatch below) sees fresh
            // input for this frame.
            KeyContext activeContext = DetermineContext();

            // Route controller inputs to the appropriate state-machine bucket. Runs every frame
            // so the router can interrupt speech / drive nav even without a gamepad.
            ControllerRouter.Update(activeContext);

            if (GamepadManager.AnyKeyboardKeyDown())
                ControllerRouter.NotifyKeyboardInput();

            // Handle modal dialogs first (each consumes all input when open)
            if (ConfirmationDialog.HandleInput()) return;
            if (TextInputWindow.HandleInput()) return;
            if (ModMenu.HandleInput()) return;
            if (BattleResultNavigator.HandleInput()) return;

            // Game-context hotkeys below only fire when the game window is the foreground
            // window, so mod functions don't trigger while the player is in another app.
            // Placed AFTER the modals so the now-virtual dialogs/menu keep working even when
            // the game window isn't foreground.
            if (!WindowsFocusHelper.IsGameWindowFocused())
                return;

            if (!GamepadManager.AnyKeyboardKeyDown())
                return;

            // Skip ALL mod hotkeys (including F8 and the function keys) while the player is
            // typing in the game's own text field, so naming/input screens aren't disrupted.
            if (IsInputFieldFocused()) return;

            // Bare F-keys only fire with no modifier held, so OS shortcuts like Alt+F4
            // (close window), Ctrl+F-keys and Shift+F-keys don't trigger the screen
            // reader. Explicit Shift/Ctrl bindings still match via GetCurrentModifiers.
            bool anyModifierHeld = IsAnyModifierHeld();

            // F8 to open mod menu — allowed on the field AND in field menus, blocked in battle
            // and on the title screen. Rejection wording lives in
            // ControllerRouter.SpeakModMenuUnavailable so Start-button and F8 stay in sync;
            // the Start-button gate in ControllerRouter.HandleStateTransitions must match this.
            if (!anyModifierHeld && GamepadManager.IsKeyCodePressed(KeyCode.F8))
            {
                if (IsFieldOrFieldMenuActive())
                    ModMenu.Open();
                else
                    ControllerRouter.SpeakModMenuUnavailable();
                return;
            }

            // Handle function keys (F1/F3/F5 — special coroutine/battle logic) — bare keypress only
            if (!anyModifierHeld)
                HandleFunctionKeyInput();

            KeyModifier currentModifiers = GetCurrentModifiers();

            // Alt held with no registered Alt-binding → skip dispatch so Alt+<key> doesn't
            // accidentally trigger the unmodified binding. (Shift/Ctrl are routed through
            // currentModifiers and matched exactly by the registry, so they still work.)
            if (IsAltHeld())
                return;

            // Dispatch all registered bindings (includes V, I, and all other keys)
            DispatchRegisteredBindings(activeContext, currentModifiers);
        }

        private static bool IsAltHeld()
        {
            return GamepadManager.IsKeyCodeHeld(KeyCode.LeftAlt)
                || GamepadManager.IsKeyCodeHeld(KeyCode.RightAlt);
        }

        private static bool IsAnyModifierHeld()
        {
            return GamepadManager.IsKeyCodeHeld(KeyCode.LeftShift)
                || GamepadManager.IsKeyCodeHeld(KeyCode.RightShift)
                || GamepadManager.IsKeyCodeHeld(KeyCode.LeftControl)
                || GamepadManager.IsKeyCodeHeld(KeyCode.RightControl)
                || GamepadManager.IsKeyCodeHeld(KeyCode.LeftAlt)
                || GamepadManager.IsKeyCodeHeld(KeyCode.RightAlt);
        }

        /// <summary>
        /// Determine the current input context based on game state.
        /// </summary>
        private KeyContext DetermineContext()
        {
            // Events/cutscenes: Global only. Keeps field/entity/waypoint/teleport hotkeys and
            // the entity scanner inert during scripted sequences (and keeps their IL2CPP work
            // off the frame) while mod mode, the repeat key, and Global info keys stay live.
            // Placed first so the event path costs a single cached bool read.
            if (Patches.GameStatePatches.IsInEventState)
                return KeyContext.Global;

            if (IsBestiaryDetailActive())
                return KeyContext.Bestiary;

            if (IsStatusScreenActive())
                return KeyContext.Status;

            // Check for battle results screen before general battle context
            if (BattleResultDataStore.HasData)
                return KeyContext.BattleResult;

            if (IsInBattle() || Patches.BattleState.IsInBattle)
                return KeyContext.Battle;

            if (Patches.DialogueTracker.ValidateState() || Patches.ShopMenuTracker.IsInShopSession)
                return KeyContext.Global;

            // Field keys only fire while actively on a field map with no menu open.
            // Otherwise fall through to Global so field/entity/waypoint/toggle hotkeys
            // are silent no-ops off-field, while Global info keys still work everywhere.
            if (IsOnValidMap() && !MenuStateRegistry.AnyActive())
                return KeyContext.Field;

            // Fallback: neither field nor battle (e.g., menus, fading)
            return KeyContext.Global;
        }

        /// <summary>
        /// Get the currently held modifier keys. Uses SDL/GetAsyncKeyState so modifiers
        /// register regardless of game window focus.
        /// </summary>
        private KeyModifier GetCurrentModifiers()
        {
            bool shift = GamepadManager.IsKeyCodeHeld(KeyCode.LeftShift) || GamepadManager.IsKeyCodeHeld(KeyCode.RightShift);
            bool ctrl = GamepadManager.IsKeyCodeHeld(KeyCode.LeftControl) || GamepadManager.IsKeyCodeHeld(KeyCode.RightControl);

            if (ctrl && shift) return KeyModifier.CtrlShift;
            if (ctrl) return KeyModifier.Ctrl;
            if (shift) return KeyModifier.Shift;
            return KeyModifier.None;
        }

        private void DispatchRegisteredBindings(KeyContext activeContext, KeyModifier currentModifiers)
        {
            foreach (var key in registry.RegisteredKeys)
            {
                if (GamepadManager.IsKeyCodePressed(key))
                    registry.TryExecute(key, currentModifiers, activeContext);
            }
        }

        private static void OpenBattleResultNavigator()
        {
            if (BattleResultDataStore.HasData)
                BattleResultNavigator.Open();
            else
                FFV_ScreenReaderMod.SpeakText(LocalizationHelper.GetModString("no_data"), interrupt: true);
        }

        private void HandleMovementStateKey()
        {
            if (!IsOnValidMap())
            {
                FFV_ScreenReaderMod.SpeakText(T("Not on map"), interrupt: true);
                return;
            }

            MoveStateHelper.SyncWithActualGameState();
            if (MoveStateHelper.IsOnFoot())
            {
                bool isRunning = MoveStateHelper.GetDashFlag();
                FFV_ScreenReaderMod.SpeakText(isRunning ? T("Running") : T("Walking"), interrupt: true);
            }
            else
            {
                int moveState = MoveStateHelper.GetCurrentMoveState();
                FFV_ScreenReaderMod.SpeakText(MoveStateHelper.GetMoveStateName(moveState), interrupt: true);
            }
        }

        /// <summary>
        /// Context cascade for the on-demand details key. Bound to the I key and to right
        /// stick up (ControllerRouter.HandleNormalNonField), so both stay in sync.
        /// </summary>
        internal static void HandleItemInfoKey()
        {
            if (Patches.ShopMenuTracker.ValidateState())
            {
                Patches.ShopDetailsAnnouncer.AnnounceCurrentItemDetails();
            }
            else if (Patches.ItemMenuTracker.ValidateState())
            {
                Patches.ItemDetailsAnnouncer.AnnounceItemDescription();
            }
            else if (Patches.JobMenuTracker.ValidateState())
            {
                Patches.JobDetailsAnnouncer.AnnounceCurrentJobDetails();
            }
            else if (Patches.AbilitySlotMenuTracker.ValidateState())
            {
                Patches.AbilitySlotDetailsAnnouncer.AnnounceCurrentDetails();
            }
            else if (Patches.AbilityEquipMenuTracker.ValidateState())
            {
                Patches.AbilityEquipDetailsAnnouncer.AnnounceCurrentDetails();
            }
            else if (Patches.AbilityMenuTracker.ValidateState())
            {
                Patches.AbilityDetailsAnnouncer.AnnounceCurrentAbilityDetails();
            }
            else
            {
                Patches.JobAbilityTrackerHelper.ClearAllTrackers();
                Patches.ItemMenuTracker.ClearState();
                AnnounceConfigTooltip();
            }
        }

        /// <summary>
        /// Handle function key input for game state announcements.
        /// </summary>
        private void HandleFunctionKeyInput()
        {
            if (GamepadManager.IsKeyCodePressed(KeyCode.F1))
            {
                if (!IsOnValidMap())
                {
                    FFV_ScreenReaderMod.SpeakText(T("Not on map"), interrupt: true);
                    return;
                }
                CoroutineManager.StartUntracked(AnnounceWalkRunState());
                return;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.F3))
            {
                if (!IsOnValidMap())
                {
                    FFV_ScreenReaderMod.SpeakText(T("Not on map"), interrupt: true);
                    return;
                }
                CoroutineManager.StartUntracked(AnnounceEncounterState());
                return;
            }

            // Auto Detail is context-free (field, menus, battle) — unlike F5, which is
            // restricted to where mod configuration applies.
            if (GamepadManager.IsKeyCodePressed(KeyCode.F7))
            {
                FFV_ScreenReaderMod.ToggleAutoDetail();
                return;
            }

            if (GamepadManager.IsKeyCodePressed(KeyCode.F5))
            {
                // Set the enemy-HP display mode before combat, not during it — same gate as F8,
                // so mod configuration is reachable from the field and field menus but never
                // from battle or the title screen.
                if (IsFieldOrFieldMenuActive())
                {
                    int current = PreferencesManager.EnemyHPDisplay;
                    int next = (current + 1) % 3;
                    PreferencesManager.SetEnemyHPDisplay(next);
                    string[] options = { T("Numbers"), T("Percentage"), T("Hidden") };
                    FFV_ScreenReaderMod.SpeakText(string.Format(T("Enemy HP: {0}"), options[next]), interrupt: true);
                }
                else
                {
                    ControllerRouter.SpeakModMenuUnavailable();
                }
            }
        }

        private bool IsInBattle()
        {
            return Patches.ActiveBattleCharacterTracker.CurrentActiveCharacter != null;
        }

        private bool IsInputFieldFocused()
        {
            try
            {
                if (EventSystem.current == null) return false;
                var currentObj = EventSystem.current.currentSelectedGameObject;
                if (currentObj == null) return false;
                return currentObj.TryGetComponent(out UnityEngine.UI.InputField inputField);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error checking input field state: {ex.Message}");
                return false;
            }
        }

        internal static bool IsOnValidMap()
        {
            if (Patches.GameStatePatches.IsScreenFading) return false;
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            return playerController?.fieldPlayer != null;
        }

        /// <summary>
        /// Where mod-owned config hotkeys (F5, F8) apply: a live field map, including field
        /// menus — but never battle, the title screen, dialogue, or a cutscene.
        ///
        /// Deliberately NOT ControllerRouter.IsFieldActive, which additionally excludes menus
        /// because it also drives audio suppression, the entity scanner, and mod-mode teleport.
        ///
        /// The dialogue/event terms block the mod MENU (and F5/F8) mid-conversation without
        /// touching mod MODE, which stays reachable everywhere — see ControllerRouter's
        /// Back-button branch. Before input ran during events these were unreachable anyway;
        /// now that OnUpdate no longer early-returns, the gate has to be explicit.
        ///
        /// Does not apply to F1/F3: those are the game's own hotkeys that the mod polls without
        /// consuming and merely narrates, so their gate must mirror the game's own availability
        /// (IsOnValidMap alone) and must never be tightened, or the mod goes silent while the
        /// game still acts.
        /// </summary>
        internal static bool IsFieldOrFieldMenuActive()
        {
            return IsOnValidMap()
                && !ControllerRouter.IsInBattle
                && !Patches.DialogueTracker.IsInDialogue
                && !Patches.GameStatePatches.IsInEventState;
        }

        private bool IsStatusScreenActive()
        {
            if (cachedStatusController == null || cachedStatusController.gameObject == null)
            {
                cachedStatusController = GameObjectCache.Get<StatusDetailsController>();
            }

            return cachedStatusController != null &&
                   cachedStatusController.gameObject != null &&
                   cachedStatusController.gameObject.activeInHierarchy;
        }

        private bool IsBestiaryDetailActive()
        {
            if (!MenuStateRegistry.IsActive(MenuStateRegistry.BESTIARY_DETAIL))
                return false;
            if (cachedBestiaryInfoController == null || cachedBestiaryInfoController.gameObject == null)
                cachedBestiaryInfoController = GameObjectCache.Get<LibraryInfoController_KeyInput>();
            return cachedBestiaryInfoController != null &&
                   cachedBestiaryInfoController.gameObject != null &&
                   cachedBestiaryInfoController.gameObject.activeInHierarchy;
        }

        private static void AnnounceConfigTooltip()
        {
            try
            {
                var keyInputController = GameObjectCache.Get<ConfigActualDetailsControllerBase_KeyInput>();
                if (keyInputController == null)
                    keyInputController = GameObjectCache.Refresh<ConfigActualDetailsControllerBase_KeyInput>();

                if (keyInputController != null && keyInputController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => keyInputController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFV_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }

                var touchController = GameObjectCache.Get<ConfigActualDetailsControllerBase_Touch>();
                if (touchController == null)
                    touchController = GameObjectCache.Refresh<ConfigActualDetailsControllerBase_Touch>();

                if (touchController != null && touchController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => touchController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFV_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error reading config tooltip: {ex.Message}");
            }
        }

        private static string TryReadDescriptionText(System.Func<UnityEngine.UI.Text> getTextField)
        {
            try
            {
                var descText = getTextField();
                if (descText != null && !string.IsNullOrWhiteSpace(descText.text))
                    return descText.text.Trim();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error accessing description text: {ex.Message}");
            }
            return null;
        }

        private static System.Collections.IEnumerator AnnounceWalkRunState()
        {
            yield return null;
            yield return null;
            yield return null;
            bool isDashing = MoveStateHelper.GetDashFlag();
            FFV_ScreenReaderMod.SpeakText(isDashing ? T("Run") : T("Walk"), interrupt: true);
        }

        private static System.Collections.IEnumerator AnnounceEncounterState()
        {
            yield return null;
            try
            {
                var userData = Il2CppLast.Management.UserDataManager.Instance();
                if (userData?.CheatSettingsData != null)
                {
                    bool enabled = userData.CheatSettingsData.IsEnableEncount;
                    FFV_ScreenReaderMod.SpeakText(enabled ? T("Encounters on") : T("Encounters off"), interrupt: true);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error reading encounter state: {ex.Message}");
            }
        }
    }
}
