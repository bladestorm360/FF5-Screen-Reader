# FF5 Screen Reader Mod — Architecture & Debug Reference

## Mod Architecture

### Core (`Core/`)
- `FFV_ScreenReaderMod` — Entry point, SpeakText(), SpeakTextDelayed(), entity refresh, scene transitions, IsAccessibilityEnabled + ToggleAccessibility() (Ctrl+F8 kill switch: disable stops all coroutines, resets all state trackers, clears audio; enable reinitializes preferences, recaches game objects, rescans entities, announces map)
- `InputManager` — Keybinding dispatch via KeyBindingRegistry. F1/F3/F5/F8/I/V/Shift+I inline. Unity `Input.GetKeyDown`/`Input.GetKey` for game input. KeyContext-based dispatch (Global, Field, Battle, BattleResult, Status, Bestiary)
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
- `GameStatePatches` — BattleState, map transitions, IsInEventState (cached via ChangeState hook)
- `TitleMenuPatches` — Title screen
- `DashFlagPatches` — Walk/run state for F1
- `MainMenuPatches` — In-game main menu + state cleanup
- `SaveLoadPatches` — Save/load menus + confirmation popups
- `NamingPatches` — Name entry screen
- `BestiaryPatches` — Bestiary (Picture Book) accessibility: state tracking, list nav, detail stats, formations, maps
- `MusicPlayerPatches` — Music Player (Extra Sound) accessibility: state tracking, song list nav, Play All/Arrangement toggle
- `GalleryPatches` — Gallery (Extra Gallery) accessibility: state tracking, list nav, image open/return

### Field (`Field/`)
- `NavigableEntity` — Entity wrapper (TreasureChestEntity overrides FormatDescription)
- `GroupEntity` — Grouped entities, delegates to IGroupingStrategy representative
- `EntityFactory` — Creates entities, filters duplicates (goName + entityName checks)
- `FieldNavigationHelper` — Pathfinding, distance, terrain attributes, landing detection

### Menus (`Menus/`)
- `MenuTextDiscovery` — Generic UI hierarchy text discovery
- `SaveSlotReader`, `StatusDetailsReader`, `CharacterSelectionReader`, `ConfigMenuReader`
- `BestiaryReader` — Data extraction/speech formatting for bestiary entries, stats, formations, maps
- `BestiaryNavigationReader` — Virtual buffer navigation for bestiary detail stats (arrow keys, group jump)
- `MusicPlayerReader` — Data extraction/speech formatting for music player song entries

### Utils (`Utils/`)
- `TolkWrapper` — NVDA interface
- `CoroutineManager` — Managed coroutines, self-cleanup, max 20, StartUntracked/StopManaged
- `SoundPlayer` — WaveOut audio playback (5 channels, P/Invoke winmm, hardware looping)
- `ToneGenerator` — Tone generation + WriteWavHeader
- `GameConstants` — Audio, tile size, direction vectors, map IDs
- `GameObjectCache` — Cached lookups (Get/Refresh pattern)
- `TextUtils`, `CollectionHelper`, `DirectionHelper`, `PlayerPositionHelper`
- `AnnouncementDeduplicator` + `AnnouncementContexts` — Dedup (string/int/object)
- `LocalizationHelper` — MessageManager wrapper + 12-language mod string dictionary
- `BattleResultDataStore` — Static data store for navigator (points + stats)
- `BattleUnitHelper`, `CharacterStatusHelper`, `SelectContentHelper`
- `EntityTranslator` — JSON-based Japanese→English name translation (4-tier lookup) + nested `EntityDump` (key 0)
- `AudioChannel` — WaveOut backend (winmm P/Invoke, per-channel waveOut handles)
- `WindowsFocusHelper` — Win32 focus stealing for mod windows
- `VKConstants` (in ConfirmationDialog/TextInputWindow) — VK constant definitions for Win32 key input

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

