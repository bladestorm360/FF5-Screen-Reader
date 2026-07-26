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
- `BattleResultPatches` — Per-phase: ShowPointsInit (totals only + points page), ResultStatusUpController.SetData (level-up page, one per character), ShowGetAbilitysInit/ShowLevelUpAbilitysInit (abilities + job proficiency), ShowGetItemsInit (items), EndWaitInit (cleanup). Counter stop is `MonitorExpCounterAnimation` plus per-phase safety nets — `ShowPointsExit` is **not** patched.
- `ItemMenuPatches` — Item menu + ItemMenuState (shared announce helpers) + ItemMenuTracker + ItemUseTracker + FieldItemReannouncePatches
- `EquipMenuPatches` — Equipment command bar / slot list / item select + EquipMenuState + FieldEquipReannouncePatches
- `ItemDetailsAnnouncer` — I key equipment compatibility
- `JobAbilityPatches` — Job/ability menus + JobAbilityTrackerHelper + FieldJobAbilityReannouncePatches
- `FieldMenuPatches` — Field (main) menu initial focus: Show + InitNone
- `SaveListPatches` — Save/load slot initial focus: SaveListController.SetActive
- `StatusMenuFocusPatches` — Status character-select initial focus: ListInit / SelectInit
- `ConfigMenuPatches` — Config menu
- `MovementSpeechPatches` — Movement announcements + vehicle state transitions
- `MovementSoundPatches` — Footstep audio
- `ShopPatches` — Shop menus + ShopMenuTracker + equipment command bar
- `PopupPatches` — All popup types (common, game over, info, job change, change name)
- `GameStatePatches` — BattleState, map transitions, IsInEventState (cached via ChangeState hook), config menu bestiary detection (states 17/18)
- `TitleMenuPatches` — Title screen
- `DashFlagPatches` — Walk/run state for F1
- `MainMenuPatches` — In-game main menu + state cleanup
- `SaveLoadPatches` — Save/load menus + confirmation popups
- `NamingPatches` — Name entry screen
- `BestiaryPatches` — Bestiary (Picture Book) accessibility: state tracking, list nav, detail stats, formations, maps, config menu bestiary (ConfigBestiaryStateHandler)
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
- `KeyHelpReader` — Shift+I key help tooltip reader (GameObjectCache + unsafe view pointer + Text components)

### Utils (`Utils/`)
- `TolkWrapper` — NVDA interface
- `CoroutineManager` — Managed coroutines, self-cleanup, max 20, StartUntracked/StopManaged
- `SoundPlayer` — WaveOut audio playback (5 channels, P/Invoke winmm, hardware looping)
- `ToneGenerator` — Tone generation + WriteWavHeader
- `GameConstants` — Audio, tile size, direction vectors, map IDs
- `GameObjectCache` — Cached lookups (Get/Refresh pattern)
- `TextUtils`, `CollectionHelper`, `DirectionHelper`, `PlayerPositionHelper`
- `MenuFocusAnnouncer` — Initial-focus reads: frame-bounded settle coroutine + generation latch, `IsMenuOpen()` / `IsAlive()` gates
- `UnityHelpers` — `IsControllerActive()` null-safe `activeInHierarchy` check
- `LocalizationHelper` — MessageManager wrapper + 12-language mod string dictionary
- `BattleResultDataStore` — Static data store for navigator (points + stats)
- `BattleUnitHelper`, `CharacterStatusHelper`, `SelectContentHelper`
- `EntityTranslator` — JSON-based Japanese→English name translation (4-tier lookup) + nested `EntityDump` (key 0)
- `AudioChannel` — WaveOut backend (winmm P/Invoke, per-channel waveOut handles)
- `WindowsFocusHelper` — Win32 focus stealing for mod windows
- `VKConstants` (in ConfirmationDialog/TextInputWindow) — VK constant definitions for Win32 key input

## Key Game Namespaces

**Il2CppLast.*** — Battle, Map (MapManager, FieldController, FieldMap), Entity.Field, UI (Cursor), UI.KeyInput/Touch, UI.Message, Data.Master, Data.User, Defaine

**Il2CppSerial.FF5.*** — UI.KeyInput (AbilityContentListController, AbilityCommandController, AbilityChangeController, AbilityUseContentListController, JobChangeWindowController, BattleQuantityAbilityInfomationController, ResultStatusUpController, ResultPointController)

⚠ **Every one of these has a `UI.Touch` twin, and on PC the Touch twin is often an empty stub.** `Serial.FF5.UI.Touch.ResultStatusUpController` (dump.cs:284064) has no fields and no `SetData` at all; the real one is `Serial.FF5.UI.KeyInput.ResultStatusUpController` (dump.cs:288887). Patch the **KeyInput** variant, and prefer a typed `[HarmonyPatch(typeof(...))]` over reflection/`FindType` so a wrong target is a build error instead of a silent no-op.

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
- **Initial focus**: `MenuFocusAnnouncer.Request(tag, () => TryAnnounceX())`
- **Content access**: `SelectContentHelper.TryGetItem(list, index)`
- **FieldController access**: `GameObjectCache.Get<FieldMap>()?.fieldController` (NOT direct Get<FieldController>)

---

## No-Dedup Policy

There is **no central deduplicator** (matching FF1, which has none either). `AnnouncementDeduplicator`
and `AnnouncementContexts` were deleted; 105 call sites became ~20 local guards.

**When speech doubles, fix the redundant call — do not add a dedup net.** Decision order:

1. **Does a once-per-event method exist?** Hook that; delete the per-frame patch.
2. **Two patches announcing the same fact?** Delete one; the loser updates state only.
   (`LibraryMenuController.Show` now caches `CurrentMonsterData` and stays silent — `OnContentSelected`
   is the sole announcer.)
3. **Same-frame open double?** Generation latch (`MenuFocusAnnouncer._gen`), never text comparison.
4. **Right strings, wrong order?** One-shot suppression bool, cleared in `finally`
   (`SuppressContentChange`, `SuppressNextListEntry`).
5. **Phase/state overlap?** State gate (`EnteredEquipmentFromShop`, `IsInShopSession`).
6. **Only if 1–5 fail:** a local static in that patch class, with a comment naming what re-fires.

**Surviving local guards** (each is a genuine same-event re-fire, not deduplication):
scroll-view `SelectContent`/`SetCursor` carrying a `WithinRangeType` (item list, equip panes, job,
ability, spell list, config remap rows) · `BattleCommandSelectController.SetCommandData`/`SetCursor`
(re-invoked on cancel-back from targeting) · `BattleTargetPatches` `TargetMode` enum + index (replaced
four contexts and their cross-resets) · `ConfigActualDetails*` slider pair — *the check IS the logic*,
not a guard · config arrow value (fires at range ends where nothing changes) ·
`ParameterActFunctionManagment` `_lastActDataPtr` — **pointer** identity so two goblins attacking both
announce · `BattleConditionController` `_lastCondition` (per-target + persistent re-application) ·
`GameStatePatches._lastAnnouncedMapId` — guards **side effects**, not just speech (one door transition
invokes `CheckMapTransition` 3–4×). Battle-scoped guards are cleared each turn by
`BattleMenuController.SetCommandSelectTarget`; result-phase one-shots by `ShowPointsInit`.

## Initial-Focus Announcements

Navigation fires on cursor movement; the game's **initial cursor placement fires nothing**, so menu
entry and return-from-submenu were silent.

`Utils/MenuFocusAnnouncer.cs` — `Request(tag, Func<bool> tryAnnounce)` starts a **frame-bounded settle
coroutine** (`MaxSettleFrames`, 6) that calls `tryAnnounce` once per frame until it returns true. The
cap is a **timeout, not a delay** — a ready menu costs exactly one frame. It was 30 (~0.5s); at that
size a slow read still fired long after the cursor had moved, which reads as lag. A read needing more
than 2 frames logs `initial focus read took N frames`, and a timeout logs `gave up after N frames` —
if either shows up repeatedly the menu is gated on the wrong readiness signal (usually `IsMenuOpen()`
on a screen that is not a MenuManager menu), which is the thing to fix rather than raising the cap.

