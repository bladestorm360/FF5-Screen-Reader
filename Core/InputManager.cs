using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using FFV_ScreenReader.Utils;
using MelonLoader;
using Il2CppSerial.FF5.UI.KeyInput;
using FFV_ScreenReader.Menus;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;

namespace FFV_ScreenReader.Core
{
    public class InputManager
    {
        private readonly FFV_ScreenReaderMod mod;
        private StatusDetailsController cachedStatusController;
        private readonly KeyBindingRegistry registry = new KeyBindingRegistry();

        public InputManager(FFV_ScreenReaderMod mod)
        {
            this.mod = mod;
            InitializeBindings();
        }

        /// <summary>
        /// Registers a field-only binding with a "Not available in battle" fallback for the Battle context.
        /// </summary>
        private void RegisterFieldWithBattleFeedback(KeyCode key, KeyModifier modifier, System.Action action, string description)
        {
            registry.Register(key, modifier, KeyContext.Field, action, description);
            registry.Register(key, modifier, KeyContext.Battle, NotAvailableInBattle, description + " (battle blocked)");
            registry.Register(key, modifier, KeyContext.Global, NotOnMap, description + " (no map)");
        }

        private static void NotAvailableInBattle()
        {
            FFV_ScreenReaderMod.SpeakText("Not available in battle", interrupt: true);
        }

        private static void NotOnMap()
        {
            FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
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

            // Status screen: bulk stat reading
            registry.Register(KeyCode.LeftBracket, KeyContext.Status, () => FFV_ScreenReaderMod.SpeakText(StatusDetailsReader.ReadPhysicalStats()), "Read physical stats");
            registry.Register(KeyCode.RightBracket, KeyContext.Status, () => FFV_ScreenReaderMod.SpeakText(StatusDetailsReader.ReadMagicalStats()), "Read magical stats");

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

            // --- Field: waypoint keys (with Global fallback) ---
            registry.Register(KeyCode.Comma, KeyModifier.Shift, KeyContext.Field, mod.CyclePreviousWaypointCategory, "Previous waypoint category");
            registry.Register(KeyCode.Comma, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Previous waypoint category (no map)");
            registry.Register(KeyCode.Comma, KeyModifier.None, KeyContext.Field, mod.CyclePreviousWaypoint, "Previous waypoint");
            registry.Register(KeyCode.Comma, KeyModifier.None, KeyContext.Global, NotOnMap, "Previous waypoint (no map)");
            registry.Register(KeyCode.Period, KeyModifier.Ctrl, KeyContext.Field, mod.RenameCurrentWaypoint, "Rename waypoint");
            registry.Register(KeyCode.Period, KeyModifier.Ctrl, KeyContext.Global, NotOnMap, "Rename waypoint (no map)");
            registry.Register(KeyCode.Period, KeyModifier.Shift, KeyContext.Field, mod.CycleNextWaypointCategory, "Next waypoint category");
            registry.Register(KeyCode.Period, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Next waypoint category (no map)");
            registry.Register(KeyCode.Period, KeyModifier.None, KeyContext.Field, mod.CycleNextWaypoint, "Next waypoint");
            registry.Register(KeyCode.Period, KeyModifier.None, KeyContext.Global, NotOnMap, "Next waypoint (no map)");
            registry.Register(KeyCode.Slash, KeyModifier.CtrlShift, KeyContext.Field, mod.ClearAllWaypointsForMap, "Clear all waypoints for map");
            registry.Register(KeyCode.Slash, KeyModifier.CtrlShift, KeyContext.Global, NotOnMap, "Clear all waypoints (no map)");
            registry.Register(KeyCode.Slash, KeyModifier.Ctrl, KeyContext.Field, mod.RemoveCurrentWaypoint, "Remove current waypoint");
            registry.Register(KeyCode.Slash, KeyModifier.Ctrl, KeyContext.Global, NotOnMap, "Remove waypoint (no map)");
            registry.Register(KeyCode.Slash, KeyModifier.Shift, KeyContext.Field, mod.AddNewWaypointWithNaming, "Add waypoint with name");
            registry.Register(KeyCode.Slash, KeyModifier.Shift, KeyContext.Global, NotOnMap, "Add waypoint (no map)");
            registry.Register(KeyCode.Slash, KeyModifier.None, KeyContext.Field, mod.PathfindToCurrentWaypoint, "Pathfind to waypoint");
            registry.Register(KeyCode.Slash, KeyModifier.None, KeyContext.Global, NotOnMap, "Pathfind to waypoint (no map)");

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
            registry.Register(KeyCode.I, KeyContext.Global, HandleItemInfoKey, "Item details");

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
            // Handle confirmation dialog first (consumes all input when open)
            if (ConfirmationDialog.HandleInput())
                return;

            // Handle text input window next (consumes all input when open)
            if (TextInputWindow.HandleInput())
                return;

            // Handle mod menu next (consumes all input when open)
            if (ModMenu.HandleInput())
                return;

            // Handle battle result navigator (consumes all input when open)
            if (BattleResultNavigator.HandleInput())
                return;

            if (!Input.anyKeyDown)
                return;

            if (IsInputFieldFocused())
                return;

            // F8 to open mod menu (unavailable in battle, not on map guard)
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (IsInBattle())
                    FFV_ScreenReaderMod.SpeakText("Unavailable in battle", interrupt: true);
                else if (!IsOnValidMap())
                    FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                else
                {
                    ModMenu.Open();
                    FFV_ScreenReaderMod.SpeakText("Mod menu", interrupt: true);
                }
                return;
            }

            // Determine active context
            KeyContext activeContext = DetermineContext();
            KeyModifier currentModifiers = GetCurrentModifiers();

            // Handle function keys (F1/F3/F5 — special coroutine/battle logic)
            HandleFunctionKeyInput();

            // Dispatch all registered bindings (includes V, I, and all other keys)
            DispatchRegisteredBindings(activeContext, currentModifiers);
        }

        /// <summary>
        /// Determine the current input context based on game state.
        /// </summary>
        private KeyContext DetermineContext()
        {
            if (IsStatusScreenActive())
                return KeyContext.Status;

            // Check for battle results screen before general battle context
            if (BattleResultDataStore.HasData)
                return KeyContext.BattleResult;

            if (IsInBattle() || Patches.BattleState.IsInBattle)
                return KeyContext.Battle;

            if (Patches.DialogueTracker.ValidateState() || Patches.ShopMenuTracker.IsInShopSession)
                return KeyContext.Global;

            if (IsOnValidMap())
                return KeyContext.Field;

            // Fallback: neither field nor battle (e.g., menus, fading)
            return KeyContext.Global;
        }

        /// <summary>
        /// Get the currently held modifier keys.
        /// </summary>
        private KeyModifier GetCurrentModifiers()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (ctrl && shift) return KeyModifier.CtrlShift;
            if (ctrl) return KeyModifier.Ctrl;
            if (shift) return KeyModifier.Shift;
            return KeyModifier.None;
        }