### Music Player Classes (Il2CppLast.*)
- **SubSceneManagerExtraSound** (Last.Management): State machine — Init=0, View=1, GotoTitle=2. `ChangeState(State)` triggers transitions.
- **ExtraSoundController** (Last.UI.KeyInput): Main controller with `listController`, `toggleController` (Touch.ToggleController), `loopKeys` (PlaybackOn=0, PlaybackOff=1). `ChangeKeyHelpPlaybackIcon(LoopKeys)` fires on Play All toggle.
- **ExtraSoundListController** (Last.UI.KeyInput): Song list management with `currentContent` (set-only), `mainController`. `SwitchOriginalArrangeList()` toggles arrangement/original. `set_CurrentContent(ExtraSoundListContentController)` fires on cursor movement.
- **ExtraSoundListContentController** (Last.UI.KeyInput): Individual song item with `ContentInfo` (ExtraSoundListContentInfo), `Index`.
- **ExtraSoundListContentInfo** (Last.UI.KeyInput): Data class with `musicName` (string), `playTime` (int), `bgmId` (int).
- **ToggleController** (Last.UI.Touch): Arrangement toggle with `ToggleState` bool (true=Arrangement, false=Original).

### Gallery Classes (Il2CppLast.*)
- **SubSceneManagerExtraGallery** (Last.Management): State machine — Init=0, View=1, Details=2, GotoTitle=3. `ChangeState(State)` triggers transitions.
- **GalleryContentListController** (Last.UI.KeyInput, 9266): Individual gallery item with `contentData` at 0x18 (GalleryListCotentData). `SetFocus(bool)` fires on cursor movement.
- **GalleryListCotentData** (Last.OutGame.Gallery, 6406): Data class with `number` (int, 0x10), `name` (string, 0x18), `isFocus` (bool, 0x20).

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

### Bestiary (Picture Book) Accessibility (2026-02-13)
**Feature**: Full screen reader support for the bestiary/encyclopedia (Title > Extra > Picture Book).

**Architecture**:
- `SubSceneManagerExtraLibrary.ChangeState` — central state tracker (List=1, Field=2, Dungeon=3, Info=4, ArTop=5, ArBattle=6)
- `LibraryMenuController.Show` (KeyInput) — fires when list cursor moves, announces entry
- `LibraryInfoController.SetData` (KeyInput) — fires when detail view opens, builds stat buffer from `LibraryInfoContent` UI elements
- `ExtraLibraryInfo.OnNextPageButton`/`OnPreviousPageButton` — page turns rebuild stat buffer
- `ExtraLibraryInfo.OnChangedMonster` — monster switching in detail view
- `ArBattleTopController` — formation data via Traverse (monsterPartyList, selectMonsterPartyIndex)

**Key classes**: `BestiaryStateTracker`, `BestiaryNavigationTracker`, `BestiaryReader`, `BestiaryNavigationReader`

**Stat buffer**: Dynamically built from active UI elements (`LibraryInfoContent.monsterDataTable`, `statusTable`, `optionTable`, `hierarchyTable`). Items group reads from `Monster` master data instead of UI (game uses icons without text). Only includes entries whose GameObjects are activeInHierarchy — ensures parity with sighted player.

**Monster name resolution** (for formations): `MasterManager.Instance.GetList<Monster>()` → dictionary lookup by ID → `Monster.MesIdName` → `MessageManager.GetMessage()`.

**Item name resolution**: `MonsterData.MonsterMaster` → `StealContentId1-4`/`DropContentId1-8` → `MasterManager.GetList<Content>()` → `Content.MesIdName` → `LocalizationHelper.GetGameMessage()`.

**Minimap (list view overlay)**: `LibraryMenuController.selectState` field (0=MonsterList, 1=EnlargedMap) at offset 0x44, `selectMapIndex` at offset 0x50, both accessed via direct IL2CPP properties (Traverse fails on IL2CPP nested enums). Caches entry name on open. Announces "Minimap open: [map name]" / "Minimap closed. [cached entry name]" on state transitions, and just "[map name]" on habitat cycling.

