# FF5 Screen Reader Mod — Architecture & Debug Reference

## Mod Architecture

### Core (`Core/`)
- `FFV_ScreenReaderMod` — Entry point, SpeakText(), SpeakTextDelayed(), entity refresh, scene transitions
- `InputManager` — Keybinding dispatch via KeyBindingRegistry. F1/F3/F5/F8/I/V inline
- `ModMenu` — Audio-only settings menu (F8), Windows API focus control
- `EntityCache` — Caches field entities; grouping via IGroupingStrategy
- `EntityNavigator` — Entity cycling with timing-aware filters (OnAdd/OnCycle)
- `Filters/` — IEntityFilter (FilterTiming, FilterContext), CategoryFilter, PathfindingFilter, ToLayerFilter, IGroupingStrategy, MapExitGroupingStrategy
- `PreferencesManager` — 17 MelonPreferences entries (added ExpCounter toggle + volume)
- `BattleResultNavigator` — Focus-stealing navigable results window (L key)
- `WaypointController` — Waypoint CRUD (add, rename, remove, cycle, pathfind)
- `AudioLoopManager` — 3 audio loops, toggles, battle/dialogue suppression (singleton)
- `NavigationStateSnapshot` — Struct for save/restore of 5 navigation booleans
- `KeyBinding` + `KeyBindingRegistry` — Declarative keybinding with KeyModifier/KeyContext

### Patches (`Patches/`)
- `CursorNavigationPatches` — Cursor movement announcements
- `MessagePatches` — Dialog/message text + DialogueTracker
- `BattleCommandPatches` — Battle command selection
- `BattleTargetPatches` — Battle target selection
- `BattleMessagePatches` — Damage/heal/status + BattleCommandMessagePatches (defeat)
- `BattleResultPatches` — Per-phase: ShowPointsInit (totals), ShowPointsExit (counter stop), SetData (stat diffs), abilities, items, EndWaitInit (cleanup)
- `ItemMenuPatches` — Item menu + ItemMenuTracker + ItemUseTracker
- `ItemDetailsAnnouncer` — I key equipment compatibility
- `JobAbilityPatches` — Job/ability menus + JobAbilityTrackerHelper
- `ConfigMenuPatches` — Config menu
- `MovementSpeechPatches` — Movement announcements + vehicle state transitions
- `MovementSoundPatches` — Footstep audio
- `ShopPatches` — Shop menus + ShopMenuTracker + equipment command bar
- `PopupPatches` — All popup types (common, game over, info, job change, change name)
- `GameStatePatches` — BattleState, map transitions
- `TitleMenuPatches` — Title screen
- `DashFlagPatches` — Walk/run state for F1
- `MainMenuPatches` — In-game main menu + state cleanup
- `SaveLoadPatches` — Save/load menus + confirmation popups
- `NamingPatches` — Name entry screen

### Field (`Field/`)
- `NavigableEntity` — Entity wrapper (TreasureChestEntity overrides FormatDescription)
- `GroupEntity` — Grouped entities, delegates to IGroupingStrategy representative
- `EntityFactory` — Creates entities, filters duplicates (goName + entityName checks)
- `FieldNavigationHelper` — Pathfinding, distance, terrain attributes, landing detection

### Menus (`Menus/`)
- `MenuTextDiscovery` — Generic UI hierarchy text discovery
- `SaveSlotReader`, `StatusDetailsReader`, `CharacterSelectionReader`, `ConfigMenuReader`

### Utils (`Utils/`)
- `TolkWrapper` — NVDA interface
- `CoroutineManager` — Managed coroutines, self-cleanup, max 20, StartUntracked/StopManaged
- `SoundPlayer` — waveOut playback (6 channels, looping, LRU tone cache, 16-bit)
- `ToneGenerator` — Tone generation + WriteWavHeader
- `GameConstants` — Audio, tile size, direction vectors, map IDs
- `GameObjectCache` — Cached lookups (Get/Refresh pattern)
- `TextUtils`, `CollectionHelper`, `DirectionHelper`, `PlayerPositionHelper`
- `AnnouncementDeduplicator` + `AnnouncementContexts` — Dedup (string/int/object)
- `LocalizationHelper` — MessageManager wrapper + 12-language mod string dictionary
- `BattleResultDataStore` — Static data store for navigator (points + stats)
- `BattleUnitHelper`, `CharacterStatusHelper`, `SelectContentHelper`
- `WindowsFocusHelper` — VK constants, focus management

## Key Game Namespaces

**Il2CppLast.*** — Battle, Map (MapManager, FieldController, FieldMap), Entity.Field, UI (Cursor), UI.KeyInput/Touch, UI.Message, Data.Master, Data.User, Defaine