Not a poll
and not a timer: no standing per-frame Harmony hooks (Rule 2), budget in `yield return null` frames
rather than `WaitForSeconds` (Rule 3). Same shape as the existing Gallery/MusicPlayer entry coroutines,
tightened from seconds to frames. `yield` sits **outside** the `try` (yield-in-try-with-catch is illegal).

A single global generation latch (`_gen`) collapses the `Show`+`InitNone` double on open (later wins)
and caps concurrent settle coroutines at ~1 — important given `CoroutineManager`'s 20-coroutine limit
with oldest-first eviction. `Cancel()` on menu close drops pending reads.

**`tryAnnounce` returns true when it SPOKE**, false while data isn't ready (which is what drives the
retry). Each menu's helper is shared with its navigation postfix so both produce the same string; the
state-entry hook clears that helper's local guard first, so re-entry on the same row always speaks.

**Structural separation is what prevents doubles** — navigation hooks (`SelectContent`/`SetCursor`/
`SetFocus`) never fire on state entry, and the `*Init` hooks never fire on cursor movement.

| Screen | State-entry hook | Cursor source |
|---|---|---|
| Field menu | `KeyInput.MainMenuController.Show(bool)` + `InitNone` | `commandMenuController.selectCursor` (0x38) |
| Item list | `ItemListController` `CommandSelectInit`/`UseSelectInit`/`ImportantSelectInit`/`OrganizeSelectInit`/`SortSelectInit` | `selectCursor` 0x60, `dataList` 0x78 |
| Item-use target | `ItemUseController.SingleInit`/`AllInit` | `contentList` 0x40, `selectCursor` 0x50 |
| Equip (3 panes) | `EquipmentWindowController.CommandInit`/`InfoInit`/`SelectInit` | each pane's own `selectCursor` |
| Job change | `JobChangeWindowController.SelectJobInit` | `jobSelectCursor` 0x40 (base) |
| Ability command / spell / target | `AbilityWindowController.CommandInit`/`UseListInit`/`UseTargetInit` (+ `AbilityUseContentListController.SingleInit`/`AllInit`) | each sub-controller's `selectCursor` |
| Ability equip | `AbilityChangeController.SelectCommandInit`/`SelectListInit` | **no cursor field** — cached index from the nav postfix, default 0 |
| Status char-select | `StatusWindowController.ListInit`/`SelectInit` | `selectCursor` 0x40 (base), `contentList` 0xC8 |
| Save/load slots | `KeyInput.SaveListController.SetActive(bool,bool,bool)` | `selectCursor` 0x58 |
| Title Options list | `KeyInput.TitleWindowController.InitializeOption` | `commandController` 0x50 → `selectCursor` 0x30, `activeContents` 0x28 |

> **Two different screens, easily confused.** The title **Options list** (Config / Privacy Policy / …)
> is a `TitleWindowController` command list driven by `TitleMenuCommandController`. `OptionController`
> is the **settings** screen you reach after choosing Config inside it. Hooking `OptionController`
> does nothing for the Options list — the patch applies and simply never fires.
>
> **Touch vs KeyInput naming differs here.** The KeyInput controller uses `InitializeOption` /
> `InitializeExtra`; the Touch variant uses `InitOption`. `dump.cs` lists the Touch class first, so
> reading the wrong one yields a name that patches cleanly and never fires. Confirm the namespace in
> `script.json` (`Last.UI.KeyInput.*` vs `Last.UI.Touch.*`) before hooking.

**Gates.** Field-menu-family helpers gate on `MenuFocusAnnouncer.IsMenuOpen()` (`MenuManager.IsOpen`,
reliably false during a map/asset load — this is what keeps scene construction silent). Equipment
accepts `IsMenuOpen() || ShopMenuTracker.IsInShopSession` because that window is shared by the field
menu and the shop. Item/ability target readers bail on `BattleState.IsInBattle`. `SaveListPatches`
uses `ShouldReadSaveSlot()` = `IsMenuOpen() || LoadGameWindowController active`, which excludes the
**background autosave** whose `SaveListController` is briefly active during a map load.

### Never patch a folded empty-body stub (launch crash)

IL2CPP folds **every empty method body in the game onto one shared native address**. In FF5 that is
`2561440`, backing **4398** methods. Patching it installs a detour on all of them at once and hard
crashes during `OnInitializeMelon` — MelonLoader logs nothing after the previous patch line, so the
symptom is a silent truncated `Latest.log`, not an exception.

This bit `EquipmentWindowController.NoneInit`, `AbilityChangeController.NoneInit` (both 2561440) and
`JobChangeWindowController.NoneInit` (4886656, shared with 1 other). All three are now unpatched.
Same failure FF1 records for `OptionController.UpdateSelectLanguage`.

**Check before hooking any `*Init` / empty-looking method** — `dump.cs` shows `{ }` for every body,
so it cannot tell you; use `script.json`, where a duplicated `"Address"` means a folded stub:

```
python -c "import re,io,collections; addr=None; c=collections.Counter(); rows=[]
for l in io.open('script.json',encoding='utf-8'):
    m=re.search(r'\"Address\": (\d+)',l)
    if m: addr=int(m.group(1)); continue
    m=re.search(r'\"Name\": \"(.+?)\"',l)
    if m and addr is not None: c[addr]+=1; rows.append((addr,m.group(1))); addr=None
print([ (a,n) for a,n in rows if 'YourController' in n and 'Init' in n and c[a]>1 ])"
```

Verified-unique (safe) exit hooks in use: `AbilityWindowController.NonInit` (10203056),
`StatusWindowController.NonInit` (6570528), `MainMenuController.Close` (6928816).

The Options list has no `MenuManager` gate — the list being populated is the readiness signal — and
it announces through `TitleMenuCommandController_SetCursor_Patch.Queue` rather than speaking
directly, so a `SetCursor` in the same frame coalesces instead of doubling.

**The Options list is the ONLY title list hooked.** `InitSelect` (main title list) is not: the game
already fires `SetCursor` there, both on first appearance and on back-out from a sub-list. Hooking it
produced a second announcement one frame later — `MenuFocusAnnouncer` yields a frame before reading,
which puts it outside the one-frame coalesce window, so "Load Game" and "Options" were each spoken
twice. `InitializeExtra` is not hooked either: the Extras list already announced correctly.
Diagnosis: identical text ~17 ms apart (one frame) in the speech log means two coroutines, not one.

**Deliberately NOT hooked:** the main menu's `CommandMenuController.SetFocus` (deleted — the game
re-asserts it on nav echo / confirm / return, indistinguishable from its arguments; navigation is
owned by the generic cursor reader and entry by `FieldMenuPatches`); `TitleWindowController.InitSelect`
(see above); **all** config settings screens — the in-game `ConfigController.InitializeSelect` and the
title-screen `OptionController.ShowConfig` / `InitializeConfigList` both already announce via
`ConfigCommandController.SetFocus`, and an entry hook there was worse than a plain duplicate: clearing
the `SetFocus` guard let the game's own second `SetFocus` through, so entering Config spoke
"Language: English" twice; and the shop
(`ShopCommandMenuController.SetCursor`, `ShopListItemContentController.SetFocus` already fire on
entry); title, battle, bestiary, music player and gallery, which already announce.

### Language row reads blank

The Language config row's dropdown label is **empty for the currently-selected language** — the game
blanks the current item (its native name is a sprite). Reading the dropdown therefore yields nothing
and the row announced as just "Language". `ConfigMenuReader.GetCurrentLanguageDisplayName()` maps
`MessageManager.currentLanguage` (FF5 `Language` enum, `Ja=1 … Pt=12`) through a self-contained
table instead; never use the game's `GetLanguageMessage`, which returns empty for the current language.

The row is identified by `ConfigCommandsData.ConfigCommandType == Language` **or** by
`LanguageContentData != null` — `ConfigCommandsData` is a separate MonoBehaviour reference that is
not always wired up (notably on the title-screen Options list), which is what made the row fall
through to the dropdown branch. A blank-label dropdown row also falls back to the current language.

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

**Status**: Superseded 2026-07-26 — the `Serial.FF5.UI.Touch` target was wrong and the status-up hook never fired. See below.

### Multi-Phase Battle Results: Level-Up Page (2026-07-26)