**Full map (list view → map → list, state 1→2→1)**: State 2 (Field). `ExtraLibraryField.NextMap()`/`PreviousMap()` hooked for habitat cycling. Map index tracked manually in `BestiaryStateTracker.FullMapIndex` (reset to 0 on state 2 entry). **Caching**: `CurrentMonsterData` (IL2CPP reference) becomes stale after scene transition (list scene unloads, GC collects MonsterData during 2-frame yield). Entry name and habitat names are cached as plain C# strings in `BestiaryStateTracker.CachedEntryName`/`CachedHabitatNames` BEFORE the `AnnounceMapView` coroutine starts. Announces "Map open: [cached habitat]" on entry, "[cached habitat]" on cycle, "Map closed. [cached entry]" on return to list (state 1 with previousState==2).

**Bug fix** (2026-02-14): Initial entry not announced on bestiary open. `Show` and `OnContentSelected` don't fire during initial list population — only on user cursor movement. Fix: `AnnounceListOpen()` queries `FindObjectOfType<LibraryMenuListController_KeyInput>().GetCurrentContent()` directly after 3 yield frames (1 before summary + 2 after), bypassing cache timing issues. Removed UpdateView patch (Patch 2c) and cache-restore dance in ChangeState — no longer needed.

**Bug fix** (2026-02-14): Map announcements missing habitat/entry names. Full map said "Map open" with no habitat, cycling was silent, close had no entry re-announcement. Minimap close also missing entry name. **Root cause**: `CurrentMonsterData` (IL2CPP object reference) becomes stale after scene transition — list scene unloads when full map loads, GC collects `MonsterData` during 2-frame yield in `AnnounceMapView`. **Fix**: Cache entry name and all habitat names as plain C# strings (`BestiaryStateTracker.CachedEntryName`/`CachedHabitatNames`) BEFORE starting coroutines. Minimap caches entry name on open (selectState→1), uses it on close (selectState→0). Full map caches both before `AnnounceMapView`, uses cached data in coroutine, cycle helper, and return-to-list handler. Also fixed incorrect state transition assumption: full map returns to list (state 1→2→1), not detail (4→2→4) — removed wrong "Map closed" from case 4.

**Bug fixes** (2026-02-13):
1. **Shift+I key help**: Reads active `KeyIconView` objects (button labels + action text). Global binding.
2. **Minimap toggle silent**: Tracked `selectState` in Patch 6 (UpdateController). Announces "Map shown"/"Map hidden" on Right/Left key in list view.
3. **Items show "None"**: `dropTable` UI uses icons without text. Replaced with master data lookup: `MonsterData.MonsterMaster` → `StealContentId1-4`/`DropContentId1-8` → `Content.MesIdName`.

**Screenshot corrections** (2026-02-13): Fixed 4 mismatches vs game display:
1. Unencountered entries: "Unknown" → "???" (game shows "???")
2. Header: "Picture Book" → "Bestiary" (game header says "Bestiary")
3. `GetParamValueText`: Expanded from 2 to 7 text fields (`valueText`, `multipliedValueText`, `parameterValueText`, `percentText`, `persentValueText`, `defaultValueText`, `multipliedText`). Private IL2CPP fields accessed via lowercase names. Fallback changed from "???" to empty string.
4. `ReadParamValueArray` fallback: "???" → empty string (consistent with GetParamValueText).

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
VK constant dedup (ConfirmationDialog, TextInputWindow). SelectContentHelper.TryGetItem adopted at 11 sites across 6 files. Dead code removal.

### Per-Phase Battle Results (2026-02-11)
Hooks: ShowPointsInit (EXP/Gil/ABP), ResultStatusUpController.SetData (level-up, in Serial.FF5.UI.Touch namespace — manual FindType patch), ShowGetAbilitysInit/ShowLevelUpAbilitysInit (abilities via UI text), ShowGetItemsInit (items via GetContentDataList). Status-up: 1-frame delay, heuristic (category, before, after) triple detection. Battle action dedup: object-based identity instead of string.

**Status**: Verified in-game.

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

### Spell List — contentList Fix (2026-02-12)
**Problem**: All spell slots reading as "Empty". Two prior approaches failed:
1. Original: `AbilityList[targetCursor.Index]` — `AbilityList` is compact (learned spells only), but `targetCursor.Index` maps to the visual grid (includes empty slots for unlearned spells), causing index mismatch after the first tier.
2. Previous fix: `__instance.SelectedOwnedAbility` — auto-property only updated by `SelectContent()`, not during cursor movement (`SetCursor`), so always null.