**Il2CppSerial.FF5.*** — UI.KeyInput (AbilityContentListController, AbilityCommandController, AbilityChangeController, AbilityUseContentListController, JobChangeWindowController, BattleQuantityAbilityInfomationController), UI.Touch (ResultStatusUpController)

## Job & Ability Classes

### Data (Il2CppLast.Data.Master.*)
- **Job** (353633): Id, MesIdName, MesIdDescription, Strength/Vitality/Agility/Magic
- **Ability** (345749): Id, UseValue (MP), AbilityLv, TypeId
- **Command** (348974): Id, MesIdName, MesIdDescription, CommandLv

### User Data (Il2CppLast.Data.User.*)
- **OwnedAbility** (363837): Ability, SkillLevel (0=not learned), MesIdName, MesIdDescription
- **OwnedJobData** (364638): Id, Level, CurrentProficiency
- **OwnedCharacterData** (363962): Name, Parameter, OwnedAbilityList, OwnedJobDataList
- **AbilityEquipData** (291695): Index, ContentId, Job, JobLevel, Ability, Command, IsEquiped, IsFocus
- **CharacterParameterBase** (342439): CurrentHP/MP, BaseMaxHp/Mp
- **PlayerCharacterParameter** (343918): ConfirmedMaxHp/Mp/ConditionList

### Controllers (Il2CppSerial.FF5.UI.KeyInput.*)
- **AbilityContentListController** (285082): Patched via `SetCursor(Cursor,bool,WithinRangeType,bool)` — SelectContent has ambiguous overloads
- **AbilityCommandController** (284786): `SelectContent(int)` — command slots
- **AbilityChangeController** (286594): `SelectContent(int,WithinRangeType)` + `SelectCommand(int)` — equip abilities
- **AbilityUseContentListController** (285635): `SelectContent(IEnumerable,Cursor)` — target selection
- **JobChangeWindowController** (287201): `SelectContent(int,WithinRangeType)` — job selection

## Common Patterns
- **IL2CPP prefix**: `IL2CppLast.Map.FieldController`
- **Harmony**: `[HarmonyPatch(typeof(Class), nameof(Class.Method))]`
- **Caching**: `GameObjectCache.Get<T>()` with `Refresh<T>()` fallback
- **Coroutines**: `CoroutineManager.StartManaged()`
- **Speech**: `SpeakText(text, interrupt)` / `SpeakTextDelayed(text, 0.3f)` for post-focus-change
- **Dedup**: `AnnouncementDeduplicator.ShouldAnnounce(context, value)`
- **Content access**: `SelectContentHelper.TryGetItem(list, index)`
- **FieldController access**: `GameObjectCache.Get<FieldMap>()?.fieldController` (NOT direct Get<FieldController>)

---

## Debug History

### Spell List — SetCursor Workaround (2024-12-28)
**Problem**: Main menu spell list silent. AbilityContentListController.SelectContent has ambiguous overloads; all Harmony patch attempts failed (ambiguous match, crash on set_SelectedIndex).

**Root cause**: KeyInput version has `SelectContent(int)` public + `SelectContent(Cursor,WithinRangeType,bool)` private — Harmony can't disambiguate. Navigation uses OnSelect callback, not SelectContent.

**Solution**: Patch `SetCursor(Cursor,bool,WithinRangeType,bool)` — unique 4-param signature. Access `AbilityList[targetCursor.Index]`. Added ability_command skip in CursorNavigationPatches to prevent double-announce. Format: "Cure, MP 4" / "Cura, MP 9, Not learned" / "Empty".

**Lesson**: When SelectContent has ambiguous overloads, look for SetCursor or other navigation-chain methods with unique signatures.

### I Key Tracker Mutual Exclusion
**Problem**: I key silent in config menu — AbilityMenuTracker's `activeInHierarchy` check stayed true after menu close.

**Solution**: Mutual exclusion — each menu open clears all other trackers. Created ItemMenuTracker, ItemDetailsAnnouncer, JobAbilityTrackerHelper.

**Lesson**: `activeInHierarchy` unreliable for menu state; use explicit tracker registration/deregistration.

### Shop Equipment Command Bar
**Problem**: Equipment command bar silent from shop. Controller-level SetFocus fires once on entry, not per-navigation. Cursor blocked by "shop" exclusion pattern.

**Solution**: View-level `EquipmentCommandView.SetFocus(bool)` patch for main menu path. For shop path, targeted bypass in CursorExclusionHelper when `EnteredEquipmentFromShop` is true. Dual-state pattern (clear shop state on entry, restore on return).

### Sound System & ModMenu Port (2026-01-29)
Ported from FF1: CoroutineManager rewrite (self-removing wrappers, bi-directional mapping), IList parameter to fix .ToArray() memory leak, volume caching, ModMenu (F8, audio-only with Windows API focus control), F1/F3/F5 function key announcements, DashFlagPatches.