        private void DispatchRegisteredBindings(KeyContext activeContext, KeyModifier currentModifiers)
        {
            foreach (var key in registry.RegisteredKeys)
            {
                if (Input.GetKeyDown(key))
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
                FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                return;
            }

            MoveStateHelper.SyncWithActualGameState();
            if (MoveStateHelper.IsOnFoot())
            {
                bool isRunning = MoveStateHelper.GetDashFlag();
                FFV_ScreenReaderMod.SpeakText(isRunning ? "Running" : "Walking", interrupt: true);
            }
            else
            {
                int moveState = MoveStateHelper.GetCurrentMoveState();
                FFV_ScreenReaderMod.SpeakText(MoveStateHelper.GetMoveStateName(moveState), interrupt: true);
            }
        }

        private void HandleItemInfoKey()
        {
            if (Patches.ShopMenuTracker.ValidateState())
            {
                Patches.ShopDetailsAnnouncer.AnnounceCurrentItemDetails();
            }
            else if (Patches.ItemMenuTracker.ValidateState())
            {
                Patches.ItemDetailsAnnouncer.AnnounceEquipRequirements();
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
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (!IsOnValidMap())
                {
                    FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                    return;
                }
                CoroutineManager.StartUntracked(AnnounceWalkRunState());
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (!IsOnValidMap())
                {
                    FFV_ScreenReaderMod.SpeakText("Not on map", interrupt: true);
                    return;
                }
                CoroutineManager.StartUntracked(AnnounceEncounterState());
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (IsInBattle())
                {
                    int current = FFV_ScreenReaderMod.EnemyHPDisplay;
                    int next = (current + 1) % 3;
                    FFV_ScreenReaderMod.SetEnemyHPDisplay(next);
                    string[] options = { "Numbers", "Percentage", "Hidden" };
                    FFV_ScreenReaderMod.SpeakText($"Enemy HP: {options[next]}", interrupt: true);
                }
                else
                {
                    FFV_ScreenReaderMod.SpeakText("Only available in battle", interrupt: true);
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

        private bool IsOnValidMap()
        {
            if (Patches.GameStatePatches.IsScreenFading) return false;
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            return playerController?.fieldPlayer != null;
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

        private void AnnounceConfigTooltip()
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

        private string TryReadDescriptionText(System.Func<UnityEngine.UI.Text> getTextField)
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
            FFV_ScreenReaderMod.SpeakText(isDashing ? "Run" : "Walk", interrupt: true);
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
                    FFV_ScreenReaderMod.SpeakText(enabled ? "Encounters on" : "Encounters off", interrupt: true);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error reading encounter state: {ex.Message}");
            }
        }
    }
}