**Solution**: Read private `contentList` field (offset 0x50) via IL2CPP pointer access. This is `List<BattleAbilityInfomationContentController>` indexed by visual grid position. Each controller's `.Data` property returns the `OwnedAbility` at that slot (null for empty/unlearned). Uses established unsafe pointer pattern (see `PopulateVehicleTypeMap`, `CacheTerrainMappingData`).

**Lesson**: When a list controller has both a compact data list and a visual content list, always use the content list (indexed by visual cursor position) for cursor-driven navigation.

### ConditionType 4 Mislabel Fix (2026-02-12)
**Problem**: ConditionType 4 labeled "KO" in CharacterStatusHelper fallback names. Diagnostic logging showed characters with HP and Poison also showing "KO". ConditionType 4 is `Dying` (critical/low-HP flag), not actual KO — ConditionType 5 (`UnableFight`) is real KO.

**Fix**: Renamed fallback for ConditionType 4 from "KO" to "Critical" in `CharacterStatusHelper.ConditionTypeFallbackNames`.

### Entity Translator + Entity Dump (2026-02-12)
**Feature**: Translates Japanese NPC/entity names to English via external JSON file (`UserData/EntityNames.json`). Ported 4-tier lookup from FF4: exact match → strip numeric/SC prefix → strip circled suffix → strip both. EntityDump (key 0) collects Japanese entity names per map and writes to JSON. Duplicate detection: compares existing map entries, only adds truly new names.

**Architecture**: `Utils/EntityTranslator.cs` (static class + nested `EntityDump`). `NavigableEntity.Name` property calls `Translate()`. `EntityFactory.ContainsJapaneseCharacters` changed to `internal` for dump filtering. JSON load/save follows WaypointManager pattern (manual parse/serialize, UTF-8).

### Battle Target Status Effects (2026-02-12)
**Feature**: Target selection now shows status conditions after HP/MP. Player: "Name: HP x/y. MP x/y. status: Poison, Blind". Enemy: "Name: HP x/y. status: Poison". Reuses `CharacterStatusHelper.GetStatusConditions()` (same whitelist, localized names, fallback names).

### Naming Popup Enhancements (2026-02-12)
1. **CommonPopup initial button**: Read `selectCursor` at offset 0x68, get button text via ReadButtonFromCommandList. Wrapped in try/catch. Result: "Bartz. Use this name? Yes"
2. **ChangeNamePopup hint**: Append `LocalizationHelper.GetModString("default_name_hint")` (12 languages). Result: "Enter a name for Bartz. Press Enter for default name"

### Key Help Pagination Fix (2026-02-14)
**Problem**: Shift+I (`AnnounceKeyHelp`) only read the currently-visible page of controls from `KeyHelpController`. The game paginates controls across multiple pages (cycling on a timer), so pressing Shift+I gave random partial results depending on which page was showing.

**Solution**: Added `ReadAllKeyHelpItems()` which reads the `pageList` field (offset 0x30) directly via unsafe pointers. This is `List<List<KeyHelpIconData>>` containing ALL pages. Each `KeyHelpIconData` has `MessageId` (0x10), `MessageId2` (0x18), and `Keys` (0x20, `Key[]` value-type array). Message IDs are resolved via `LocalizationHelper.GetGameMessage()`. Key enum values are mapped to display names via `GetKeyDisplayName()`. Falls back to the original `GetComponentsInChildren<KeyIconView>` approach if unsafe read fails.

**Key detail**: `Key[]` is a value-type array (4 bytes per element), not a reference array (8 bytes). Data starts at array offset 0x20.

### Music Player Duration Fix (2026-02-14)
**Problem**: All song durations showed "0:00". `LookupDuration()` correctly returned `entry.Time` (e.g., 153 for "Main Theme of Final Fantasy V" = 2:33), but `FormatPlayTime()` divided by 1000 assuming milliseconds. Integer division `153 / 1000 = 0`.