### Battle/Dialogue Navigation Suppression (2026-01-30)
**Architecture**: BattleState (GameStatePatches) captures NavigationStateSnapshot on battle start, restores on map transition. DialogueTracker (MessagePatches) suppresses/restores on dialogue open/close. AudioLoopManager has SuppressNavigation/RestoreNavigation methods. KeyBindingRegistry.RegisterFieldWithBattleFeedback() for "Not available in battle" feedback.

**Hooks**: SetCommandData→battle start, CheckMapTransition→battle end, SetContent→dialogue start, MessageWindow_Close→dialogue end.

### Performance Optimization (2026-01-30)
Replaced FindObjectOfType with state flags (ItemUseTracker.IsItemUseActive, BattleState.IsInBattle) and GameObjectCache. Pre-allocated WaitForSeconds. Created GameConstants.cs. Removed dead code.

**Lesson**: State flags from Show/Close patches are O(1) vs O(n) FindObjectOfType.

### Game Over Popup + Defeat Message (2026-01-31)
**Problem**: Defeat message uses BattleCommandMessageController.SetMessage, not ScrollMessageManager.Play. GameOverLoadPopup is separate from GameOverSelectPopup.

**Solution**: BattleCommandMessagePatches with runtime FindType(). GameOverLoadPopup offsets: title=0x38, message=0x40, cursor=0x58, cmdList=0x60. Controller→view(0x30)→loadPopup(0x18)→messageText(0x40).

### Save Slot Popup Re-read (2026-02-02)
**Problem**: Re-selecting save slot only read "No", not popup message. `Popup.Open()` only called on first show; re-shows use `SetPopupActive(true)`.

**Solution**: Call ReadSavePopupMessage() directly in LoadGameWindowSetPopupActive_Postfix (same pattern as InterruptionSetEnablePopup).

### Vehicle Disembark -1 State (2026-02-02)
**Problem**: "On foot" never announced. FF5 disembark sequence: `2→-1→1`. `-1` not in intermediate states, and `previousId==-1` early return blocked the final transition.

**Solution**: Added `-1` to IsIntermediateTransportation(). Removed early return. Track pre-intermediate state for proper "exiting intermediate" announcement.

**Lesson**: FF5 uses -1 as intermediate state during disembark; GetOff is never called, only ChangeTransportation.

### Delayed Announcements for Focus Changes (2026-02-03)
**Problem**: NVDA announces window title on focus change before mod speech.

**Solution**: 0.3s delay via `SpeakTextDelayed()` for all dialog open/close callbacks. ConfirmationDialog.cs uses coroutine-delayed prompt.

### Deep Refactoring (2026-02-08)
9-phase extraction: FFV_ScreenReaderMod 1688→742, SoundPlayer 1335→873, InputManager 744→448. New files: PreferencesManager, WaypointController, AudioLoopManager, NavigationStateSnapshot, ToneGenerator, KeyBinding, KeyBindingRegistry, MainMenuPatches.

### Audio System
- 16-bit PCM (wBitsPerSample=16, nBlockAlign=4, buffer=32768)
- Loop safety: bounded while loops, manual Time.time timing, battle/scene suppression
- Volume: ScaleSamples(), volume-baked wall tone generation
- LRU tone cache: max 16, keyed by (directionMask, volume)

### Speech Redundancy Fixes (2026-02-06)
1. TreasureChestEntity: override FormatDescription to avoid double type name
2. Map transitions: skip SystemMessage if it matches current map name
3. Save/load popup: check SaveLoadMenuState.IsActive before PopupState.ShouldSuppress

### Vehicle Entity Tracking (2026-02-06)
**Problem**: PopulateVehicleTypeMap populated dict but didn't add mapObjects to results list.

**Solution**: Pass results list, add mapObjects, filter disabled vehicles (enabled check).

### Battle Miss NonView (2026-02-09)
**Problem**: Steal/Focus announced "Miss". CreateDamageView 5th param MissType.NonView means no visual display.

**Solution**: Capture missType param; suppress announcement when NonView. Removed blanket `value==0` workaround.

### Landing Ping Terrain Detection (2026-02-07)
Rewrote GetNearbyLandingSpots() using terrain attributes instead of movement checks. Landing = in LandingList OR not in OkList. APIs: ConvertWorldPositionToCellPosition, GetCellAttribute, CheckLandingList, CheckOkList. Uses TransportationInfo.Id (not Type). Ghidra confirmed CheckOkList/CheckLandingList are boolean arrays indexed by (attribute-1).

**Bug**: FieldController not directly cacheable — must go through `GameObjectCache.Get<FieldMap>()?.fieldController`.

### Entity Scan Paren Fix + Japanese Save Point (2026-02-11)
1. "here" replacement: `IndexOf→LastIndexOf` to target last paren group (distance/direction), not entity name parens
2. Japanese save point duplicate: added `セーブポイント` name filter in EntityFactory alongside goName "SavePoint" filter