**The bug**: the level-up screen was completely silent. `PatchStatusUpSetData` targeted `Il2CppSerial.FF5.UI.Touch.ResultStatusUpController` — an empty stub with no `SetData` (dump.cs:284064). The postfix never ran; `"ResultStatusUpController.SetData fired"` appeared in no log. Everything downstream (`ExtractStatDiffs`, `AnnounceStatusUpCoroutine`, the navigator's stats grid) was dead code, and level-ups were being announced on page 1 next to Gil as a workaround.

**Fix**: typed `[HarmonyPatch]` on `Il2CppSerial.FF5.UI.KeyInput.ResultStatusUpController.SetData` (dump.cs:288887, RVA 0x4B0E60). Called once per character page from `ResultPointController.StatusUpInit` and again from `StatusUpAction` as the player presses A.

**Structural row reading** replaces the old flat-text triple heuristic. `ResultStatusUpController` exposes `view.nameText` and `contentList` (`List<Last.UI.KeyInput.ResultStatusContentController>`, dump.cs:475389). Each row carries its own `Type` (`ParameterType`: Level=1, HP=2, MP=3, JobLevel=-1) and a `ResultStatusContentView` (dump.cs:475425) with `categoryText` / `jobLevelText` / `beforValueText` / `afterValueText` / `arrowImage` / `upperJobMasterText` / `lowerJobMasterText`. Reading the row objects means labels ("Lv.", "HP", job name, "Master") come out localized with no lookup table. `ActiveText()` guards each read on `gameObject.activeInHierarchy` — the panel hides values rather than blanking them, so an inactive Text still holds the previous character's text. Output: `"Lenna: Level up! Lv. 2 to 3, HP 44 to 53, MP 9 to 15, Freelancer"`.

**Result-screen page map** (`ResultMenuController.State`, dump.cs:475011 — `None/ShowPoints/SkillLevels/ShowStatusUp/ShowGetItems/ShowGetAbilitys/ShowLevelUpAbilitys/EndWait`). `ShowLevelUpAbilitys` dispatches through `IsToAbilityLevelUp()` → `ResultSkillController.ShowLevelUp` and `IsToJobProficiencyLevelUp()` → `ResultSkillController.ShowJobProficiencyLevelUp` (per-row: `ResultLevelUpContentController.SetJobProficiencyData`, `ResultLevelUpContentView.SetJobProficiencyLevelUpText`). Job level up is therefore its **own page**, currently covered only by the raw text scrape; a diagnostic logs the full unfiltered scrape so a typed hook can be written once jobs are unlocked.

**FF5 has no weapon/spell proficiency system.** `IsWeaponSkillLevelUp()`, `SkillLevelTarget`, `GrouthWeaponSkillList` and `SetWeaponSkillLevelUpText` are inert FF2-era FFPR engine members. Their presence in dump.cs is not evidence of an FF5 feature — do not wire them up.

**Compact row summaries (2026-07-26).** `ResultPage` carries an optional `RowSummaries[]`; when present `BuildFullRowText` speaks it verbatim instead of joining every cell to its column header. Stat rows read `"HP: 44 > 53 (9)"` rather than `"HP, 44 Before, 53 After, +9 Change"`; the job row reads as just `"Freelancer"`. Columns stay browsable with Left/Right, where naming them is still useful. The `Change` *cell* keeps an explicit `+9` because it can be read in isolation; the summary uses the bare `(9)`. The announcement uses the same `>` separator with no deltas. Both go through `Transition()` in `BattleResultPatches`, so swapping `>` for `GetModString("to")` (if NVDA's punctuation level swallows it) is a one-constant change.

**Navigator is now multi-page.** `BattleResultDataStore` holds an ordered `List<ResultPage>` (`Title`, `RowHeaders`, `ColHeaders`, `Cells`, optional `RowSummaries`) instead of two mutually-exclusive blobs. Pages append as their phase fires: points, one per levelling character, abilities, items. A page with zero columns is a flat list. `Open()` starts on the last page added (the one on screen); PageUp/PageDown or L1/R1 cycles. This fixes the old behaviour where any level-up made the EXP/Next/ABP page permanently unreachable and flattened every character into one row list.

**No dedup added** — per standing project rule. If a character ever speaks twice, `SetData` is the wrong call site and the hook moves; the duplicate is diagnostic, not something to filter.

**Status**: Builds clean. Needs in-game verification (see plan verification steps).

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

### Offline Mass Entity-Label Extraction (2026-07-04)
**Feature**: `tools/extract_entities.py` (Python + UnityPy) dumps every translatable entity label across all 238 maps at once, replacing the per-map `EntityDump` (key 0) walk. Output `translation.generated.json` (mod root) is the flat `{ rawJpName: { lang: value } }` shape `EntityTranslator` already consumes. First run yielded 1367 unique labels (Event 364, Entity 421, NPC 385, AnimEntity 195, ShopNPC 12, RandomEvent 14, +minor). Re-runnable on game updates; merges without clobbering filled values.

**Status**: fully translated and shipped. All 1367 labels are translated into all 12 localization languages (`ja` identity + en/fr/it/de/es/ko/zht/zhc/ru/th/pt) and promoted into the embedded `translation.json` (674 KB, embedded via `FFV_ScreenReader.csproj:83`). `en` is the runtime fallback for any missing language (`EntityTranslator.TryLookup`). Pipeline for regenerating after a game update: `extract_entities.py full translation.generated.json` → split keys into chunks → translate chunks → `tools/apply_translations.py apply <batch>` per batch → `apply_translations.py promote` (writes `translation.json`, sets `ja` = key). Note some `Entity(5)` keys are dev-internal (collision/purpose markers) and `【汎用】`-prefixed generics slip past `EntityFactory.IsPlaceholderEntity` (only bare `汎用` is caught) — prune or tighten later if desired.

**Why offline (data architecture)**: FFPR splits data into two tiers. *Global tier* (always resident): master tables (`MasterManager.GetList<T>()` — `Map`, `Area`, `Transportation`, …) and the `MessageManager` text dictionary. *Per-map tier* (only live while a map is loaded): entity placements/labels. NPC/event/interactible labels are **not** in any master table — they are literal `MapObjectProperty.name` strings authored inside each map's Addressables bundle (`StreamingAssets/aa/StandaloneWindows64/map_*.bundle`) in **Tiled-editor JSON** (`layers[].objects[]`, each object with `name` + a `properties[]` list holding `object_type`). Entity groups live as `entity_default` TextAssets plus base64 `inline` `entity[]` groups in the bundle's `package` manifest. That is why they feel "runtime only": the game only `JsonUtility.FromJson`-parses a map's package when you enter it. The `Npc` master exists but its `npc_name` is unconsumed and `GetList<Npc>` is not AOT-instantiated, so it is not a usable source. The two categories that ARE globally tabled (and need no dump): **map exit names** (`PropertyGotoMap.MapId` → `Map`/`Area` master → `MessageManager`, done in `MapNameResolver.cs`) and **vehicle tags** (`Transportation` master `message_id` → `MessageManager`). Treasure is a mod constant. An in-mod sweep was rejected because `dump.cs` is signature-only, so the addressable-key/loader-wiring conventions aren't recoverable without live reverse-engineering — offline reads the same JSON straight from the bundles. See `tools/extract_entities.py` header for the filter (mirrors `EntityFactory` object_type excludes + Japanese-char + placeholder filters).

### Battle Target Status Effects (2026-02-12)
**Feature**: Target selection now shows status conditions after HP/MP. Player: "Name: HP x/y. MP x/y. status: Poison, Blind". Enemy: "Name: HP x/y. status: Poison". Reuses `CharacterStatusHelper.GetStatusConditions()` (same whitelist, localized names, fallback names).

### Naming Popup Enhancements (2026-02-12)
1. **CommonPopup initial button**: Read `selectCursor` at offset 0x68, get button text via ReadButtonFromCommandList. Wrapped in try/catch. Result: "Bartz. Use this name? Yes"
2. **ChangeNamePopup hint**: Append `LocalizationHelper.GetModString("default_name_hint")` (12 languages). Result: "Enter a name for Bartz. Press Enter for default name"

### Key Help IL2CPP Cast Constraint Fix (2026-02-20)
**Problem**: Shift+I (`AnnounceKeyHelp`) crashed with `Il2CppObjectBase.Cast: type argument 'T' violates the constraint of type parameter 'T'` when `KeyHelpController` instances were active.

**Root cause**: `FindObjectsOfType<KeyHelpController>()` (plural) and `GetComponentsInChildren<KeyIconController>()` return IL2CPP arrays whose element indexers invoke `Cast<T>()`, which fails for game-specific types (`KeyHelpController`, `KeyIconController`, `KeyIconView`).

**Solution**: Rewrote `KeyHelpReader.cs` to avoid all Cast-triggering array access on game-specific types:
1. `GameObjectCache.Get/Refresh<KeyHelpController>()` — single typed instance, no array indexer (same pattern as `AnnounceConfigTooltip`)
2. Unsafe pointer read at offset 0x18 for private `view` field (`KeyHelpView`) — no public accessor
3. `view.ContentsParent` (public property) → `transform` → iterate children by index
4. `activeInHierarchy` check on each child — game deactivates entries on other pages, so this filters to visible page only
5. `GetComponentsInChildren<Text>(false)` per active child — `Text` is standard Unity type, no Cast issues (same pattern as `KeyboardGamepadReader.cs:74`)

**Lesson**: IL2CPP `Cast<T>()` fails for certain game-specific types when accessed via array indexers (`FindObjectsOfType<T>()[i]`, `GetComponentsInChildren<T>()[j]`). Avoid by using singular `FindObjectOfType<T>()` (via `GameObjectCache`), transform hierarchy navigation, and only calling `GetComponentsInChildren` on standard Unity types like `Text`.

### Music Player Duration Fix (2026-02-14)
**Problem**: All song durations showed "0:00". `LookupDuration()` correctly returned `entry.Time` (e.g., 153 for "Main Theme of Final Fantasy V" = 2:33), but `FormatPlayTime()` divided by 1000 assuming milliseconds. Integer division `153 / 1000 = 0`.

**Fix**: `SoundPlayerList.Time` is in seconds, not milliseconds. Removed the `/ 1000` division in `FormatPlayTime()`. Also cleaned up one-shot diagnostic logging (`_loggedDiagnostics`, `shouldLog`) that was no longer needed.

**Lesson**: `SoundPlayerList.Time` returns seconds. The IL2CPP dump annotation `playTime` on `ExtraSoundListContentInfo` is misleading — it's not milliseconds.

### Save Complete Popup Announcement (2026-02-13)
**Problem**: "Save Complete" popup text never spoken after Quick Save or Normal Save. Only "Close" button was read.

**Root cause**: Completion text lives on `savePopup` (0x38), not `commonPopup` (0x28). The original `DelayedSaveCompleteRead` coroutine read from `commonPopup` and got nothing. Meanwhile, `SavePopup.UpdateCommand` fires on the completion popup (reuses same `SavePopup` instance), but `lastPopupButtonIndex` was still set from the confirmation dialog — so the "first call" path (which reads title+message via `DelayedSavePopupRead`) was skipped, and only the button text ("Close") was announced.

**Solution**: Reset `lastPopupButtonIndex = -1` in both `InterruptionInitComplite_Postfix` (Quick Save) and `SaveWindowCompleteInit_Postfix` (Normal Save). This causes the next `SavePopup.UpdateCommand` call to treat it as a fresh popup, triggering `DelayedSavePopupRead` which reads title+message from the `savePopup`'s own text fields (0x38/0x40). Removed the old `DelayedSaveCompleteRead` calls that read from the wrong popup.

**Lesson**: When a popup reuses the same `SavePopup` instance for different screens (confirmation → completion), the dedup index must be reset at each transition so the first-call path re-triggers.

⚠ **This entry was wrong for Quick Save until 2026-07-26.** The two postfixes were written but **never registered with Harmony** — `ApplyPatches` only ever patched `SetEnablePopup`. Normal Save worked anyway (for a different reason, below); Quick Save stayed broken for five months while this write-up claimed otherwise. See the next entry.

### Quick Save Completion Popup Was Silent — Orphaned Postfix (2026-07-26)

**Problem**: after confirming Quick Save, the completion popup spoke only "Close". Normal Save read its completion popup correctly.

**Root cause**: `InterruptionInitComplite_Postfix` existed and was correct, but `TryPatchInterruptionController` never registered it — it patched only `SetEnablePopup`. `git log -S` confirms `ApplyPatches` never contained the registration.

Normal Save was never relying on `SaveWindowCompleteInit_Postfix` either. It works because `SaveWindowController` (dump.cs:470079) has a five-state machine `None/Select/Save/popup/Complete` with a real `PopupExit()`, so the popup is torn down and re-shown across `popup → Complete` and `SetPopupActive` fires a second time, resetting the flag via `SaveWindowSetPopupActive_Postfix`.

`InterruptionWindowController` (dump.cs:465200) has only `None/Confirmation/Complite` and **no `*Exit` methods at all**. It reuses one `SavePopup` instance across the transition, so `SetEnablePopup` fires exactly once and the flag stays stale from the confirmation.

**Fix**: register the existing postfix on `InitComplite` (note the game's spelling; private, dump.cs:465262, RVA 0x802830). Deliberately did **not** register `SaveWindowController.CompleteInit` — Normal Save already resets via `SetPopupActive`, and a second reset landing after the first-call read would double-announce. Deleted `SaveWindowCompleteInit_Postfix`.

**Also fixed: `SaveLoadMenuState.IsActive` leaked after every Quick Save.** `InterruptionSetEnablePopup_Prefix` sets it, but the postfix's `isEnable == false` branch cleared only `IsInConfirmation`, and Quick Save has no `SetActive` hook to call `ResetState()` the way the other three save/load windows do. It stayed true until the next `MainMenuController.Show`, and while true every `Cursor.*Index` patch returns early — cursor movement in the main menu was silent. Now cleared by a postfix on `InterruptionWindowController.Close()` (dump.cs:465232, RVA 0x802400).

**Lessons**:
- A `TryPatch*` helper that resolves a method and silently skips registration is invisible at runtime. Every branch now logs a warning naming the user-visible consequence.
- Writing the fix and documenting it is not the same as wiring it up. When a doc entry says "solution: X", confirm X is actually registered.
- `SaveLoadMenuState.IsActive` gates the cursor patches, so leaking it produces *silence*, not noise — the failure mode nobody notices. Any prefix that sets it needs a matching teardown hook.
- **Stale comments**: several prefixes here claimed to "suppress `PopupOpen_Postfix`". That postfix only checks `IsShopActive()` and never fires for save popups anyway, since `SavePopup` derives from `MonoBehaviour`, not `Il2CppLast.UI.Popup` (dump.cs:469928). Comments corrected. Note `PopupPatches.cs`'s `SaveLoadMenuState.IsActive = true` in the game-over load handler carried the same wrong rationale but is genuinely load-bearing — it stops the generic cursor reader double-reading buttons `GameOverLoadPopup.UpdateCommand` already handles.

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

### Config Menu Bestiary Support (2026-02-20)
**Problem**: Bestiary worked from extras menu (title screen) but not from config menu (in-game). The config menu uses `SubSceneManagerMainGame.ChangeState` with states 17 (MenuLibraryUi = list) and 18 (MenuLibraryInfo = detail), not `SubSceneManagerExtraLibrary.ChangeState`. Both paths share the same UI controllers (`LibraryMenuController_KeyInput`, `LibraryInfoController_KeyInput`), so existing patches on those controllers already fired — they just bailed because `BestiaryStateTracker.CurrentState` was never set.

**Solution**: `ConfigBestiaryStateHandler` (new internal static class in `BestiaryPatches.cs`) maps MainGame states 17→1 (list) and 18→4 (detail) in `BestiaryStateTracker`. Called from `GameStatePatches.ChangeState_Postfix` which already hooks `SubSceneManagerMainGame.ChangeState`. On entry (state 17, previousState ≤ 0): resets tracker, suppresses initial entry, reuses `AnnounceListOpen()` (changed to `internal static`). On return from detail (state 17, previousState = 4): re-announces current entry. On exit (state changes away from 17/18): calls `ClearState()`. `WasInConfigBestiary` flag prevents false exit triggers during normal gameplay.

**Additional patch**: `MenuExtraLibraryInfo_OnChangedMonster_Patch` — config menu's `MenuExtraLibraryInfo.OnChangedMonster(MonsterData)` has a different RVA (0x3CFBA0) than `ExtraLibraryInfo.OnChangedMonster` (0x5C1FE0), so needs its own HarmonyPatch. Same logic as existing extras patch.

**No changes needed** to: `BestiaryReader`, `BestiaryNavigationReader`, `InputManager`, page turn patches (same RVA, shared), minimap/UpdateController patch, full map/formation patches (config menu doesn't have these).

**Lesson**: When two menu paths share the same UI controllers but use different state machines, the existing controller patches will fire for both — they just need the state tracker to be set. Map the new state machine's values to the existing tracker states rather than creating parallel tracking.

### Status Navigator Group Heading Announcements (2026-02-20)
**Feature**: Shift+Up/Down group jumps now prepend the group name (e.g. "Vitals. HP: 450 / 450") so the user knows which group they've landed in. Normal arrow key navigation is unchanged.

**Implementation** (StatusDetailsReader.cs):
- `GetGroupDisplayName(StatGroup)` — maps enum to human-readable string (CharacterInfo→"Character Info", Vitals→"Vitals", Attributes→"Attributes", CombatStats→"Combat Stats", Progression→"Progression")
- `ReadCurrentStatWithGroup()` — same flow as `ReadCurrentStat()` but formats as `"{groupName}. {statValue}"`
- `JumpToNextGroup()` and `JumpToPreviousGroup()` — changed from `ReadCurrentStat()` to `ReadCurrentStatWithGroup()`

**Pattern**: Ported from `BestiaryNavigationReader` which already prepends group names on Shift+arrow jumps.

### Auto Detail F7, Right Stick Up/Left, and I/U Restructure (2026-07-25)

**Feature**: F7 toggles Auto Detail with a spoken confirmation (default flipped to on). Right stick up mirrors the `I` key, right stick left mirrors a new `U` key. `I` now reads *descriptions* in every menu (matching FF1/FF4), and the "which jobs can equip this" readout moved to `U` — which also works in shops for the first time.

**Equip lookup must use master data, not OwnedItemData.** The old `ItemDetailsAnnouncer` resolved jobs via `UserDataManager.SearchOwnedItem(contentId)` → `EquipUtility.CanEquipped(ownedItemData, jobId)`. `SearchOwnedItem` returns **null for items the player does not own**, so that path can never work in a shop. The rewrite goes through master data instead:

`Content.TypeId` (2=weapon, 3=armor) + `Content.TypeValue` → `Weapon`/`Armor.EquipJobGroupId` → `MasterManager.GetData<JobGroup>(id)` → for each `UserDataManager.ReleasedJobs` entry, `EquipUtility.CanEquipped(jobGroup, job.Id)`.

**Use `EquipUtility.CanEquipped(JobGroup, int)`** (dump.cs:394447). FF1 hand-rolls the `JobGroup.Job1Accept…Job22Accept` columns and assumes `jobId == arrayIndex + 1`; FF5 exposes the game's own predicate, which removes both the column scan and that assumption. Do not port FF1's version here.

**IL2CPP dictionary iteration is fine.** `foreach (var kvp in dict)` over an `Il2CppSystem.Collections.Generic.Dictionary` works (see `FieldNavigationHelper.cs:149`). The pointer-offset technique documented elsewhere is for *reaching* a dictionary stored in a private field at a known offset — not for iterating one you already hold. `MasterManager.GetList<Content>()` returns the dictionary directly, so it can be iterated normally.

**Shop item resolution** (`Menus/UsableByAnnouncer.cs`): shops give a display name and a `ShopListItemContentController.ContentId`, but whether that id is a `Content` primary key or a type-local id is unverified. The resolver tries the keyed lookup first and **validates the resolved name against the displayed name**; on mismatch it falls back to a linear scan of the `Content` table by localized name. Wrong data is never announced — worst case it stays silent. The scan only runs on an explicit key press.

**F-key gating rule** — this is the distinction that governs which predicate to use:
- `F1` (walk/run) and `F3` (encounters) are *the game's own* hotkeys. The mod polls them without consuming and merely narrates the resulting state. Their gate must **mirror the game's** availability (`IsOnValidMap()` alone) and must never be tightened, or the mod goes silent while the game still acts.
- `F5`, `F7`, `F8` are **mod-owned**, so the mod picks the gate. `F5`/`F8` use `InputManager.IsFieldOrFieldMenuActive()` (live map, including field menus; never battle or title screen). `F7` is deliberately context-free.

`IsFieldOrFieldMenuActive()` is deliberately **not** `ControllerRouter.IsFieldActive` — the latter additionally excludes menus because it also drives audio suppression, the entity scanner, and mod-mode teleport. Changing `IsFieldActive` to widen F8 would have broken field navigation. The F8 gate and the Start-button gate (`ControllerRouter.HandleStateTransitions`) must always be changed together.

**Right stick needs no `ConsumeButton()`.** The right-stick *axes* have no `InputActionType` mapping in `InputPassthroughPatches` (only buttons, d-pad, left stick and triggers do), so the game can never see them.

### Items Menu Announced First Inventory Row Instead of Command Bar (2026-07-25)

**Symptom**: Entering the Items menu spoke the first inventory item rather than the focused command-bar option (Use / Key Items / Sort). The equip menu was correct.

**Cause**: `FieldItemReannouncePatches.ApplyPatches` hooked `ItemListController.CommandSelectInit` alongside the four list states (`UseSelectInit`, `ImportantSelectInit`, `OrganizeSelectInit`, `SortSelectInit`) and routed all five to the same `ItemList_Init_Postfix` → `TryAnnounceItemListInitial`, which reads `controller.dataList` + `selectCursor` — the **inventory list**. `CommandSelectInit` is the command bar, not a list, so entry announced the wrong thing. The in-code comment even identified it as the command bar; the handler just didn't distinguish it.

**Fix**: removed `CommandSelectInit` from the `ItemListController` loop and hooked `ItemWindowController.CommandSelectInit` instead — that is the controller owning `commandController`. New `ItemMenuState.AnnounceItemCommand` reads `ItemCommandController.contentList[index].Data.Name`, mirroring `EquipMenuState.AnnounceEquipCommand`.

**Structure** (all `Last.UI.KeyInput`):
- `ItemWindowController.commandController` → `ItemCommandController` @ 0x38
- `ItemCommandController.contentList` (`List<ItemCommandContentView>`) @ 0x40, `selectCursor` @ 0x50
- `ItemCommandContentView.Data` → `ItemCommadContentData.Name` (note the game's typo: *ItemCommad*)

**Why the window controller, not the list controller**: this matches the equip menu, where `EquipmentWindowController.CommandInit` reaches through to its own `commandController`. Both `ItemWindowController` and `ItemListController` have the same `*SelectInit` state names — the window's state machine drives the list's — so hooking the same state on both would double-fire. The four list states stay on `ItemListController`; only the command state moved.

**Command-bar navigation was never broken**: the generic cursor announcer (`CursorNavigationPatches`) already reads it, since `list_window` is in the exclusion list but the item command bar is not. Only the initial-focus path needed fixing.

**Folded-stub check performed** (per the `EquipMenuPatches` NoneInit warning): `ItemWindowController.CommandSelectInit` is RVA `0xA235E0`, unique across the whole dump — a real body, safe to patch. This build's folded empty-method addresses are `0x1D80` (21302 methods), `0x2715A0` (2671), `0x29DB30` (2597); hooking any of those detours thousands of methods and hard-crashes on launch. **Always grep `RVA: 0x… ` and confirm a count of 1 before patching any `*Init`.**

### Mod String Localization Audit (2026-07-25)

**Problem**: `ModTextTranslator.T()` falls back key → `en` → **the key itself**. A `T()` call with no `mod_text.json` entry therefore speaks raw English in all 11 non-English languages, silently — no warning, no log line. 54 such keys had accumulated, including high-traffic ones (`{0}: {1} damage`, `Walk`, `Run`, `Encounters on/off`, every waypoint message, `Stick Click Normalization on/off`).

**Fix**: all 54 authored plus 4 new → `mod_text.json` now holds 250 keys × 12 languages, 0 incomplete.

**Repeatable audit** — nothing else surfaces this class of bug, so re-run it whenever `T()` keys are added: parse `mod_text.json`, regex every `T("literal")` call site across `*.cs`, and diff the two sets. Also assert every entry has all 12 language codes. Caveat: only catches string literals — `T(variable)` call sites can't be checked statically.

**Editing constraint**: `mod_text.json` is dense with non-ASCII. Per Rule 5, never touch it with PowerShell or a whole-file `Write` — use the `Edit` tool. Keys containing `{0}` are safe: `ParseNestedJson` locates the key between quotes *before* scanning for `{`.

### Event-State Input Gate, Repeat Dialogue, Unified Audio Gate (2026-07-25)

**Problem 1 — mod mode dead during dialogue.** `FFV_ScreenReaderMod.OnUpdate()` opened with `if (GameStatePatches.IsInEventState) return;`, so `inputManager.Update()` — and therefore `GamepadManager.Update()` and `ControllerRouter.Update()` — never ran. Story dialogue runs inside `SubSceneManagerMainGame.State.Event` (12), so *every* controller mod input was dead there, including the Back button that enters mod mode. Note the misdiagnosis trap: there is no dialogue check anywhere in `ControllerRouter.cs`; the Back branch has never been gated. The blocker was one early return three files away.

**Why removing it is safe**: the Pyramid 5F freeze (see 2026-02-17 entry) was caused by a *permanent Harmony trampoline on `Timer.Update`*, and the fix is the dynamic apply/remove in `TimerPatches.ToggleTimerFreeze()` — `Timer.Update` stays unpatched unless the player toggles timer freeze (Shift+T, off by default). `OnUpdate` installs no patches and is not on that causal path. The guard was extra insurance against generic per-frame IL2CPP overhead.

**Fix**: `OnUpdate` always calls `inputManager.Update()`. The protection's *substance* moves into `InputManager.DetermineContext()`, which now returns `KeyContext.Global` for event state as its **first** check — so field/entity/waypoint/teleport hotkeys and the entity scanner stay inert during cutscenes at the cost of one cached bool read. Side benefit: `ControllerRouter.IsFieldActive` is no longer frozen mid-cutscene (it was stale precisely because `ControllerRouter.Update()` never ran).

**Problem 2 — the mod menu was never actually gated either.** `IsFieldOrFieldMenuActive()` was `IsOnValidMap() && !IsInBattle`, which passes while an NPC talks; it only *looked* blocked because the `OnUpdate` return killed input first. Lifting that return exposed the missing gate, so `!DialogueTracker.IsInDialogue && !GameStatePatches.IsInEventState` was added there — one function covering all three consumers (F8, F5, Start button). New reason string `Unavailable during dialogue` in `SpeakModMenuUnavailable()`. **Mod mode stays ungated by design; only the mod menu is blocked.**

**Repeat dialogue** (`DialogueTracker.RepeatCurrentPage()`, ported from FF1 `MessageWindowPatches.RepeatLastDialogue`): re-speaks `GetPageText(lastAnnouncedPageIndex)` with `interrupt: true`. All the state it needs already existed. The speaker prefix is *always* re-attached, unlike the first announcement which only prefixes on speaker change — a repeat has no preceding context. Bound to bare `R` (`KeyContext.Global`, self-gated on `IsInDialogue` so it is a silent no-op elsewhere) and to mod mode + Square via a dialogue-first branch in `HandleModModeState()`. **`R` was free**: FF5's pathfinding filter is `Shift+\` / `Shift+P`, not `R` — `docs/plan.md` claimed otherwise and was wrong.

**Unified audio gate** — `AudioLoopManager.IsAudioSuppressed` replaces a copy-pasted two-stage condition in three loops plus two ad-hoc subsets:

```
BattleState.IsInBattle || GameStatePatches.IsInEventState || DialogueTracker.IsInDialogue
  || suppressed || !ControllerRouter.IsFieldActive || ControllerRouter.SuppressGameInput
  || GameStatePatches.IsScreenFading
```

Applied to `BeaconLoop`, `WallToneLoop`, `LandingPingLoop`, the footstep call site in `OnUpdate`, and the wall-bump postfix in `MovementSoundPatches`. Net new coverage: **footsteps and wall bumps were never menu-gated**, and the beacon was never fade-gated. Footsteps also needed this explicitly — their event gating had been an accident of the `OnUpdate` early return. Loop-specific map-change and `vehicleTransitionSuppressedUntil` windows stay in their loops. `IsScreenFading` is safe to widen: every failure path returns `false`, so it can never latch audio off permanently. Ordering matters — `inputManager.Update()` refreshes `IsFieldActive` before the footstep check reads it.

**Enemy HP toggle was a dead preference.** `PreferencesManager.EnemyHPDisplay` was written by F5 and `ModMenu` but read by **nothing**; `BattleTargetPatches.BuildEnemyAnnouncement()` hardcoded `$"{name}: HP: {cur}/{max}"`. Exact enemy HP is a cheat-adjacent disclosure, so this had to obey the setting. Now switches on the pref: 0 = `: HP: {0}/{1}`, 1 = `: N%`, 2 = nothing. Status effects still append in all three modes. That method is the **only** enemy-HP disclosure in the mod (single caller); `BuildPlayerAnnouncement` is party data and is untouched.

**Lesson**: when a feature is "unavailable" somewhere, check whether the *driver loop* runs there before hunting for a gate in the feature's own code — and when a preference appears not to work, grep its getter for readers before debugging the writer.

### Menu-State Leak Disabled All Field Features (2026-07-25)

**Problem**: after backing out of item-use targeting, every field feature went dead — entity cycling, pathfinding, wall tones — on **both** keyboard and controller. Mod mode and the mod menu still worked, which made it look controller-specific; it wasn't.

**Root cause**: `MenuStateRegistry.AnyActive()` gates both `KeyContext.Field` (`InputManager.DetermineContext`) and `ControllerRouter.IsFieldActive`. `ITEM_USE` is set by `ItemUseController.Show` and cleared only by `.Close()` — but `ItemUseController` is a state machine, and backing out of target selection returns to its `Non` state without calling `Close()` (that only runs when the whole item window closes). The flag latched on permanently. Nothing ever re-validated the registry, so **all 20 flags share this failure mode**: one missed `Close` hook silently disables the mod's field half until the game restarts.

**Fix, two layers**:
1. *Self-heal* — `GameStatePatches.ChangeState_Postfix` calls `MenuStateRegistry.ResetAll()` on `FieldReady | Player | ChangeMap`. `SubSceneManagerMainGame.State` (`dump.cs:376244`) gives menus their own states (`Menu=5`, `Shop=9`, `MenuLibraryUi=17`…), so `Player=3` provably means field control with nothing open. This recovers from any leak, present or future.
2. *Specific leak* — postfix on `ItemUseController.SingleExit` clears `ITEM_USE`.

**Folded-stub landmine** (extends the 2026-02-17 note): in `Il2CppLast.UI.KeyInput.ItemUseController`, the two hooks that look right are the dangerous ones — `NonInit()` and `AllExit()` are both RVA `0x2715A0`, the folded empty-method address shared by 2671 methods. `SingleExit()` is RVA `0xA20600`, count 1, and is the only safe exit hook. The `All` targeting path has no real exit body at all, which is why layer 1 is the primary fix. Patched manually via the existing `Patch()` helper rather than by attribute, so an unresolvable target logs a warning instead of aborting `PatchAll`.

**Also fixed in the same pass**:
- *Phantom "On chocobo" before "On foot"* — `FieldPlayer_ChangeMoveState_Patch` returned on `IsInEventState` *before* updating `lastMoveState`. Scripted mounts left the baseline stale, so the scripted dismount read as Walk→Chocobo and announced a state the player had held for a minute. Now the state is always tracked and only the speech is suppressed. `GetOn`/`GetOff` also gained "already in this state" guards.
- *Battle results "- ABP"* — the stored `abpToNext` was **correct**; a Freelancer (and a mastered job) genuinely has no next job level. The bug was announcing a column that could never apply. `BuildPointsGrid` now omits the ABP column when no character has a value, and `BuildFullRowText` skips `"-"` cells. Both the row summary and left/right navigation already iterate `colHeaders.Length`, so nothing else needed changing.
- *Second same-name KO silenced* — `BattleConditionController_Add_Patch` dedup'd on the rendered string, so two "Devil Crab: KO" collided. Re-keyed to a `HashSet<(IntPtr,int)>` of (unit instance, condition id), matching the `_lastActDataPtr` guard in the same file whose comment already warned that "a string compare would swallow the second". Persistent-condition re-fires are still suppressed.
- *Quick Save read a party slot* — `CharacterSelectionReader` walks 15 ancestors matching any name containing `character`/`chara`/`status`/`party`/`member`. The save-confirm screen embeds a party preview, so it matched and read slot 0. Added a negative guard for `save`/`load`/`popup`/`confirm`/`dialog` containers, with a one-shot log of the matched name for confirmation.

**Lesson**: latched booleans mirroring game UI state need a periodic authority to reconcile against — prefer the game's own state machine over hand-maintained flags, and always give such a mirror a recovery path.

### Equipment Slot Read Twice on Entry (2026-07-26)

**Problem**: entering a character's equip slot pane read the focused slot twice — `"R. Hand: Broadsword, Attack +15…"` at `03:35:11.258` and again at `.266`, one frame apart.

**Root cause**: two call sites reached `EquipMenuState.AnnounceEquipSlot`, and the `_lastSlot` guard that should have absorbed the second was defeated by a clear landing between them. `EquipmentInfoWindowController.SelectContent` fires while the pane *initialises*, not only on cursor movement — so it spoke first and set the guard. `Info_Init_Postfix` then ran, called `ClearLastAnnouncements()`, and its `MenuFocusAnnouncer` request re-read the same row a frame later with the guard wiped.

The ordering is what proves the direction: had `InfoInit` run first, its deferred read would have been deduped by the guard `SelectContent` set, and only one line would have been spoken.

**Fix**: removed the redundant call site — the `InfoInit` hook, along with `Info_Init_Postfix` and `TryAnnounceSlot`. No dedup added. `SelectContent` alone now owns the slot pane, matching FF4, which never had an `InfoInit` hook.

**Why re-entry still announces without it**: every exit from the slot pane lands in a pane whose `*Init` calls `ClearLastAnnouncements()` — cancelling goes to the command bar (`CommandInit`), choosing a slot goes to the item list (`SelectInit`) — so `_lastSlot` is always null by the time `SelectContent` fires on the next entry.

**Why the other two panes keep their hooks**: `EquipmentCommandController` has no navigation patch at all, and the select pane's `SetCursor` is a genuine cursor hook. Only the slot pane had a navigation announcer that doubled as an entry announcer.

**Lesson**: the initial-focus design assumes state-entry `*Init` hooks and navigation announcers are *disjoint*. That invariant does not hold for every controller — some navigation methods run during initialisation. Before adding an `*Init` hook, check whether the pane's navigation patch already fires on entry; if it does, the `*Init` hook is redundant. A guard that clears on entry cannot protect against this, because the clear is what unmasks the duplicate.

**Follow-up, same day**: the item-select pane had the identical defect — `"Empty, Attack +3"` and `"Leather Cap…"` each read twice, ~11 ms apart, on choosing a slot. `EquipmentSelectWindowController.SetCursor` also fires during initialisation, so `SelectInit` was redundant too. Removed it, plus `Select_Init_Postfix` and `TryAnnounceSelect`.

### Magic Command Bar Read Twice on Entry (2026-07-26)

**Problem**: Magic → select a character read the focused command twice — `"White Magic"` at `06:30:04.152` and again at `.189`, and the same on re-entry at `06:30:09.581/.592`.

**Root cause**: the third instance of the equip defect above. `AbilityCommandController.SelectContent` fires during `AbilityWindowController.CommandInit` with its `contentList` already populated, so it announces on its own. `AbilityCommand_Init_Postfix` then called `ClearLast()` — wiping the guard the navigation patch had just set — and its deferred `MenuFocusAnnouncer` read spoke the identical line a frame later. `MenuFocusAnnouncer` always yields at least one frame, so the later of any such pair is always the `*Init` read.

**Fix**: reduced `AbilityCommand_Init_Postfix` to **clear-only**. The `Request` call was the redundant announcer and is gone; `ClearLast()` stays.

**Why not delete the hook outright** (as the equip panes did): the equip sub-panes still had *something* clearing their guards — the command bar's `CommandInit`, which was kept. `AbilityWindowController` has no such umbrella: `Exit_Init_Postfix` (NonInit) clears the two `AbilityChangeController` guards but not `AbilityCommandController`'s. Delete the whole hook and entering Magic, backing out, and re-entering without moving the cursor would hit `index == _lastIndex` and go silent. Clearing a guard on entry is the *opposite* of a dedup net — it exists so a repeat entry does speak.

**Unresolved: the spell list.** `SpellList_Init_Postfix` (`UseListInit`) has the same shape, and whether it is redundant depends on something the dump cannot answer — dump.cs carries no method bodies, and this codebase contains both outcomes:
- `EquipmentSelectWindowController.SetCursor` fires at init **with data ready** → the `*Init` announce is redundant (line above).
- Bestiary `SetCursor` fires at init **with data not ready** — "first caches 'Bestiary', second overwrites with correct item" — so there the deferred read is the one that does the real work and must be kept.

`AbilityContentListController.Announce` returns false when `contentList` (0x50) is unpopulated, so both behaviours are possible. No log in any session has ever opened a spell list (the party has been all-Freelancer with no magic learned), so there is no evidence either way yet.

**How to settle it in one trip**: learn a spell, open Magic → character → White Magic. If the spell name reads twice ~1 frame apart, it is the same defect — reduce `SpellList_Init_Postfix` to clear-only exactly as above. If it reads once, the hook is doing the real work; leave it. No extra instrumentation needed — the speech log alone distinguishes the two.

Deleting it needed one extra step, though, and it is the interesting part: with `InfoInit` already gone, `SelectInit`'s `ClearLastAnnouncements()` was the *only* thing clearing `_lastSlot`, so cancelling from the item list back to the slots would have gone silent. Rather than restore a hook, the invalidation moved into the announcers themselves — `AnnounceEquipSlot` nulls `_lastSelectRow` and `AnnounceEquipSelect` nulls `_lastSlot`, each before its own dedup check so it still happens on a re-fire. Each guard now only suppresses an unchanged-row re-fire *within* its own pane, which is all it was ever documented to do.

`CommandInit` is the last remaining `*Init` hook here, and it stays: `EquipmentCommandController` has no navigation patch, so nothing else would announce the command bar's focused entry.

**Lesson**: when a shared "clear everything" call is removed, trace which guard each *other* caller was relying on it to reset. Cross-pane staleness belongs with the thing that changes focus, not in an entry hook that races the navigation patch.

### Quick Save Read a Party Slot — and Why the First Fix Missed (2026-07-26)

**Problem**: choosing Quick Save announced `"Bartz, Freelancer, Level 3, Front Row, HP 30/55, MP 8/14"` at `03:47:50.557`, 27 ms before `"Save your progress?"`.

**First attempt, and why it failed**: a name blacklist (`save`/`load`/`popup`/`confirm`/`dialog`) on the ancestor walk in `CharacterSelectionReader.TryReadCharacterSelection`. It never fired — its one-shot diagnostic was absent from the log, proving no ancestor carried any of those words. It was a guess about hierarchy naming, and the guess was wrong. Reverted in full; a substring blacklist there is also a latent hazard (a container named `loadout` would silently kill a legitimate read).

**Actual call path** — the read never went through the generic cursor heuristic, which is why a guard placed in that heuristic's walk could not help:

- `", Front Row"` is only produced by `CharacterSelectionReader.ReadCharacterInformation`, so the string is built there — but that method has two callers.
- `CursorExclusionHelper.ExclusionPatterns` already excludes `party` and `status` ancestors, and `ShouldSkip` bails when `SaveLoadMenuState.IsActive`, so the generic path is largely walled off from the party panel already.
- The tell was `[Status] initial focus read gave up after 6 frames` appearing during **Equip** character-select: Equip's character list is driven by the *Status* machinery. So the string comes from the dedicated `StatusWindowController` path.
- `StatusWindowController_SelectContent_Patch` guarded only on instance/cursor `activeInHierarchy` — no popup gate — and announced via `DelayedCharacterAnnouncement`, a one-frame coroutine into `StatusMenuState.AnnounceCharacterRow`.

Confirming Quick Save opens the popup, which re-drives the pause menu's party panel; the postfix's one-frame coroutine then spoke the party row. The 27 ms gap is simply two independent deferred reads (this one, and the popup's own delayed read) landing a frame or two apart.

**Fix**: `StatusMenuState.AnnounceCharacterRow` returns false when `PopupState.IsConfirmationPopupActive || SaveLoadMenuState.IsActive`.

**Why this is ordering-proof, unlike a synchronous guard**: `PopupState.SetActive` runs synchronously inside the `BasePopup.Open` postfix (`HandlePopupDetected` sets the flag, *then* starts its delayed read), while both routes into `AnnounceCharacterRow` are deferred — one-frame yield for navigation, settle loop for initial focus. So the flag is always settled by the time it is read, no matter whether `SelectContent` or `Open()` ran first within the frame. Returning `false` rather than `true` matches the method's "not readable yet" contract, so the settle loop retries and still announces if the popup closes while it is alive.

**Lesson**: when a guard does not work, check whether its diagnostic fired before adjusting its parameters — an absent diagnostic means the code never ran, so the whole placement is wrong, not the threshold. And identify which of a shared builder's *callers* produced the string before guarding the builder; a nearby unrelated log line (here, `[Status] … gave up`) can be the thing that pins down the path.

### Session Sweep: Guards, Battle Entry, Equip Details, Status Commands (2026-07-26)

**Index-keyed list guards.** Every list dedup guard compared the *built announcement*. Two key items are both named "Pendant", and with menu position announcements off the two rows produce byte-identical strings — `MenuPosition.Format` only disambiguates them when that preference is on — so the second was swallowed. Never Pendant-specific: two unequipped equip slots both read "Empty, Attack +3", and duplicate equipment appears twice in the select list. All guards now compare **index**, matching the ability patches which were index-keyed all along. The battle item/ability lists had no reset at all and gained `ClearLast()` on command-window rebuild, because index-keying alone would leave a stationary cursor silent when a stack's count changed.

**Popup initial focus — two subsystems, one symptom.** The log separates them by interrupt flag: `PopupPatches.DelayedPopupRead` always uses `interrupt:true`, so `"Leave?"` arriving at `interrupt=False` is the message system, not a popup.
- `ReadCommonPopup` already appended the focused button; `ReadJobChangePopup` did not. Extracted `AppendFocusedButton`; KeyInput `JobChangePopup` (dump.cs:474247) has `commandList` 0x50 (the constant already existed, unused) and `selectCursor` **0x60**. `InfomationPopup` is deliberately not a caller — it has neither field, it is message-only.
- The "Leave?" choice list is `Last.UI.KeyInput.MessageSelectController` (dump.cs:471170), which nothing announced on open; options only spoke once the cursor MOVED via the generic cursor reader. Hooked `Show(int defaultIndex)` — the moment the list is presented, carrying the starting focus. `shared=1`.

**Battle entry was far too late.** `BattleState.SetActive()` had one caller — `BattleCommandSelectController.SetCommandData` — so "in battle" meant "the first command window was populated", after the encounter effect, the scene load and the ATB fill. Every gate turns on with that flag (`IsAudioSuppressed`, `IsFieldActive`, `KeyContext.Battle`, cursor suppression), so wall tones ran through the whole transition. The loop was never at fault: it re-checks every 0.1s.

Now hooked at `Last.Map.EventProcedure.EventEncount` (dump.cs:323465) — the single funnel where random encounters (`FieldController.ExcuteEncount`) and scripted ones (the `Encount` opcode) converge, before the encounter SE and screen effect — plus `EventEncountBoss` (dump.cs:323468) for the separate boss path. Both `shared=1`. `EventEncountBoss` is an explicit interface implementation (`Last.Map.IEventAccessor.EventEncountBoss` in metadata), so it resolves through a candidate list and warns if unresolved. `STATE_BATTLE = 13` in the existing `ChangeState` hook backstops Colosseum/AR battles.

**Pooled BattleActData.** `_lastActDataPtr` compares object addresses, and `BattleActData` is pooled — the next battle hands back the same address, so "Bartz attacks" was swallowed if Bartz also acted last in the previous battle. Both battle-message guards are intra-battle by design, but their only reset ran per *turn* from `SetCommandSelectTarget`, never at a battle boundary — and could not, since a preemptive strike or enemy-first action produces actions before any command window. Now reset from `SetActive()`, which is only reliable *because* of the earlier entry signal above.

**Battle menu is a state machine.** `BattleCommandSelectController.State` = `None/Change/Normal/Defence/Manipulate`. Left/right switch **state** via `UpdateByState`, they do not move a cursor, so the `SetCursor` hook never fired — vertical spoke, horizontal was silent. All four states share one `contentList`, repopulated per state from `normalList`/`changeList`/`defenceList`/`manipulateList`, so the existing announce path already produced the right string; only the trigger was missing. Clearing the cursor guard is the load-bearing half: switching state usually lands on the *same* index (0 in Normal, 0 in Defence), so anything `SetCursor` did fire was swallowed. Only `*Init` is hooked (all `shared=1`) — never `*Exit`, whose empty bodies fold onto the 4398-method stub address.

**Equip menu had no detail wiring at all.** Zero references to `AutoDetailEnabled`, so F7 did nothing there. The slot pane never spoke a description because the property on `OwnedItemData` is spelled **`Deiscription`** (the game's own typo, dump.cs:364383) — `.Description` does not exist on that type. Auto Detail now gates stat line and description together on both panes.

This also fixed a live cross-menu bug: pressing `I` in equip read the last *Items-menu* row, because nothing in equip wrote a tracker and `ItemMenuTracker.ValidateState()` is a bare `return IsActive` with no liveness check, while backing out of Items fires `MainMenuController.InitNone`, which clears nothing. New `EquipMenuTracker` stores extracted values (the panes hand over different types — `ItemListContentData` vs `OwnedItemData`), takes focus by clearing the item/job trackers, and sits ahead of the item branch in both cascades. **Open**: the slot pane supplies `OwnedItemData.TypeId` and the select pane `ItemListContentData.ItemType`; `BuildEquipJobsAnnouncement` gates on 2/3, and whether these share a content-type space is unconfirmed — each pane logs its first value once.

**Status Commands/Abilities panel.** Read from the rendered rows via `StatusDetailsView.EquipAbilityContentList` → `view.IconText.nameText`, **KeyInput variant only**. Unlike the battle-result case the Touch `StatusDetailsController` is *not* a stub — it has a full `SetBattleCommandText` — so patching the wrong one compiles, looks correct, and silently never fires.

Do **not** reconstruct this list. `SetBattleCommandText` runs four filter helpers and two `Func<Command,bool>` predicate caches which are exactly what strip Defend/Row/Flee; rebuilding from `Job` command ids, the master `Command` table or `OwnedAbilityList` puts them back. `StatusDetailsCommandChangeBaseController.GetAllAbility` is the full pick-list, not the screen. Rows are gated on `activeInHierarchy` + non-null `TargetData`, because the prefab hides unused slots rather than destroying them (`SetActiveNoneAbilityText`), so an inactive row holds the previous character's text. `statList` is rebuilt per character and `GroupStartIndices` computed from it — fixed indices cannot describe a variable-length group.

**Cleanups.** Deleted unreferenced `BattleState.ForceReset` (`MessagePatches.ForceReset` and `AudioLoopManager.ForceResetInternalState` are live — verify callers before touching anything by that name). `RestoreNavigationAfterBattle` went from five bools to one: four were passed and ignored once enabled state moved to `PreferencesManager`, which collapsed `NavigationStateSnapshot` to its one genuinely per-battle field.

**Status**: all of the above build clean and are deployed; none are verified in-game yet.