**Fix**: `SoundPlayerList.Time` is in seconds, not milliseconds. Removed the `/ 1000` division in `FormatPlayTime()`. Also cleaned up one-shot diagnostic logging (`_loggedDiagnostics`, `shouldLog`) that was no longer needed.

**Lesson**: `SoundPlayerList.Time` returns seconds. The IL2CPP dump annotation `playTime` on `ExtraSoundListContentInfo` is misleading — it's not milliseconds.

### Save Complete Popup Announcement (2026-02-13)
**Problem**: "Save Complete" popup text never spoken after Quick Save or Normal Save. Only "Close" button was read.

**Root cause**: Completion text lives on `savePopup` (0x38), not `commonPopup` (0x28). The original `DelayedSaveCompleteRead` coroutine read from `commonPopup` and got nothing. Meanwhile, `SavePopup.UpdateCommand` fires on the completion popup (reuses same `SavePopup` instance), but `lastPopupButtonIndex` was still set from the confirmation dialog — so the "first call" path (which reads title+message via `DelayedSavePopupRead`) was skipped, and only the button text ("Close") was announced.

**Solution**: Reset `lastPopupButtonIndex = -1` in both `InterruptionInitComplite_Postfix` (Quick Save) and `SaveWindowCompleteInit_Postfix` (Normal Save). This causes the next `SavePopup.UpdateCommand` call to treat it as a fresh popup, triggering `DelayedSavePopupRead` which reads title+message from the `savePopup`'s own text fields (0x38/0x40). Removed the old `DelayedSaveCompleteRead` calls that read from the wrong popup.

**Lesson**: When a popup reuses the same `SavePopup` instance for different screens (confirmation → completion), the dedup index must be reset at each transition so the first-call path re-triggers.

### Global Hotkey Focus Gating (2026-02-14 → 2026-02-15)
**Problem**: Mod hotkeys (G, M, H, brackets, etc.) fired when the game window was not focused when using `GetAsyncKeyState`.

**Solution (reverted)**: Was solved via SDL3 input migration. After reverting to Unity `Input.GetKeyDown`/`Input.GetKey`, this is no longer an issue — Unity input only fires when the game window is focused.

**Lesson**: Unity's `Input.GetKeyDown`/`Input.GetKey` inherently respects window focus, unlike `GetAsyncKeyState`.

### Title Menu SetCursor Coalesce Fix (2026-02-15)
**Problem**: Backing out of Music Player or Gallery to the extras menu briefly announced "Bestiary" (index 0) before the correct remembered item. The game fires `SetDefaultCursor(0)` then `SetCursorPositionMemory(remembered_index)` in the same frame, and the postfix announced both immediately.

**Solution**: One-frame coalesce in `TitleMenuCommandController_SetCursor_Patch`. Static fields cache pending announcement (`_pendingText`, `_pendingCommandId`, `_announcePending`). Postfix caches data and starts a `DeferredAnnounce()` coroutine only if one isn't already pending. Coroutine does `yield return null` then announces the last cached value (with dedup check). Multiple same-frame SetCursor calls overwrite the cache — only the last one is spoken.

**Behavior**: Double SetCursor (init): first caches "Bestiary", second overwrites with correct item, coroutine announces correct item next frame. Single SetCursor (navigation): ~16ms delay, imperceptible. Rapid navigation: only last position per frame announced.

### Volume Rebalancing (2026-02-15)
**Problem**: Relative volume balance was off. Footsteps too loud, wall bumps too quiet, wall tones/beacons/landing pings needed a small boost. Landing pings shared `WallToneVolumeMultipliers.BASE_VOLUME` with wall tones despite being a separate sound type.

**Changes** (in `SoundConstants.cs`):
- Footstep.VOLUME: 0.338f → 0.237f (-30%)
- WallBump.VOLUME: 0.506f → 0.759f (+50%)
- WallToneVolumeMultipliers.BASE_VOLUME: 0.12f → 0.132f (+10%)
- Beacon.MIN_VOLUME: 0.10f → 0.11f (+10%)
- Beacon.MAX_VOLUME: 0.50f → 0.55f (+10%)
- New `LandingPingVolumeMultipliers` class with `BASE_VOLUME = 0.132f` (+10%)