### Entity Filter Refactor (2026-02-11)
Replaced standalone PathfindingFilter with IEntityFilter interface + FilterTiming (OnAdd=cheap checks, OnCycle=expensive). IGroupingStrategy for dynamic group formation/dissolution. FilterContext created per cycle.

### Boko Map Transition Fix (2026-02-11)
OnMapTransition() forced on-foot while game still had player on Boko. Fix: check `player.moveState` before forcing; if still vehicle, defer to ChangeMoveState_Patch.

### Code Cleanup (2026-02-11)
VK constant dedup (ConfirmationDialog, TextInputWindow → WindowsFocusHelper). SelectContentHelper.TryGetItem adopted at 11 sites across 6 files. Dead code removal.

### Per-Phase Battle Results (2026-02-11)
Hooks: ShowPointsInit (EXP/Gil/ABP), ResultStatusUpController.SetData (level-up, in Serial.FF5.UI.Touch namespace — manual FindType patch), ShowGetAbilitysInit/ShowLevelUpAbilitysInit (abilities via UI text), ShowGetItemsInit (items via GetContentDataList). Status-up: 1-frame delay, heuristic (category, before, after) triple detection. Battle action dedup: object-based identity instead of string.

**Status**: AWAITING IN-GAME TESTING — temp logging active, needs verification then cleanup.

### Battle Results: Stat Gains, EXP Format & Navigator (2026-02-12)

**Changes:**
1. **EXP totals-only speech**: ShowPointsInit now speaks "{EXP} EXP, {ABP} ABP, {Gil} Gil" (totals). Per-character EXP removed from speech, available via navigator.
2. **Stat gains from data**: SetData_Postfix now takes `__0` (BattleResultCharacterData), extracts HP/MP diffs via `BeforData.parameter.ConfirmedMaxHp()` vs `AfterData.parameter.ConfirmedMaxHp()`. Format: "Bartz: HP +15, MP +5". Falls back to UI text if data extraction fails.
3. **EXP counter sound**: Rapid 2000Hz beep loop on Counter channel during ShowPointsInit→ShowPointsExit. Toggle + volume in ModMenu. Uses ToneGenerator.GenerateLandingPing for beep+silence pattern.
4. **Battle Result Navigator** (L key): Focus-stealing window following TextInputWindow pattern. Grid navigation (Up/Down=rows with full row readout, Left/Right=columns, Enter/Home=full row, Escape=close). Points grid: characters x {EXP, Next, ABP}. Stats grid: {CharName: Stat} x {Before, After, Change}. 12-language headers.
5. **Data lifecycle**: ShowPointsInit stores points data (incl. NextExp via GetNextExp()), SetData stores stats data, EndWaitInit clears all. BattleResultDataStore.HasData drives KeyContext.BattleResult.
6. **EXP counter auto-stop**: MonitorExpCounterAnimation coroutine polls pointer chain (ResultMenuController→pointController→characterListController) every 100ms. Reads perormanceEndCount vs contentList.Count via Marshal.ReadInt32/ReadIntPtr. Stops counter when animation completes. Safety net stops in ShowStatusUpInit/ShowGetAbilitysInit/ShowGetItemsInit/EndWaitInit remain.

**Architecture**: New `KeyContext.BattleResult` active only when data store has data. `SoundChannel.Counter` (6th channel). `BattleResultDataStore` centralizes data between patches and navigator.

### ABP Column Fix (2026-02-12)
**Problem**: Battle result navigator ABP column showed incorrect values. Two bugs in `BattleResultPatches.cs` ABP computation:
1. Used `GetExpTableGroupId()` (character EXP table group) as first arg to `ExpUtility.GetNextExp` — wrong for ABP lookup.
2. Used `AfterData.OwnedJob` — correct (post-battle proficiency matches game display).

**Fix**: Changed `GetExpTableGroupId()` → `ownedJob.Id` (job ID). The game's `ResultCharacterListContentController.SetData` passes the job ID as the group parameter for `ExpTableType.JobExp` lookups. Kept `AfterData` for proficiency source (matches game's displayed remaining ABP).

**Lesson**: `GetExpTableGroupId()` returns a group ID for character-level EXP tables, not ABP/job tables. For `ExpTableType.JobExp`, use `OwnedJobData.Id` as the group parameter.

### Naming Popup Enhancements (2026-02-12)
1. **CommonPopup initial button**: Read `selectCursor` at offset 0x68, get button text via ReadButtonFromCommandList. Wrapped in try/catch. Result: "Bartz. Use this name? Yes"
2. **ChangeNamePopup hint**: Append `LocalizationHelper.GetModString("default_name_hint")` (12 languages). Result: "Enter a name for Bartz. Press Enter for default name"