**Architecture change**: Landing pings now have their own `LandingPingVolumeMultipliers.BASE_VOLUME` constant, decoupled from wall tones. They still share the directional multipliers (NORTH/SOUTH/EAST/WEST) since those represent the same perceptual balance. `SoundPlayer.PlayLandingPingsLooped()` updated to use the new constant.

### Gallery Back-Button Duplicate Speech Fix (2026-02-15)
**Problem**: Pressing back from image view to the gallery list spoke the focused entry twice.

**Root cause**: Two redundant speech paths. `ChangeState` (state 2→1) launched `AnnounceGalleryReturn()` coroutine which spoke the entry, then `SetFocusContent` fired and spoke it again via `AnnouncementDeduplicator.AnnounceIfNew`. The coroutine also reset the deduplicator before speaking, so the subsequent `SetFocusContent` call wasn't suppressed.

**Fix**: Replaced the coroutine launch + `SuppressContentChange` flag with a single `AnnouncementDeduplicator.Reset(GALLERY_LIST_ENTRY)` call. This clears the dedup cache (since the entry was already announced before opening the image) and lets `SetFocusContent` handle the announcement naturally — exactly once. Deleted the `AnnounceGalleryReturn()` coroutine entirely.

### Pyramid 5F Event Loop Diagnostic Cleanup (2026-02-17)
**Context**: The Pyramid 5F event loop bug (rapid STATE_PLAYER/STATE_EVENT oscillation) was caused by a permanent Harmony trampoline on `Timer.Update` interfering with IL2CPP native event processing. The fix (dynamic patch apply/remove at runtime) lives in `TimerPatches.cs`. The investigation left ~470 lines of diagnostic code and a grace period mechanism in `GameStatePatches.cs`.

**Removed** (GameStatePatches.cs 782→311 lines):
- Grace period fields (`_lastEventExitTime`, `EVENT_GRACE_PERIOD`) and `IsInGracePeriod` property
- 15 diagnostic fields + `lastStateValue` (dead after diagnostic removal)
- 10 diagnostic Harmony hook registrations (FootEvent.RequestTriggerAction, EventProcedure.SetFlag, FootEvent.PreExcute/Excute, EventTriggerEntity.IsTriggerEnable/TriggerActivate/Suspend, FieldMapProvisionInformation.AddFootEventEntity/RemoveFootEventEntity, FootMonitoring.OnActionTriggerEvent)
- 14 diagnostic methods (all `[EventDiag]` hooks and helpers)
- 3 unused `using` directives + `UnityEngine` (no longer needed without `Time.time`)
- `RequestTriggerAction_Prefix` (grace period suppression — no longer needed)

**Simplified**:
- `IsInEventState`: was grace-period-aware getter, now `=> _cachedIsInEvent`
- `ChangeState_Postfix`: removed diagnostic logging, cycle counting, NPC position dumps
- `ResetState()`: single field reset

**Also**: InputManager.cs Ctrl+K keybinding log prefix changed from `[DIAG]` to `[Input]` (kept as intentional last-resort entity rescan).

### Job Menu "Mastered!" Always Announced (2026-02-19)
**Problem**: Every job in the job menu announced "Mastered!" regardless of actual mastery status.

**Root cause**: `view.InfoJobLevelMasterText.text` always contains "Mastered!" — the game controls visibility via `gameObject.activeInHierarchy`, not by clearing the text. The mod read `.text` directly, so the string was always non-empty.

**Fix** (JobAbilityPatches.cs ~line 215): Replaced `string masterText = view.InfoJobLevelMasterText?.text?.Trim() ?? ""` with `bool isMastered = view.InfoJobLevelMasterText?.gameObject?.activeInHierarchy == true`. Changed the branch from `!string.IsNullOrWhiteSpace(masterText)` to `if (isMastered)`.

**Lesson**: Game UI elements often have persistent text content and use GameObject visibility (`activeInHierarchy`) to show/hide. Always check visibility, not text content, for conditional UI labels.
