# FF5 Screen Reader Mod — Architecture & Debug Reference

## Mod Architecture

### Core (`Core/`)
- `FFV_ScreenReaderMod` — Entry point, SpeakText() (strips rich-text tags centrally), SpeakTextDelayed(), entity refresh, scene transitions. (There is no Ctrl+F8 kill switch any more; older notes describing one are stale.)
- `InputManager` — Keybinding dispatch via KeyBindingRegistry. F5/F7/F8 inline; F1/F3 are narrated by `GameTogglePatches`, not polled. KeyContext-based dispatch (Global, Field, Battle, BattleResult, Status, Bestiary, KeyHelp); W/S alias Up/Down in the three buffer contexts
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
- `PopupPatches` — All popup types (common, game over, info, job change, change name, input)
- `GameStatePatches` — BattleState, map transitions, IsInEventState (cached via ChangeState hook), config menu bestiary detection (states 17/18)
- `TitleMenuPatches` — Title screen
- `GameTogglePatches` — F1 walk/run and F3 encounters, from the game's own setters (any input source)
- `BattlePausePatches` — Battle pause menu (Resume / Return to Title)
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
- `FieldNavigationHelper` — Pathfinding, distance, terrain attributes, landing detection. Owns `FindPathTo` (the single choke point for every route), `PathInfo`/`RouteLeg`/`RouteFailure`/`PathSearchMode`, `BuildLegs`, and `DescribeRoute`
- `Routing/` — Long-range route search. Portable by design: no speech, no localization, no logging, no god-class references. See "Routing architecture" below
  - `RoutingAdapter` — Every game-specific fact (tile size, world-map ids, loop flags, transport ids, diagonal-movement rule, logging hook). **The only file a port should need to edit.** Contains no struct offsets — keep it that way
  - `VehicleRouteSearcher` — Terrain-attribute grid + per-transport passability + memoised BFS flood. Used when riding
  - `BreadcrumbRouteChainer` — Hop-level A* chaining several `MapRouteSearcher` searches. Used on foot beyond the game's window
  - `PathSpaceNormalizer` — Self-calibrating cell-vs-world coordinate detection for the game's searcher

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
rather than `WaitForSeconds` (Rule 3). Same shape as the old Gallery/MusicPlayer entry coroutines,
tightened from seconds to frames; since Round 2 (2026-09-24) those two entry reads run through it too. `yield` sits **outside** the `try` (yield-in-try-with-catch is illegal).

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

**Solution**: Read private `contentList` field (offset 0x50) via IL2CPP pointer access. This is `List<BattleAbilityInfomationContentController>` indexed by visual grid position. Each controller's `.Data` property returns the `OwnedAbility` at that slot (null for empty/unlearned). Uses established unsafe pointer pattern (see `PopulateVehicleTypeMap`).

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

**Generalised tool + official-name pass (2026-09-23).** `tools/extract_entities.py` is now the shared FF2–FF5 version (`GAME` defaults to FF5; `full` works as before). New modes: `missing <out>` runs every label through a Python mirror of this mod's 4-tier lookup and reports only true misses (currently 0 of 1367), and `gamedict <out>` builds Japanese → {lang} from the game's own message tables (`story_cha` speaker names + `system`). Using that dictionary, 75 existing entries whose key is itself a game string were aligned with the game's localisation (344 values, applied identically to `translation.json` and `translation.generated.json`, which stay byte-identical). Examples: アポカリョープス keeps "Azulmagia" in English but is "Apokalyp"/"Apocalipso"/"아포칼리옵스" in de/es/ko as in the game; ストーカー → Wendigo; ネレゲイド → Nereid; 飛竜/飛竜草 → Wind Drake/Dragon Grass; タイクーン城/ウォルスの塔 → Castle Tycoon/Tower of Walse; 大臣 → Chancellor; 長老の枝 → Guardian Branch. Left alone: shop words, カエル, 魔物, とげ, ギル (official text is a different context).

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

**Also**: InputManager.cs Ctrl+K keybinding log prefix changed from `[DIAG]` to `[Input]` (kept as intentional last-resort entity rescan). *(Superseded: no Ctrl+K binding exists any more; the manual rescan is backtick, "Entity scan complete".)*

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

### Job List Read Three Times on Entry — the Clear, Not the Announcer (2026-07-26)

**Problem**: entering Jobs read `"Knight Lv. 0: ABP: 0/10"` three times — `10:32:56.757`, `.759`, `.771`.

**Why this is NOT the equip/magic defect, despite looking identical**: the same entry logged `[JobSelect] initial focus read gave up after 6 frames` at `.857`. "Gave up" means every one of the six deferred attempts returned false, so `MenuFocusAnnouncer` **never spoke**. All three lines came from the navigation patch, `JobChangeWindowController.SelectContent`. Removing the redundant *announcer* — the fix that worked for equip and the magic command bar — would have changed nothing here.

**Root cause**: `SelectContent` fires several times while the job list builds, *and* `SelectJobInit` runs more than once per entry. `_lastIndex` should have absorbed fires 2 and 3, but `JobSelect_Init_Postfix`'s own `ClearLast()` landed **inside that burst** and wiped the guard twice. Two extra clears, two extra reads. Arithmetic that only closes if `SelectJobInit` re-enters: one clear can unmask at most one duplicate.

**The generalisation** (now the header comment of `FieldJobAbilityReannouncePatches`): *a guard is cleared on the paths that LEAVE a state, never on the path that enters it.* An exit-path clear can only ever permit a later announcement; an entry-path clear races the game's own initialisation burst. Every `*Init` in that file now clears the guards of the **other** states in its family, so entering a state always finds its own guard already cleared by wherever it came from.

**Fix**:
- Job list: `SelectJobInit` unpatched entirely (`JobSelect_Init_Postfix` deleted). `SelectContent` owns the entry read, as proven by the log.
- `JobChangeWindowController.SetActive(bool)` clears the job guard on the **hide** edge only — the show edge is not known to be once-per-open, and a clear while the window is up would be back inside the burst.
- `ConfirmPopupInit` / `FixJobChangeInit` clear it too, so backing out of the confirm popup re-announces.
- Ability window (`CommandInit` / `UseListInit` / `UseTargetInit`) and ability equip (`SelectCommandInit` / `SelectListInit`) swapped to sibling clears; `ChangedInit` / `ConfirmPopupInit` clear both equip panes; `AbilityWindowController.SetActive` and `AbilityChangeController.SetActive` clear their families on hide.

**Why the deferred `Request` calls stay everywhere except the job list**: they are self-limiting. If navigation already spoke, `Announce()` hits the guard and returns false — the worst case is a `gave up after 6 frames` log line. That makes the fix correct whether or not navigation fires during a given `Init`, which matters because dump.cs has no method bodies and this codebase contains both outcomes (see the spell-list note above). Only the job list had *proof* its `Request` never spoke, so only it was deleted.

**Folded-stub note**: `JobChangeWindowController.ResetController` shares address `0x2715A0` (2560928) with **4397** other methods — the same empty-body folding trap as `NoneInit`. `SetActive` is what carries window-level exit cleanup instead. All five newly hooked addresses were confirmed to have exactly one owner in `script.json` before patching.

**Known gap**: switching the target character with L/R inside the job list does not re-announce if the cursor stays on the same row. Pre-existing — `SelectJobInit` did not re-fire on that transition either. `UpdateTargetCharacterView` is the obvious hook and is deliberately *not* used: it also runs during initialisation, so it would be an entry-path clear.

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

**Status**: verified in-game 2026-07-26, with one exception found by the startup warning — see the boss-encounter note below.

### Offline Data Sources: Ghidra Project and Game Bundles (2026-07-26)

Two capabilities that were available all along and were not written down, each of which cost a wrong "that's runtime-only / not recoverable" conclusion before being found.

**The Ghidra project is `FF5_Analysis`, not `GameAssembly.dll.c`.**

`FF5/GameAssembly.dll.c` (55 MB, 1.3M lines) is a **types-only** export — structs, typedefs and vtables, ending in PE/DOS header definitions. It contains **zero function bodies** (`grep` for `FUN_`, `^{` returns nothing). Concluding from it that "no decompilation exists" is wrong.

The real analysis is at `D:\Games\Dev\ghidra_12.0.3_PUBLIC\projects\FF5_Analysis.rep`, program name `GameAssembly.dll` (549 MB, fully analyzed). A single function decompiles from it in about a minute:

```
GHIDRA_HEADLESS_MAXMEM=8G analyzeHeadless.bat \
  "D:\Games\Dev\ghidra_12.0.3_PUBLIC\projects" FF5_Analysis \
  -process GameAssembly.dll -noanalysis \
  -scriptPath <dir> -postScript DecompileOne.java
```

`-process ... -noanalysis` only — never `-import` (destroys the analysis) and never a timeout flag (aborts with no partial save). The script takes a VA (dump.cs prints `RVA` and `VA` per method; image base is `0x180000000`), calls `getFunctionContaining`, and prints `DecompInterface.decompileFunction(fn, 0, monitor)`.

**This is the answer whenever dump.cs is signature-only.** Any hardcoded table built in a constructor — the exact case that blocked the jobs-screen work — is recoverable this way. IL2CPP helper calls are readable in the output: `FUN_18155fcf0(list, N, ...)` is `List<int>.Add(N)` and `FUN_1813f2030(dict, key, list, ...)` is `Dictionary<int,List<int>>.Add`. Note one constructor may build several such dictionaries in sequence (`JobInfomationData` builds equip icons, learned abilities, passives, equip abilities and learning levels), so a parser must stop at the first dictionary rather than keying blindly — later dictionaries reuse keys 1..22 and silently overwrite.

**Game data is plain text inside the Addressables bundles.** UnityPy is installed and `tools/extract_entities.py` is the working precedent. Under `FINAL FANTASY V_Data/StreamingAssets/aa/StandaloneWindows64/`:

| Bundle | Contents |
|---|---|
| `master_assets_all_*.bundle` | **92 master tables as plain CSV** TextAssets — `weapon`, `armor`, `job`, `job_group`, `content`, `icon`, `parts_group`, `item`, `monster`, … |
| `message_assets_all_*.bundle` | **Localized text**, tab-separated `MSG_ID\ttext`. English is `system_en` (89 KB) + `etc_text_en`; 12 languages present |
| `assetspath_assets_all_*.bundle` | The Addressables path index — grep it to find which bundle holds an asset |

So master data and every localized string are readable offline. `MasterManager.GetList<T>()` at runtime and these CSVs are the same data.

**Column traps found the hard way:** `weapon.type_id` is *not* the weapon category — it is `1` for 107 of 108 rows. The category is `weapon.category_type`, and `armor` has no category column at all (its `parts_group_id` is the *slot*). `content.icon_id` is `0` for all equipment, so item icons do not come from there; each item's icon is embedded as an `<IC_XXX>` prefix in its localized name string.


### Jobs Screen "Equippable" Row (2026-07-26)

The row is sprite icons with **no text**, so nothing could be scraped from the screen. Its source is a hardcoded `Dictionary<int, List<int>>` in the constructor of `Serial.FF5.Management.JobInfomationData`, surfaced at runtime via `ProviderManager.Instance.JobInfomationProvider.GetEquipIconList(jobId)`.

**Extracted offline**, no playtest and no unlocked jobs required — the dictionary is built at construction for all 22 jobs and never consults save data. Decompiled that one constructor out of the `FF5_Analysis` Ghidra project (see the previous entry) and mirrored the result as a static table in `Utils/JobEquipData.cs`. Mirrored rather than called at runtime because it is build-time constant, it avoids a 22-job × 190-item scan per key press, and the returned icon ids need names the game does not ship.

**Naming.** There is no message id for any weapon category — `MENU_JOB_EQUIPMENT` ("Equippable") and `MENU_JOB_WHICH_ALSO` ("Any") exist and are read from the screen, but the categories themselves are mod-authored in `mod_text.json` (16 keys × 12 locales). Names were confirmed against the game's own English item names in `system_en`, cross-checked with the FF5 wiki's weapon-type list:

- `IC_WND` is **Staff**, not "wand" — every item in it is a Staff.
- `IC_NSRD` is **Knight Sword** (Excalibur, Ragnarok, Defender), distinct from `IC_SRD` **Sword** (Broadsword, Long Sword).
- `IC_TRW` is **Boomerang** (Moonring Blade, Rising Sun), which is *not* the same as the wiki's "thrown".
- `IC_AX` and `IC_HMR` are separate **icons** even though the master data files axes and hammers under one `category_type` — which is why Berserker shows both.

**Two wiki types deliberately absent.** "Thrown" (`IC_SRK`: Shuriken, Fuma Shuriken) and `IC_BAG` (Ash) resolve to job group 2 — *nobody* can equip them; they are Throw-command consumables, so they never appear in a job's row. "Short sword" is not its own icon: Kunai / Kodachi / Sasuke's Katana sit in the katana `category_type` but carry the knife icon.

**One type the wiki omits.** `IC_FLL` (Flail, Morning Star) is a real equip category restricted to Red Mage, White Mage, Time Mage, Chemist and Mime — exactly matching the extracted rows. The wiki folds flails into staves, but the game draws a separate icon, so it gets its own name: one spoken name per icon keeps the readout 1:1 with what a sighted player sees, which is the rule for this screen.

**Panel gate (a live bug, fixed here).** The footer's `X — View Description` swaps the info panel between the equippable row and the job description, as two sibling GameObjects on `JobChangeWindowView`: `infoContentBase` (0xF8) and `infoDescriptionBase` (0x100). `JobDetailsAnnouncer` read `InfoJobDescriptText` unconditionally, so with the equippable row showing, `I` spoke a description that was not on screen. It now reads whichever panel is active.

**Deliberately not announced: job stat modifiers.** `Last.Data.Master.Job` exposes `Strength`/`Vitality`/`Agility`/`Magic` (dump.cs:353633), and they sit right beside everything else this feature touches — but the panel does not display them. Speaking them would invent information the sighted player does not have.

Freelancer (job 1) and Monk (job 3) are intentionally empty in the table: the game routes them through `IsAll` / `IsNothingAllEquip` and prints text, which is read from the screen so it stays localized.

**Status**: verified in-game 2026-07-26.

### Il2CppInterop Mangles Explicit Interface Implementations (2026-07-26)

**Problem**: `BattleStartPatches` hooked `EventEncount` fine but not its boss counterpart. The startup warning fired:

```
[BattleStart] Could not resolve a hook for boss encounters
  (tried: Last.Map.IEventAccessor.EventEncountBoss, EventEncountBoss).
  Field audio will keep playing through the transition until the battle scene loads.
```

Random and scripted encounters entered battle state at the encounter as intended; **bosses silently fell back to the scene-load backstop**, ~1–2 s later.

**Cause**: IL2CPP metadata (and dump.cs) spell an explicit interface implementation with dots — `Last.Map.IEventAccessor.EventEncountBoss`. Il2CppInterop rewrites those dots as **underscores**. Confirmed by string-scanning the generated assembly at `MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll`:

```
Last_Map_IEventAccessor_EventEncountBoss
NativeMethodInfoPtr_Last_Map_IEventAccessor_EventEncountBoss_Private_Virtual_Final_New_Void_MonsterParty_Vector2_Int32_0
```

**Rule**: when patching a method dump.cs prints as `Namespace.IInterface.Method`, the interop name is `Namespace_IInterface_Method`. `AccessTools.Method` will not find the dotted form. The generated assemblies under `MelonLoader/Il2CppAssemblies/` are the authority on any interop name and can be grepped directly — no need to guess or discover it at runtime.

**Fix**: correct name first in the candidate list, plus a reflection fallback matching any method whose name *ends with* the bare method name, so a future interop naming change degrades to a scan rather than to silence.

**Lesson (the reason this was caught at all)**: the warning named the *user-visible consequence*, not just the failure. A bare "patch failed" line would have been scrolled past; "field audio will keep playing through the transition" is checkable. Every `TryPatch` warning in this codebase should read that way.

---

## Routing architecture (2026-07-26)

### The two limits that forced a mod-owned searcher

`MapRouteSearcher` cannot route a vehicle, for two independent reasons:

1. **A 64x64 cell window.** `MapRouteSearcher.SearchHorizontalLimit` and
   `SearchVerticalLimit` are both 64 (`dump.cs:262506`), so the search covers roughly
   ±31.5 tiles from the start. The far side of a world map is out of reach by construction —
   this is why Wind Shrine → Tule never worked, on foot or otherwise.
2. **It models walking.** It searches the *collision* mapping data. A ship crossing ocean
   and an airship crossing mountains are invisible to that model.

### Which searcher runs when

`FieldNavigationHelper.FindPathTo` is the single choke point — all five call sites go
through it, so nothing needs its own routing logic.

| Situation | Searcher |
|---|---|
| Riding, on a world map | `VehicleRouteSearcher` (unbounded flood over terrain attributes) |
| Riding, in an interior | Game's `MapRouteSearcher` — see the interior guard below |
| On foot, target within ~31 cells | Game's `MapRouteSearcher`, unchanged |
| On foot, target beyond the window, `PathSearchMode.Full` | `BreadcrumbRouteChainer` |
| On foot, `PathSearchMode.Quick` | Game's `MapRouteSearcher` only — never chains |

**Interior guard**: the vehicle searcher additionally requires `IsWorldMap(mapId)`, not just
vehicle state. Terrain attributes govern movement on world maps; inside towns and dungeons
it is layers and collision entities that decide, and the attribute grid models neither. The
player *can* be mounted in an interior (riding Boko in before the scripted dismount), so
vehicle state alone is not a sufficient gate — a route built from the grid there would be
confidently wrong.

### `MapRouteSearcher.Search` THROWS outside its window — it does not return empty

The single most important fact in this section, because getting it wrong silently disabled an
entire subsystem for a full test session.

For a destination outside the 64x64 window, `Search` raises `MapRouteSearchException`
(`dump.cs:262688`). It does **not** return `null` or an empty list. Any code that treats "no
route" as "empty result" will never see the far-target case at all.

`FieldNavigationHelper.FindPathTo` originally wrapped its whole body in one `try` whose catch
set `pathInfo.ErrorMessage` and returned **without logging**. So every long-range target threw,
the catch swallowed it, and the breadcrumb fallback sitting a few lines below was unreachable.
Symptoms: far targets said "no path", near targets behaved normally, and the log contained
nothing at all — not even a warning.

Now: every `Search` call goes through `FieldNavigationHelper.SafeSearch`, which converts a
throw into "no points" so the normal empty-result path runs. Both that catch and the outer
backstop log **once** (they are reached from the 2 s beacon loop and once per entity in the
pathfinding filter, so ungated logging would flood).

**Rule for this codebase:** a catch that wraps a whole feature must never be silent. The
one-shot log costs nothing and is the difference between a five-minute diagnosis and staring
at a clean log wondering why a feature does nothing.

### Removed: the vehicle-requirement inference ("Requires Pirate Ship")

Built, then deleted 2026-07-26 before ever shipping usefully. Recorded because the idea is
tempting enough to be reinvented.

It flooded the terrain grid once per vehicle under that vehicle's `OkList` and reported the
least-capable vehicle whose reachable set contained the target — so a town across the ocean
answered "Requires Pirate Ship" instead of "no path".

**Why it had to go: terrain reachability cannot see event gating.** A destination may be
closed by an unfinished story event rather than by terrain, and nothing in the terrain data
distinguishes the two. A confident "Requires Pirate Ship" when the real blocker is an
unfinished event sends the player somewhere useless — strictly worse than saying nothing. The
claim was unfalsifiable from inside the mod.

**Terrain naming would not have rescued it.** This is the part worth understanding, because
"if only we could tell water from mountain" is the natural next thought. The check never
classified terrain: it asked each vehicle's `OkList` directly, which is the game's own ground
truth for "may this vehicle occupy this tile". A terrain *name* would have to be derived from
that same data ("walking can't enter, ship can" → water), making it a strictly longer path to
an identical answer with an extra chance to mislabel. Naming was only ever narration on top of
the same unreliable inference. The defect was never the labelling — it was the inference.

Two further flaws found while removing it, both real, neither the reason:

- The flood started at the **player's** cell under the *vehicle's* rules, with the start tile
  seeded passable unconditionally. Standing on land under ship rules, it leaked into adjacent
  water and spread over the whole ocean — it never verified the ship was reachable or even
  present.
- An airship's `OkList` admits nearly every tile, so its flood reached everything including
  tiles it could never land on. Reachability is the wrong test for a flying vehicle;
  `CheckLandingList` is.

Failure wording therefore reverts to exactly what the mod said before long-range routing
existed: the entity announce says `no path`, the waypoint pathfind falls back to
`waypoint.FormatDescription`. `RouteFailure` survives with `NoPathProved` / `StoppedEarly`,
spoken by nothing, set and logged by the chainer — that distinction separates a real finding
from a tuning problem when reading a log.

### `CheckOkList` indexing

Decompiled at `docs/Scripts/decompiled_vehicle_methods.c:758`:
`CheckOkList(transportId, attribute)` looks up `modelList[transportId]`, indexes
`okList[attribute - 1]`, and returns `== 1`. **Attribute 0 always returns false** — the
guard short-circuits before the index. `CheckLandingList` is identical against `landingList`.
`VehicleRouteSearcher` mirrors this by starting its lookup table at index 1.

### Cell attributes are "foot IDs" — and there are no terrain names

Confirmed by a full dump search (2026-07-26). The integer from
`FieldController.GetCellAttribute` is a **foot ID**, and it is the *same* integer the
Geomancer/Gaia ability uses:

```
GetCellAttribute -> foot ID
  -> MapAttribute.footInfoList : Dictionary<int, FootInformation>   (dump.cs:322083)
  -> FootInformation.battle_background_asset_id                     (dump.cs:351290)
  -> BattleBackgroundAsset.ability_random_group_id                  (dump.cs:347273)
  -> AbilityRandomGroup -> the Gaia spell pool                      (dump.cs:346331)
```

Battle terrain and map cell attribute are **not** separate systems — no per-map "field type"
field is involved.

**There is no terrain name anywhere in FF5 PR.** No terrain enum, no terrain master table,
no `message_id` on `FootInformation` or `BattleBackgroundAsset`, no terrain strings in
`stringliteral.json`. `MessageManager` cannot localize a cell attribute because nothing in
the chain carries a message id. `MapConstants` has 15 nested types and none of them are
terrain. (False friend: `AttributeType` at `dump.cs:312572` is *elemental* — Flame/Cold/
Thunder — not terrain.)

This is why routing answers **capability** ("Requires Pirate Ship") rather than terrain
("crosses ocean"): a cell the walking transport cannot enter and the ship can *is* water by
construction, and the capability answer is both derivable and more actionable. No terrain
name table is needed or wanted.

If a naming feature is ever wanted, the only two semantic sources are:
- `Serial.FF5.Map.TransportationEvent.RiverFootAttribute` / `ForestFootAttribute`
  (`dump.cs:289194`) — hardcoded `List<int>` of foot IDs. Exactly two categories.
- `FootInformation.BattleBackgroundAssetId` as a stable terrain-*class* key (all forest
  tiles share one), with a mod-supplied name table. The asset names themselves are numbered
  (`bg_ff6_29` style in the FF6 dump), not semantic.

**Foot IDs are not fixed for the whole game.** `MiscAssetDesc.MapFootAttribute` /
`ConversionTargetFootId` / `AfterConversionFootId` (`dump.cs:276606-276634`) remap foot IDs
between story flags, applied via `MapModel.SetConversionFootId` (`dump.cs:328268`). The
cached grid is therefore dropped on map transition **and on every exit from event state**,
because a cutscene can flip such a flag without a map reload. Rebuilds are lazy, so the
invalidation is free unless the player then routes.

### Why a flood and not per-target A*

`VehicleRouteSearcher` runs one uniform-cost BFS from the player and memoises it on
`(mapId, transportId, playerCell)`. Movement cost is uniform, so BFS is already an exact
shortest-path search, and one flood answers all three questions the callers ask:

- entity-filter reachability → `dist[target] >= 0`, O(1) per entity
- a path to any target → walk the `parent` array
- the nearest reachable stand-in tile → ring-expand from the target

That last one is what makes an airship useful: it cannot sit on a town tile, so the route
goes to the closest tile it *can* occupy and `PathInfo` carries the remaining offset plus
whether that tile is a landing spot.

### Neighbour order is load-bearing — do not reorder it

`EnsureFlood` visits **north, south, east, west** and only then the diagonals. That order is
not arbitrary. In a fixed-order FIFO BFS the first predecessor to reach a cell claims it, and
visiting both vertical neighbours before both horizontal ones makes every shortest path
canonical: the vertical run is always claimed first. On open terrain a route therefore
decomposes into exactly two legs — `north 40, west 30` — instead of 70 alternating single
steps that `BuildLegs` cannot merge and `DescribeRoute` would truncate at six legs plus
"and 64 more".

This was verified by replicating the BFS and the leg decomposition outside the game before
any code changed: 4-connected open terrain gives 2 legs for every start/goal pair tried, and
still 2 legs around a 20x30 obstacle. No tie-break or path-straightening pass is needed, and
one was deliberately *not* added. Reordering these four calls silently degrades every spoken
route, so if this ever needs to change, re-run that check first.

### Diagonals for free-moving vehicles

`RoutingAdapter.AllowsDiagonalMovement` 8-connects the flood for `TransportationType`
`Plane` (3), `LowFlying` (7) and `SpecialPlane` (8), via the public
`TransportationController.GetTransportationType(id)` — no struct offsets. The airship and
the wind drake fly with free directional input (the game swaps in
`FieldPlayerKeyAirshipController` for them), so routing them on 4 neighbours both overstates
the distance and describes a path the player would never fly. Measured on open terrain:
40 north / 30 west is **70 steps 4-connected, 40 steps 8-connected**, two legs either way
(`north 10, northwest 30`).

Everything else stays 4-connected, walking included — town and world-map foot movement is
grid-locked, so a diagonal instruction there would be unfollowable.

Cost stays uniform at 1 per step, so the BFS remains an exact shortest-path search; it just
becomes Chebyshev rather than Manhattan distance, which is the right cost model for analog
movement. A **corner rule** rejects a diagonal step when *both* orthogonal neighbours are
impassable, so a flyer never squeezes through a pinch its `OkList` does not permit — it still
rounds a single-tile obstacle diagonally.

The diagonal flag is part of the flood cache key, so swapping ship for airship rebuilds
rather than reusing a 4-connected flood.

### World-map wrap comes from the game, not a guess

`RoutingAdapter.WorldMapWrapsX/Y` used to return `IsWorldMap(mapId)` — a guess. They now read
`MapModel.GetLoopType()` (`dump.cs:331104`) against `MapConstants.LoopType`
(`None=0, Horizontal=1, Vertical=2, All=3`), reached via `fieldController.mapManager`
→ `CurrentMapModel`. `RoutingAdapter.RefreshMapInfo` caches it per map and is called from
`EnsureGrid`, which is the only thing that runs before a flood — that ordering is what
guarantees `TryVisit` never reads a stale flag.

The old heuristic remains the fallback if the read throws, so a failure degrades to previous
behaviour rather than silently disabling wrap on a map that does loop. `RefreshMapInfo` also
cross-checks `fieldController.CheckCurrentWorldMap()` (`dump.cs:325497`) against
`GameConstants.IsWorldMap` and warns once on a mismatch — behaviour is unchanged, but a
missing world-map id shows up in the log instead of having to be debugged blind.

### Breadcrumb chaining: what makes it correct

The lever is that `MapRouteSearcher.Search` accepts an **arbitrary start cell** — it need not
be the player's. So the mod searches from virtual cells, stitches the results, and never
moves anything, keeping layers/hidden passages/`IgnoreRoute` handled by the game.

Two design points that are easy to get wrong:

- **A* over hop nodes, not a greedy chain.** "Hop toward the target, repeat" is greedy
  best-first with a 31-tile horizon and dead-ends permanently on any concave obstacle bigger
  than the horizon. Edges are real `Search` results, edge cost is the returned step count,
  and a closed set lets a pocket be costed and abandoned.
- **Bound the region, never the trajectory.** An earlier draft bailed when the last few hops
  had not got closer. That is wrong: a route that must back out of a room regresses for
  several hops before turning the corner, so *any* "give up when not getting closer" rule
  cannot solve those maps. The bound is instead
  `max(WINDOW_RADIUS, |start − target| × 1.5) + 32` cells from the start. Backward candidate
  bearings (`±135°, 180°`) are in the candidate set from the **first** expansion, never added
  as a last resort.

**Two outcomes, both silent about the reason:**

| Outcome | Condition | Spoken |
|---|---|---|
| No path proved | Open set emptied, no budget hit | Caller's existing failure wording |
| Stopped early | Hop cap / wall clock / region bound fired | Caller's existing failure wording |

They sound identical on purpose — the player cannot act on the difference, and a partial route
into a dead end is worse than silence. The distinction lives in the log only.

**Cost note.** The removed vehicle pre-check also short-circuited the expensive case, so an
ocean-locked target now runs the hop search to exhaustion before reporting failure. That
should be *fast* in exactly that case — the player is on an island, the reachable node set is
small, and the frontier empties quickly. The budget (`MAX_HOPS` 24, `BUDGET_MS` 400) caps the
worst case on large open landmasses. Watch the log rather than pre-optimising.

### Coordinate space: a real ambiguity, resolved at runtime

`MapRouteSearcher.Search` is *given* cell positions and its `Node` type stores `CellPos`,
which suggests it returns cell positions. But every route is described through
`DirectionHelper`, which reads **+y as north** — true in world space and backwards in cell
space, where y grows southward. One of those readings is wrong and the dump cannot say which.

`PathSpaceNormalizer` settles it empirically instead of guessing: the first path of a session
is compared against the start expressed *both* ways (two references, because a single
threshold would misfire at the coordinates where a cell index and a world unit coincide).
The verdict is cached and logged, and cell-space paths are converted so callers only ever
see world space.

**Answered in-game 2026-07-26: `WORLD space`.** There was no pre-existing north/south
inversion — on-foot routes were always described correctly. The conversion branch is
therefore dead code on FF5. It stays as a guard for the sibling ports, where the answer has
not been measured and must not be assumed.

### Performance notes

(`docs/PerformanceIssues.md` referenced in CLAUDE.md does not exist in this repo; routing
performance notes live here.)

- **Attribute grid build** — one `GetCellAttribute` per cell, so 65k interop calls on a
  256x256 world map. Built from the map-transition hook so the cost lands during a loading
  screen, and only for world maps. Elapsed ms is logged. If it ever exceeds ~150 ms, switch
  to a bulk `int[,]` read of `GetCurrentMappingData()` using the Il2CppArray layout (2D
  bounds at `+0x10`, 16 bytes per dimension; data at `+0x20`).
- **Passability table** — one `CheckOkList` per *distinct attribute*, not per cell. The foot
  ID space is small (`LandingGroup` master is `type1`…`type32`), so this is ~32 interop calls
  per vehicle instead of 65k.
- **Flood memoisation** — recomputed only when map, transport, or player cell changes. This
  matters because `FindPathTo` is called from the 2 s beacon loop and once per entity by the
  pathfinding filter.
- **Filtered entity view** — `EntityNavigator.FilteredCount`/`FilteredIndex` evaluate every
  OnCycle filter over every entity, so the result is cached against the position it was
  computed at. The player stands still while cycling, so one computation serves a whole
  cycling burst.
- **Chaining is opt-in** — `PathSearchMode.Quick` is the default so any call site added later
  is cheap by construction. Only the waypoint-pathfind key and the entity announce pass
  `Full`. Consequence: with the filter on `Quick`, a far target can be hidden by the
  pathfinding filter yet still routable with `/`.

### Planned: beacon follows the route, not the objective

**Status: designed, not implemented.** Blocked on in-game verification of the routing work
above — if long-range routing does not hold up, none of this applies.

#### Why the current beacon stops making sense

`BeaconLoop` (`Core/AudioLoopManager.cs:293`) is entirely straight-line to the final target:

| Cue | Derived from | Line |
|---|---|---|
| Pan (left/right) | `targetPos.x - playerPos.x` | `:423-424` |
| North/south tone | `targetPos.y < playerPos.y - 8f` | `:426` |
| Volume | straight-line distance | `:421` |
| Ping interval | straight-line distance | `:397-407` |
| Pitch | halved when no path found | `:384-408` |

That is defensible only while the pathfinder cannot see far enough to know better. Once it
can, a straight-line bearing is actively misleading: standing at the Wind Shrine, Tule may be
due north while the only walkable route runs east first. The beacon says "north" and walks
the player into the ocean — worse than no beacon, because it is confidently wrong.

#### Aim at the next turn, not the next breadcrumb

Breadcrumb *nodes* are the wrong target. They are hop seams at arbitrary ~24-cell spacing,
artifacts of how the search was budgeted, and one can land mid-corridor where nothing about
the player's movement changes.

The meaningful target is the **next turn**: the vertex ending leg 0 of `PathInfo.Legs`. That
is by definition the point where the required direction changes, which is exactly what a
directional cue should mark. Everything the beacon already computes then works unchanged,
just against that vertex instead of the objective:

- pan from `nextTurn.x - playerPos.x`
- north/south from `nextTurn.y` vs `playerPos.y`
- volume and interval from distance to the next turn (or route distance remaining — see
  below), not straight-line distance to the objective

**Prerequisite:** `RouteLeg` is currently `{ Direction, Steps }` with no position, so the turn
vertex cannot be located. `BuildLegs` (`Field/FieldNavigationHelper.cs`) must also carry each
leg's end vertex — either a `Vector3 EndPosition` on `RouteLeg`, or the index into
`PathInfo.WorldPath`. Small change, but nothing else can proceed without it.

#### The cadence problem, and why the route has to be cached

The beacon calls `FindPathTo` every tick, on `PathSearchMode.Quick`, and **must not** move to
`Full` on that cadence — chaining is hundreds of milliseconds and this runs at 0.5-5 Hz.

But Quick is exactly what fails in the long-range case this feature is for. So the beacon
cannot both re-search every tick and know the route.

Resolution: **search once, re-aim many times.** Run one `Full` search when the target is
chosen — `RestartBeacon` (`:121`) already fires on the pathfind key — cache the resulting
`WorldPath`/`Legs`, and per tick merely pick the nearest un-passed turn from the cache. No
search per tick at all, which is cheaper than today.

This reintroduces stored route state, which the speech path deliberately avoids. It therefore
needs explicit invalidation, and getting this list wrong is the likely source of bugs:

- target changed (`lastBeaconTarget` already tracks this, `:342`)
- map changed (hook alongside the existing `VehicleRouteSearcher.InvalidateAll()`)
- exit from event state (foot IDs can convert — same reason the grid drops)
- vehicle boarded or dismounted (traversability changed entirely)
- player displaced far from the cached path (teleport, or simply walking off it) — re-search
  rather than aiming at a stale turn

#### Retiring Mode B

The down-pitched "out of range" beacon exists because the game's searcher could not see past
~31.5 tiles, so "no path" usually meant "too far to tell". That reason is gone: a failed route
is now one of two *definite* answers — `RouteFailure.NoPathProved` (search space exhausted,
genuinely no route within the model) or `RouteFailure.StoppedEarly` (a budget or bound fired
first, so reachability is unknown).

Dead once this lands: `MODE_B_INTERVAL_FAR`, `MODE_B_INTERVAL_NEAR`, `MODE_B_FAR_TILES`,
`MODE_B_NEAR_TILES` (`:64-67`), the `lowPitch` local, and the `pathValid` branch (`:384-409`).
`SoundPlayer.PlayBeacon`'s `lowPitch` parameter (`Utils/SoundPlayer.cs:262`) has no other
caller and can lose the parameter with it.

**What replaces it is not nothing.** Genuinely unreachable targets still exist. Pinging low
forever at a town across the ocean is worse than saying so once and going quiet. Recommended:
on a `Full` route failure with `NoPathProved`, say "no route" **once**, then silence the
beacon, re-arming the announcement only on the invalidation events listed above.

Note what this must *not* say. There is no `DescribeFailure` and no `KnownTransports` — both
died with the "Requires Pirate Ship" removal (see "The vehicle-requirement check and why it
was removed"). Naming a required vehicle is an event-gating claim that terrain data cannot
support. Saying "no route" is a statement about the search; saying "requires the pirate ship"
is a statement about the story, and only one of those is knowable here.

One caveat to respect: a `Quick` failure inside the search window is still ambiguous, so the
speak-the-reason path must fire only on a `Full` result. Otherwise the beacon will announce
"No route" for targets that are merely momentarily blocked.

#### Distance cues should follow the route

Related, and worth doing at the same time: volume and interval currently scale with
straight-line distance, so a target 5 tiles away through a wall — 80 tiles by route — sounds
almost arrived. With a cached route, `PathInfo.StepCount` (or remaining steps from the
player's position) is the honest measure. `MODE_A_FAR_TILES` is 31.5 (`:61`), which is the old
window radius; if distances become route distances, that ceiling wants re-tuning too.

### Port checklist (FF1 / FF2 / FF3 / FF4)

Copy `Field/Routing/` wholesale, then:

1. Rename the namespace in all four files.
2. `RoutingAdapter` — re-derive every field. **There are no struct offsets to port**; the
   only one this code ever had went away with the vehicle-requirement check, and it should
   stay that way.
   - `TileSize` / `TileSizeInverse` (FF5 is 16; do not assume).
   - `IsWorldMap(mapId)` — FF5 uses ids 0/1/2. `RefreshMapInfo` warns once if the game's
     `CheckCurrentWorldMap()` disagrees, which is the cheapest way to find the right ids.
   - `WorldMapWrapsX/Y` — these read `MapModel.GetLoopType()`; only the fallback is a guess.
     Still verify by routing across the map seam.
   - `AllowsDiagonalMovement` — which `TransportationType` values move analog rather than on
     the movement grid. FF5: `Plane`/`LowFlying`/`SpecialPlane`. A game without a free-flying
     vehicle can return `false` unconditionally.
3. `PathInfo` — add `Legs`, `IsApproximate`, `ApproachOffset`, `IsLandingSpot`, `Failure`.
4. `FieldNavigationHelper` — route **every** `MapRouteSearcher.Search` call through a
   `SafeSearch`-style wrapper. This is not optional: `Search` throws outside its window, and
   an unwrapped call makes the whole chainer unreachable (see above).
5. Add the `mod_text.json` keys, **translated fresh for that game** (Rule 8 — never copy
   translation strings between FF mods): `No movement needed`, `and {0} more`,
   `target {0} {1}`, `landing spot`.
6. Verify with: `grep -nE '\bT\(|SpeakText|LocalizationHelper|MelonLogger|GameObjectCache'`
   over `Field/Routing/` — must be clean outside `RoutingAdapter.cs`.

### FF1 Parity Pass (2026-09-23)

Ported the FF1 features an audit found missing (work list in plan.md, bottom of the table).
Everything below builds clean; nothing was exercised in game.

**How the hooks were chosen.** `GameAssembly.dll.c` in this folder is a type header with no
function bodies, so it cannot answer "who calls X". The questions were settled by scanning
`GameAssembly.dll` itself (read-only) for direct `call`/`jmp rel32` sites and mapping each site
back to its method through dump.cs RVAs (capstone for the one-function disassembly). Virtual
calls are indirect and do not show up, so "no caller" means "no *direct* caller". Findings:

| Question | Answer |
|---|---|
| Who changes encounters? | `CheatSettingsClient.SetIsEnableEncount` ← `FieldMap.UpdatePlayerStatePlay` (the field toggle), `ConfigActualDetailsControllerBase.SetEnableEncount` (config), `SaveSlotManager.GotoLoadSaveData` (load) |
| Who changes auto-dash? | `ConfigClient.SetIsAutoDash` ← `FieldMap.UpdatePlayerStatePlay`, config `SetIsAutoDash` / `SwitchArrowSelectTypeProcess`. It tail-calls `Config.set_IsAutoDash` |
| Does the pause menu have a non-per-frame hook? | `BattlePauseController.SetCommandSelectCursor`: `SetCursorToDefault` (open), the first-show branch of `UpdateSelect`, and the cursor's move callback `<UpdateSelect>b__27_1`. **Not** `UpdateFocus` — `UpdateSelect` calls it every frame while no popup is open |
| Does a status LB/RB switch re-run `InitDisplay`? | Not directly: `SetNextPlayer` = `GetCorpsNextIndex` + `OnChange(index)`. The switch patch dedups against the tracker's character by pointer in case `OnChange` re-enters the display state |
| Does opening a target window read the first target? | Enemies yes (`EnemysInit` calls `SelectContent`); allies no (`PlayerInit` positions with `BattleCursorUtility.SetTargetPlayer`). Hence the `PlayerInit` postfix |
| Where do bestiary page turns go? | `LibraryInfoManager.NextPage/PreviousPage → ShowData → LibraryInfoController.SetData`, and `changeGroupMonster → SetData`. `ExtraLibraryInfo.OnChangedMonster` only sets the background. So `SetData` is the single announcer |
| Why was line-fade text silent? | `LineFadeMessageManager.Play` has no direct caller. The game's `LineFadeMessageClient.Play` goes through `AsyncPlay`, which builds the `<Play>d__9` coroutine itself. That coroutine's `MoveNext` is the only caller of `LineFadeMessageWindowController.SetData` — hook that |
| Equip LB/RB | `EquipmentInfoWindowController.SetNextPlayer/SetPrevPlayer`, called from the page-turn coroutines. **Not** `UpdateSwitchCharacter` — `CommandUpdate`/`InfoUpdate` run it every frame |
| Config row re-announce after the Library | `ConfigActualDetailsControllerBase.UpdateFocus` re-asserts `ConfigCommandController.SetFocus` every frame; clearing the SetFocus guard on bestiary exit is enough |

**Folded bodies found on the way (never hook):** `CheatSettingsData.set_IsEnableEncount`
(0x346D70, 23 methods), `ConfigSaveData.set_IsAutoDash` (0x2EB9C0, 20),
`ConfigKeysSettingController.MouseSettInit` (0x2715A0, the shared empty stub).

**Pointer reads added** (verified against dump.cs): `ShopController.stateMachine` 0x98 →
`StateMachine<T>.current` 0x10 → `State<T>.Tag` 0x10 (same generic layout FF1 reads);
`SaveContentController.SlotData` 0x38 → `SaveSlotData.id` 0x30 (ids 21/22 are the autosave
and quick-save rows — `SaveSlotManager.AutoSlotId`/`SuspendedSlotId`); `ChangeNamePopup.inputField`
0x38; `InputPopup.descriptionText` 0x30.

**Localization traps fixed:**
- `ModMenu.Initialize` ran `T()` at mod load, before `MessageManager` exists, so every mod-menu
  label was English in every language. Labels (and the new descriptions) are now keys translated
  when spoken. Same for `WaypointEntity.GetCategoryNames()`, cached in a static initializer. The
  key audit regex (`T("literal")`) does not see these raw keys — check `ModMenu.Initialize` and
  `GetCategoryNames` by hand when auditing.
- Battle action wording matched English command names ("attack"/"defend"/"item"). Now by command
  identity: `CommandSortData.CommandId` Fight = 1, Item = 3; `Command.CommandType` Defence = 8
  (Defend/Flee) minus `BattleConstants.EscapeCommandId` 22. "uses item" only when no ability name
  is available, so a named item still reads by name.
- Autosave/quick-save rows were recognised by English slot names; now by `SaveSlotData.id`.
- `DirectionHelper` returns translated words — it is speech-only; never compare its result to a literal.

**Open question — Evasion.** The status screen fills each row via
`ParameterUtility.GetValue(data, row.Type)` / `SetCountValue(GetValue(row.SubType))`, and
`IsPercent(type)` decides the "%". The row's `ParameterType` is prefab data, invisible in the
dump, so whether "Evasion" is `EvasionRate` (17, a percentage — what FF1 reads) or a count could
not be settled offline. `ReadEvasion` still uses `ConfirmedDefenseCount()`. One log line of
`ParameterContentController.Type`/`SubType` for the Evasion row would settle it.
*(Settled offline in the session-2 pass below: the prefab can be read from the bundles; it is
`EvasionRate`, shown with "%".)*

**Per-frame cost kept out:** `InputManager.IsOnValidMap` self-heals with `GameObjectCache.Refresh`
only for on-demand callers; `DetermineContext` (every frame) passes `refreshIfMissing: false`,
otherwise the title screen would run a `FindObjectOfType` per frame.

**Review fixes (2026-09-23, after an independent review of this pass):**
- Plain attack vs named ability: `isDirectAttack` was true for anything under the Fight command,
  which would read an ability run under Fight as "X attacks". Now: first ability id 1 (the Fight
  command's own ability — command master row 1, `ability_id` 1), or no ability and the Fight
  command. Named abilities keep their names.
- Battle command back-out: the settle read now keeps retrying (within the 6-frame cap) while
  targeting / item use is still latched from the sub-menu just left, instead of giving up silently.
- `LocationMessageTracker`: a map transition now suppresses at most one repeated banner, then is
  forgotten — a stale "Entering X" could otherwise swallow the banner after loading a save on the
  same map (where no "Entering" is spoken).
- Equip LB/RB: "queue the next slot read behind the character name" is a frame stamp (10 frames)
  instead of a flag, so a superseded follow-up read can't leave a later unrelated read queued.
- V key: gimmick / unique movement states read "Special movement" (new key) instead of "Unknown".
- Shop command bar: `InitSelectCommand` reaches `SetCursor` twice in one frame
  (`ShopInfoController.Reset` and `SetCommandFocus`); a same-index same-frame guard speaks it once.

*(Removed in Round 2, 2026-09-24, by user decision: see the correction below and "Round 2 (2026-09-24)",
Task D. The paragraph is kept as history.)*

**Multi-hit damage (2026-09-23).** "Target: NxTotal damage" on weapon attacks now works in FF5. It
never could before: the game draws its ×N (`BattleBasicFunction.CreateHitCount` →
`DamageViewUIManager.CreateHitCount`) only when `SystemConfigData.GetBattleType()` is Command, and
FF5's returns 0 (ATB; FF1–FF3 return 1), so the `CreateHitCount` hook never fired. The
`CreateDamageView` postfix now reads the attack's own count from
`__instance.ICalcResultDic[target].GetHitCount()` for weapon abilities (`Ability.TypeId` 4 — the Fight
command's ability 1; the same rule the ×N display uses). `battleActData` is protected, read at offset
0x28. Default is now "With hit count", stored as `MultiHitDamage`. *Unverified in game:* that
`GetHitCount` is hits landed (FF5 basic attacks are usually single-hit, so most attacks read as before).

**Correction (2026-09-23, session 2, capstone): FF5's calc result never carries a hit count, so the
fallback above always returns 1 and FF5 reads the total only.** `CalcControllerProvider.GetFightStatus`
(0x3C9D40) is a stub. It logs "このFunctionは使われない想定です" and returns (0, Miss, 0), and its
only caller is `FuncitonNormalAttack.Calc` (0x6214B0). Real attacks go through `GetUniqueStatus`
(0x3CA940) → `ClacExecuteFF5.MainExecution` (0xAC3C20). Its `SetStatus` at 0x3CADCC passes hit count 0
explicitly (`xor eax,eax; mov [rsp+0x28],eax`), and so do the early-out at 0x3CB695 and
`UniqueFunction.Calc`'s re-set at 0x76B143.

Every one of the 63 `SetStatus` call sites passes 0, except the add-condition ones. `MainExecution`
does compute a value it logs as "攻撃回数:" (attack count), but it is `SpCalc`'s (0xAC94F0) sixth
tuple element, which looks like a damage multiplier, and it is dropped anyway.

The Multi-hit Damage setting therefore has no audible effect in FF5; FF4 has the same finding. The
count only exists inside the calc functions, which return tuple structs. Hooking them would mean a
struct-return postfix under Il2CppInterop, which needs a crash test. The other options are to hide
the setting in FF5, or to leave it (harmless). This is left to the user. *(The user chose removal;
done in Round 2, 2026-09-24.)*

### Open-issues pass (2026-09-23, session 2)

This pass closes the FF5 items in `OPEN_ISSUES.md`. It builds clean (0 warnings, 0 errors), but
nothing was tested in game. Every answer below was settled offline:
- UnityPy on the Addressables bundles;
- capstone on `GameAssembly.dll`, with RVAs mapped through dump.cs;
- the read-only `tools\hitscan.py` / `tools\callees.py`.

The scratch scripts are not part of the repo.

**Prefab data is readable offline.** An IL2CPP MonoBehaviour has no type tree, but its raw bytes are
simple to read:
- the header: `m_GameObject` PPtr (12 bytes), `m_Enabled` (4, aligned), `m_Script` PPtr (12) and
  `m_Name` (a length-prefixed string, 4-aligned);
- then the serialized fields, in dump.cs order.

Match `m_Script` to the bundle's MonoScript (namespace plus class name). Walk `RectTransform.m_Father`
to get each object's path. Any "prefab data, invisible in the dump" question can be settled this way.

**Evasion.** `key_menu_assets_all` →
`status_details/parameters/status_parameters_content`: eight `Last.UI.KeyInput.ParameterContentController`
rows. Each has `type` at 0x18 and `subType` at 0x1C, and every `subType` is 0:

| Row | `type` |
|---|---|
| Strength | Power (4) |
| Agility | Agility (6) |
| Stamina | Vitality (5) |
| Magic | Magic (14) |
| Attack | Attack (10) |
| Defense | Defense (11) |
| **Evasion** | **EvasionRate (17)** |
| Magic Defense | AbilityDefense (12) |

The KeyInput `StatusDetailsControllerBase.SetParameter` (0x524F00) fills each row as follows:
- label: `GetMessageByMessageConclusion(ParameterUtility.GetMessageId(type))`;
- value: `SetData(label, GetValue(data, type, false))`;
- count: `SetCountValue(GetValue(subType))`, hidden when `subType` is 0;
- "%": `SetEnablePercentText(IsPercent(type))`.

The engine code behind it:
- `GetValue` (0x497310) is a jump table. Case 17 calls `Parameter` vtable 0x280, which is slot 21,
  `ConfirmedEvasionRate(isNatunalValue)`. The vtable base is 0x130, 16 bytes per slot.
- The other seven cases map to the readers the mod already used (slots 7, 8, 9, 13, 15, 16, 18).
- `IsPercent` (0x497B10) cases:
  - 16, 18 and 19: `true`;
  - 13 and 17: `SystemConfig.IsVisibleEvasionRatePercent()`, which reaches
    `SystemConfigDataBase` slot 49. That is the shared `return true` stub (0x2EAD10), and FF5's
    `SystemConfigData` does not override it;
  - 12: `IsVisibleAbilityDefencePercent`, slot 37, `return false`.

So `ReadEvasion` now speaks `ConfirmedEvasionRate(false)` + "%", the same as FF1.

**MenuTextDiscovery English checks.**
- "Battle Type" does not exist in FF5. It is in no message table (`system_en` has "Battle Mode",
  `MSG_CFG_INF_135`) and in no `key_*`, `touch_*` or `ui_*` bundle, so the skip could never match and
  was removed.
- The last-resort value filter used `^On$|^Off$|^Active$|^Wait$`. It now compares against
  `MessageManager.GetMessage` of `MSG_CFG_INF_43/44/138/139` (On/Off/Wait/Active in `system_en`),
  resolved on each call so it follows the current language. The `^\d+%?$` number check stays.

**Save-list slot id (Touch).** Only the KeyInput `SaveListController` is instantiated by the PC
bundles:
- `key_loadgame`: the title Load list;
- `key_menu`: the field menu lists, 2 instances;
- `key_savewindow_ui`: the save window.

The Touch controller appears only in `touch_savewindow_ui`. So the title list was already read
through the KeyInput `SlotData` at 0x38. The Touch path had skipped the id check and assumed
"numbered". It now reads the Touch `<SlotData>k__BackingField` at 0x50 (dump.cs 434378), then
`SaveSlotData.id` at 0x30.

**Battle I key: the list's real state.** Both lists run a `StateMachine<State>`:
- KeyInput `BattleItemInfomationController.stateMachine` is at 0x58;
- `Serial.Template.UI.KeyInput.BattleAbilityInfomationControllerBase.stateMachine` is at 0x30;
- then `StateMachine<T>.current` at 0x10, confirmed by `get_Current` 0x10BA260 reading
  `[rcx+0x10]`, and `State<T>.Tag` at 0x10.

In both enums 0 is `None`. `Close()` (item 0x3FBD60, ability 0x6C3140) changes to `None`. The item
list's `NonInit` (0x3FD860) is `SafeActiveSet(view.rootObject, false)`: it hides `view` (0x28) →
`rootObject` (0x18), but not the controller's own GameObject. That is why the old check
(controller `activeInHierarchy`) could pass after the list closed. `BattleListDetails.TryAnnounce`
now also requires `Tag != None` and a visible `view.rootObject`. Both are pointer reads, done on the
key press only.

**Encounter toggle on a load: not a bug.** `SaveSlotManager.<GotoLoadSaveData>d__50.MoveNext`
(0x4F41A0) calls
`CheatSettingsClient.SetIsEnableEncount(UserDataManager.Instance().CheatSettingsData.isEnableEncount)`.
It runs after `FromJsonAsync`, so it re-applies the value just loaded. `SetIsEnableEncount`
(0x54EEA0) writes that same field: `SaveMapManager.SetEncountEnable`, then `UserDataManager`+0xA8
(`<CheatSettingsData>`) → +0x10 (`isEnableEncount`). The prefix's old value therefore always equals
the new value on that path, and it returns before speaking, whatever `IsFieldToggle` says. The only
change is a comment in `GameTogglePatches`.

**First ally target index.**
- `PlayerUpdate` (0x44DFC0) rewrites `playerDataList` (0x30) every frame to a new
  `GetSelectedPlayerTarget<BattlePlayerData>(TargetPlayerList)` (0xBC69C0).
- The navigation lambdas `<PlayerUpdate>b__1..3` (one body, 0x948170) call
  `SelectContent(closure.list, index)` with that list.
- `GetPlayerTarget()` (0x44BE80) returns the fresh filtered list's element at `selectCursor.Index`
  (0xD8).
- `PlayerInit` does not rewrite `playerDataList`, so right after it the field can still hold the
  previous targeting's list.

The postfix searched that list by pointer and used whatever position it found. That position could
differ from the cursor index the next `SelectContent` passes, and then a real move would be
swallowed by the `(mode, index)` guard. The postfix now uses `selectCursor.Index`, and it announces
only once `playerDataList[cursorIndex]` is the focused ally. Until then it returns false, and
`MenuFocusAnnouncer` retries within its 6-frame cap.

**Value-0 battle events.** First, what the mod did before this pass:
- It spoke every `CreateDamageView` except Miss + `NonView`.
- A 2026-07-26 log has `"Faris: 0 damage"` right before `"Faris: Paralyze"` (Entangle), and
  `"Wing Raptor: 0 damage"` after its own actions.
- Status removal is announced nowhere: only `BattleConditionController.Add` is hooked.

The key finding: **a Harmony postfix runs even when the original returns early**, and
`BattleBasicFunction.CreateDamageView` (0x889870) draws nothing in several cases. Decoded:
- `value == 0` and `hitType` is `Hit` (0) or `Non` (-1): return, no view.
- `hitType == RecoveryCondition` (7): a view only when
  `ProviderManager.SystemConfigData.GetSerialType()` (vslot 5) is 5 and `value > 0`, and then it is
  drawn as recovery. FF5's `GetSerialType` (0x2B2720) is `mov eax, 5`, so the condition is simply
  `value > 0`.
- `missType == NonView` (2): return, for any hitType.
- Otherwise:
  - `isRecovery` is forced true for `Recovery` (4) and `MPRecovery` (6);
  - it is flipped when `value < 0`;
  - `BattleUtility.CreateDamageView(data, value, isRec, IsMiss(data))` draws the view. The base
    `IsMiss` is `GetHitType() == Miss`.

`CreateViewEntity` (0x889D40) takes `hitType`, `value` and `missType` from `ICalcResult` slots 5, 3
and 13. No `CalcResult.SetStatus` call site passes a constant `Zero` or `RecoveryCondition` (a byte
scan for `mov r8d, 3/7` before 0x3CC810/0x3CC9F0 found none). The types come from the per-function
calc code, for example `RecoveryConditionFunction.Calc` (0x626F20), and were not traced further.

The postfix now mirrors those rules:
- no speech for an undrawn view;
- `RecoveryCondition` reads as "Recovered N HP";
- `Zero` (drawn as "0") still reads "X: 0 damage".

**Still open:** whether a pure status cure is `RecoveryCondition` with value 0, which is undrawn and
therefore silent, and whether any buff carries it. So no "{0}: cured" key was added. Each value-0
view logs one `[Battle] value-0 view: hitType=N isRecovery=B target=X` line, so one Antidote plus
one buff in game settles it.

**Vehicle names.** `VehicleEntity.GetDisplayName` upper-cases the first letter of the fallback key.
This is display only: `GetVehicleName` and the "On {0}" movement speech keep the lowercase keys.

**Controller state sync (the FF1 findings), event-driven with no per-frame check.**
- `GamepadManager.HandleGamepadRemoved` → `ControllerRouter.OnGamepadRemoved()` drops ModMode.
  `Update()` returns early without a gamepad, so mod mode used to stick, and `SuppressGameInput`
  with it.
- `ModMenu.Open` → `OnModMenuOpened()` sets State = ModMenu. An F8-opened menu left the router in
  Normal, so the D-pad cycled waypoints, the right stick cycled entities and LT pathfound underneath
  the menu.
- `ModMenu.Close` → `OnModMenuClosed()` returns ModMenu to Normal. A keyboard close with no
  controller used to leave State = ModMenu.
- FF1's third finding (Tab clears the battle flag mid-battle) does not apply: FF5 binds no such key.

**Official-name substring pass (2026-09-23).** The first alignment pass (`tools/official_fix.py`) only fixed entries whose Japanese key *exactly* matched a string in the game's own message tables. This pass also fixed keys that *contain* an official proper noun: characters, places, key items, vehicles, monsters, weapons and jobs. Where a value rendered the noun differently, only that noun was replaced with the game's form for that language, taken from `tools/extract_entities.py gamedict` (FF5's own tables only). It also covered:
- FF5 keys made of an event/map prefix plus an exact official name, e.g. `13:ウォルスの隕石`, `ev_e_0097:クルル` (44 keys);
- name stems that several of the game's official strings share: Tycoon 泰空 / Тайкун / ไทคูน; Walse → fr Wolse, it Walz, ru Вольс; Karnak 卡納克; Ghido; Xezat; Korean 크리스탈 → 크리스털;
- further examples: Gido's Shrine → Ghido's Cave, Wyvern → Wind Drake, Rix → Lix, Firebrand → Fire Lash.

Totals: 385 entries / 1,625 values, applied identically to `translation.json` and `translation.generated.json` (still byte-identical). Russian and German case forms of an official stem were kept.

Left for review:
- 飛竜 in Spanish (26 entries, "Viverna…"). The official form is ambiguous: "Guiverno" in the system table, "Dragón" as a speaker name.
- German Moogle labels now use masculine articles ("Den Mogry entdecken"); a native speaker should check them.

The rules, the full old→new list and the skipped items are in `D:\Games\Dev\Unity\FFPR\tools\official_substring\`. Values were edited in place, so the diff shows only the changed value lines.

### Round 2 (2026-09-24)

Four user-approved tasks. Built clean (0 warnings, 0 errors); `modtext_check` and `utf8_scan` clean.
Nothing was tested in game. Every hook below was checked for a unique `// RVA:` in dump.cs (count 1)
and for callers with `tools\hitscan.py` / capstone.

#### Task A: status removal ("{0}: {1} removed")

**FF5's `Remove` is not the right hook.** `BattleConditionController.Remove(unit, id, isNegate)`
(0x333410) has three direct callers only: the two `InterruptRemoveCondition` overloads and
`BattleEndRecoveryCondition`. The `InterruptRemoveCondition` callers are `BattleProgressATB.UpdateAlwaysEscape`,
`ActSelectSpSwordSky.ActSelectReceivedDamage` and `RampageConditionFunction.Start`, so cures and
wear-off never reach `Remove`. Decoded `Remove`: `condition = conditionDic[id]`; if
`Parameter.CurrentConditionList` (unit 0x28 → info 0x10 → 0x88) contains it, remove it (all entries
with that id when `isNegate`); then tail-call `RemoveFunction(unit, id)`. All three call sites pass
`isNegate = false` (`xor r9d, r9d`), so FF5 has no negation path.

Every other removal takes the condition out of `CurrentConditionList` directly:
- the action's result (cures, revive);
- `BattleConditionFunction.NaturalRemove` (0x336060; timed wear-off from `UpdateCondtitonRecovery`):
  `NaturalRecovery()` (vtable slot 16), then `CurrentConditionList.Remove(condition)`;
- `BattleConditionController.Recovery(unit, untilType)` (0x332DC0): the same, for each function whose
  condition has that until-type;
- `Cancellation` (0x32FC50; via `ConflictCondition` when a new condition conflicts, including KO).

The sync then runs: `CheckAddCondition()` (every frame from `BattlePlayController.UpdateStatusInPlayController`,
and from `BattleActExection.InitFinishingState` after each action) → `CheckConditionFunction`
(0x3306D0): `ConflictCondition` for new ids, then `RemoveConditionFunction` (0x332F80), which calls
`RemoveFunction` for every id whose function count exceeds its condition count, then
`AddConditionFunction` → `Add`.

**Hook: `RemoveFunction(BattleUnitData, int id)` (0x333260).** Callers: `RemoveConditionFunction`
and `Remove` (tail call). It finds the unit's `BattleConditionFunction` whose `condition.Id == id`,
calls its `Remove()` (slot 17; the only slot-17 call in the condition classes), and takes it out of
`BattleUnitDataInfo.BattleConditionFunction` (0x28). With no function it returns without doing
anything. It is the mirror of `Add` (0x32F5E0), which creates that function and which the add
announcement hooks. It runs only when a function is removed, never per frame.

`BattleConditionController_RemoveFunction_Patch`:
- **Prefix:** finds the function's `Condition` for the id (none → silent: nothing is removed and no
  add was ever announced); names it with `GetConditionName` (shared with the add patch: `MesIdName`
  → `MessageManager.GetMessage`; empty or `"None"` → silent); silent if the unit is KO
  (`ConditionType` 5) or Stone (11) through a condition other than the one leaving. That last rule is
  what silences death clearing statuses: KO is already in `CurrentConditionList` when `Cancellation`
  and the sync remove the rest. A revive removes KO itself, so it speaks "X: KO removed".
- **Postfix:** speaks only if neither the condition nor a function with that id remains (a second
  Image stack stays silent until the last one goes), once per (unit, id) per frame, then calls
  `BattleConditionController_Add_Patch.Forget` so a re-application in the same turn is announced again.
- **Battle end:** a latch in the patch, set by a prefix on `BattleEndRecoveryCondition` (0x32F890;
  only caller `BattleController.SaveRecoveryCondition`, called from `StartWinResult`,
  `EndEscapeFadeOut`, `StartForcedOnSave` and `<StartBattle>b__29_0`) and by a prefix on
  `BattleController.StateChange` (0x341570) entering WinWait (7) … End (20). StateChange into
  Init (1) … Event (6) clears it (every battle starts with `StateChange(Init)` from `StartBattle`), and
  so does `BattleState.SetActive`. The two sources are both needed: `StartForcedOnSave` and
  `<StartBattle>b__29_0` call `SaveRecoveryCondition` before their `StateChange`.

**Names.** FF5's condition master gives Poison, Blind, Stone, Toad, Mini and Float `mes_id_name`
"None" (read from `master_assets_all`, `condition`; also one Doom row, id 16). Follow-up (user
decision: cures must be announced): `GetConditionName` falls back to the localized status name by
`ConditionType` (`CharacterStatusHelper.GetConditionTypeName`) for both the add and the removal line.
- The fallback dictionary values are now mod_text keys spoken through `T()` (24 new keys, all 12
  languages). Their translations are the game's own words for each status, from the `system`
  message table: MSG_SYSTEM_114 Darkness, 115 Silence, 116 Old, 117 Mini, 118 Toad, 119 Petrify,
  121 Berserk, 122 Confuse, 123 Sleep, 124 Paralyze, 125 Slow, 126 Stop, 299 KO, 334 Poison, 415
  Zombie, and the spell names MSG_MAGIC_NAME_23 Protect, 28 Blink, 29 Shell, 32 Reflect, 57 Regen, 59
  Haste, 60 Float, 205 Doom. "Critical" (Dying) is the only hand translation. The target reader uses
  the same `T()` names now; it spoke the English literals in every language before.
- The dictionary was keyed wrongly for two statuses: Haste is `Heist` (15), not 18 (`Brave`), and
  Protect is `Proteus` (19), not 107 (`Protectra`). Neither 18 nor 107 occurs in FF5's condition
  table, so the target reader never listed Haste or Protect; it does now. Types absent from FF5's
  table (13, 403, 405, 406) are dropped.
- Types 4 (Dying, "Critical": an HP threshold) and 5 (the nameless KO row 75, which only sits in
  condition group 900 beside the named KO row 5) get no fallback on the add/remove lines, so there is
  no "X: Critical" at low HP and no second "X: KO".
- The KO/Stone death filter is unchanged: Stone is type 11 (rows 11 and 98).
- Visible change: floating enemies start battle with Float (condition 69, monster initial condition
  607), so the add line now says "Enemy: Float" at the start of such battles, as it already did for
  named start statuses (Reflect, Haste...).

**Clean-up.** The `[Battle] value-0 view:` log line is removed. FF5 never had a "{0}: cured" key.
"0 damage" for `HitType.Zero` is unchanged.

#### Task D: Multi-hit Damage removed

User decision, after the correction above: FF5's calc results never carry a hit count. Removed the mod
menu item and its description, the `MultiHitDamage` preference, the
`DamageViewUIManager.CreateHitCount` capture, `ReadWeaponHitCount` and the "NxM" branch, and the
mod_text keys "Multi-hit Damage", "Total only", "With hit count" and the description. Damage always
reads "{0}: {1} damage". An existing `MultiHitDamage` line in MelonPreferences.cfg is ignored.

#### Task B: per-frame and polling sweep

Converted:
- **Bestiary minimap** (`BestiaryPatches`): the `LibraryMenuController.UpdateController` postfix (every
  frame) compared `selectState` / `selectMapIndex` with the last frame. Now:
  - `LibraryMenuController.ChangeState(State)` (KeyInput, 0x993E70), the only writer of `selectState`
    (0x44), for open/close. Callers: `Show`, `<InitSetup>b__12_0`, `<UpdateMonsterList>b__17_0` (open)
    and `<UpdateEnlargedMap>b__18_0` (close). It returns early on the same state; the patch compares
    with its own open flag, reset with the bestiary state as the old baseline was.
  - KeyInput `LibraryMenuHabitatController.OnContentSelected(int index, MonsterData)` (0x996ED0) for
    the map change: `<UpdateEnlargedMap>b__18_0` calls it with the new `selectMapIndex` (0x50) right
    after left/right. Its other caller, the list's `OnContentSelected`, is filtered by the open flag.
- **Bestiary formation:** a 3 s `Time.deltaTime` loop calling `FindObjectOfType<ArBattleTopController>`
  every frame until `monsterPartyList` (0xC0) filled. Now `ArBattleTopController.SetActive(bool)`
  (0x3DE3F0; callers: `ExtraArBattleTopUi` StateInit/StateExit). `SetActive(true)` builds the list
  itself (`InitMonsterPartyList` 0x3DC140, its only caller), so the postfix reads it directly. It is
  gated on the bestiary as a whole, because StateInit can run inside the scene manager's `ChangeState`
  before the formation flag is set. `ChangeMonsterParty` (0x3DBAC0) writes `selectMonsterPartyIndex`
  (0xD0) synchronously, so its postfix reads `__instance` at once (it was one frame later, through
  `FindObjectOfType`).
- **Gallery and Music Player entry:** 2 s `Time.deltaTime` polls for the cached focused row. Now
  `TryAnnounceEntry` speaks the title once, then the row queued behind it, as soon as it is readable.
  It runs from `MenuFocusAnnouncer` (frame-bounded, first try one frame after `ChangeState(1)`, where
  the title used to be spoken) and from `SetFocusContent` / `SetFocus` while the entry is pending. If
  the 6-frame settle gives up, the next focus change completes it. The Music Player's arrangement toggle
  shares the suppression flag, so the entry has its own `EntryPending` flag.
- **Save/load popups and the game-over Load popup** (`SaveLoadPatches`, `PopupPatches`,
  `CursorNavigationPatches`). Follow-up, same pattern as FF4.
  - Were: a `SavePopup.UpdateCommand` postfix (every frame while a save/load/quick-save popup is up;
    its first call after a reset did the open read) and a `GameOverLoadPopup.UpdateCommand` postfix.
    The latter has no direct caller (`GameOverPopupController.UpdateSaveLoadPopup` calls `UpdateSelect`
    directly), so it may never have fired.
  - Not `SetCommandSelectCursor` either: `UpdateSelect` calls it every frame while the first command
    is hidden (a single-button popup: the cursor is forced to 1).
  - Open: each controller's own open hook reads the popup two frames later (title + message, then the
    focused button, queued) and registers it:
    - `LoadGameWindowController.SetPopupActive(true)` (title Load), popup at 0x58;
    - `LoadWindowController` / `SaveWindowController.SetPopupActive(true)` (field Load / Save), 0x28;
    - `InterruptionWindowController.SetEnablePopup(true)` (quick-save confirmation), 0x38;
    - `InterruptionWindowController.InitComplite` (0x802830, quick-save completion: same SavePopup,
      no `ResetCursor`, single "Close" button), 0x38;
    - new: `Save.KeyInput.SaveWindowController.OverwriteConfirmInit` (0x8411B0, the overwrite
      confirmation), view 0x30 → savePopup 0x28. It was only ever read through the per-frame hook.
    - game over: `GameOverPopupController.InitSaveLoadPopup`, view 0x30 → loadPopup 0x18.
  - Focused button on open: `FocusedSaveStyleIndex` applies the game's single-button rule (first
    command hidden → index 1).
  - Moves: `SavePopup` / `GameOverLoadPopup.<UpdateSelect>b__32_0` (0x7E1DB0 / 0x7F6D40) move
    `selectCursor` (0x58) only through `Cursor.NextIndex` / `PrevIndex`. `NextIndex` (0x582F80) writes
    the index (0x18) before invoking the move callback. So the cursor patches call
    `TryReadSavePopupMove` / `TryReadGameOverLoadMove` first, matched by the open popup's cursor
    pointer, with the same index guard.
  - Close: `SetPopupActive(false)`, `SetEnablePopup(false)`, `Close`, and the window `SetActive(false)`
    forget the popup.
- **Mod dialogs** (`ConfirmationDialog`, `TextInputWindow`, `SpeakTextDelayed`): `WaitForSeconds`
  0.1 s / 0.3 s / 0.3 s. These dated from the real-window dialogs, which had to wait for NVDA's focus
  announcement; the dialogs are virtual now. The prompt is spoken at once. The echo ("Yes", "Confirmed:
  X") interrupts, and the callback's result is queued behind it (`SpeakTextQueued`). Escape no longer
  echoes "Cancelled" when the callback says its own cancellation: that was "Cancelled" twice for a
  waypoint delete, and "Cancelled, Rename cancelled" for a text input.

- **Config menu rows** (`ConfigMenuPatches`). Follow-up, same pattern as FF4. The
  `ConfigCommandController.SetFocus` postfix ran every frame:
  `ConfigActualDetailsControllerBase.UpdateController` (0x838F70) → `UpdateFocus` (0x8395B0) →
  `SetFocus` on every row.
  - Now `ConfigActualDetailsControllerBase.SelectCommand` (KeyInput, private, 0x82EA70, unique). It
    stores `SelectedCommand` (0x20). Callers: `Initialize`, `ResetCursor`, `SetDefaultSelect`, the
    mouse lambda and the up/down callbacks. If the row is not on screen yet, one retry a frame later.
  - The re-announce the per-frame re-assertion used to give comes from explicit events:
    - list focus: prefixes on KeyInput `ConfigController.InitializeSelect` (0x4B8050) and
      `InitializeGameBoosterSetting` (0x4B7F00) clear the row guard, and their `SetDefaultSelect` →
      `SelectCommand` reads the row (open and return from a sub-screen);
    - title Options: prefixes on `OptionController.InitConfig` / `InitSelectLanguage` /
      `InitSelectScreenSetting` / `InitSelectSoundSettings` (0x8619F0 / 0x8629F0 / 0x863130 /
      0x863B50) clear it, and their postfixes read the shown list's row (settle);
    - popup close: `PopupPatches.PopupClose_Postfix` → `AnnounceFocusedRow` (config menu only);
    - Library exit: `ConfigBestiaryStateHandler.HandleExit` → `AnnounceFocusedRow`.
    `AnnounceFocusedRow` is a frame-bounded settle that stands down if a focus event already read a row.
  - Suppressed: `OptionController.SetActive` (0x865000) and `ConfigController.InitializeNone`
    (0x4B7F90) call `ResetCursor` → `SelectCommand` on lists that are not shown (every Options page;
    the first row on the way out), so a prefix/postfix pair sets `SuppressReads` around them.
  - The row guard stays on the text: the list scrolls, so a row controller can be reused.
- **Config slider values**. The old `SwitchSliderTypeProcess` postfix ran every frame: the tail of
  `UpdateController` (0x83959F) calls `SwitchSliderTypeProcess(SelectedCommand, key None)` every frame
  while a slider row is focused. Every value-writing method runs on that path every frame too:
  `SetSliderValue` (0x4B5FA0) → `Slider.set_value`, and `ConfigClient.SetVolume` / `SetBrightness`. So
  no game method signals a change by itself. Unity's `Slider.onValueChanged` fires only when the value
  really changes, from the left/right path in the input lambda `<UpdateController>b__0`.
  `ConfigSliderValueListener` adds one listener per slider when its row first gains focus, and reads
  the focused row's value one frame later (`SetSliderValue` writes the text after the value). The old
  postfix compared Il2Cpp wrappers with `ReferenceEquals`, which is never true across calls, so it
  probably never spoke a value.
- **Config arrow values** (`SwitchArrowSelectTypeProcess`, 0x8363B0): not per-frame. Its only
  callers are the input lambda (on left/right) and an override, so the existing postfix stays.

Kept, with the reason:
- `Timer.Update` (`TimerPatches`): patched only while the player's timer freeze (Shift+T) is on; it
  is the freeze.
- `InputSystemManager.GetKeyDown/GetKey/GetKeyUp/GetAnyKey` (`InputPassthroughPatches`): the
  controller input core.
- EXP counter loop (`BattleResultPatches.MonitorExpCounterAnimation`, `WaitForSeconds(0.1)`): an
  allowed audio loop. The wait is the tick that keeps the SDL Counter stream fed, not a speech delay.
- Audio loops (`AudioLoopManager`), footsteps (`OnUpdate` → `MovementSoundPatches`), map transitions
  (`GameStatePatches`), and `InputManager.Update` / `DetermineContext` / `ControllerRouter.Update`:
  the allowed core.
- `MenuFocusAnnouncer` and the fixed 1–3 frame deferrals started by an event: the content is filled
  after the hooked method returns, and no later event marks it ready.
- The Touch config patches (`ConfigActualDetailsTouch_*`): the Touch config UI is never
  instantiated on PC, so they never run. Left as they were.

Also in the follow-up: `NamingPatches` spoke "Name: {characterName}" as an English literal; it now
goes through `T("Name: {0}")` (new key, 12 languages).

Checked and not per-frame, although an `Update*` method is among the callers: `MapUIManager.SwitchLandable`
(per tile, from `UpdateStateSwitchLandable` ← `ChangeTransportation` / `OnScriptFinished` /
`OnPlayerFootMonitoringFinished`), `ExtraLibraryField.NextMap/PreviousMap` (input branches),
`BattlePauseController.SetCommandSelectCursor`, `BattleCommandSelectController.SetCommandData`
(`TargetSelectUpdate` cancel branch), `CheatSettingsClient.SetIsEnableEncount` / `ConfigClient.SetIsAutoDash`
(the toggle key), `OptionController.SetActive` (exit branch), `BattleTargetSelectController.SelectContent`
(move callbacks and the roulette animations), `MainMenuController.Close` (cancel branches).
`FieldPlayer.ChangeMoveState` is called repeatedly only by the sliding-floor gimmick, and the patch
returns on an unchanged state.

#### Task C: double-fix audit of 27daf0e and bfca299

- First ally target (`PlayerInit` settle read) against ally `SelectContent`: a single `(mode, index)`
  guard. `ShowWindow` resets it and runs before `PlayerInit`; allies have no `SelectContent` on open.
  No double.
- Battle back-out (`SetCommandData` → `RequestFocusedRead`) against `SetCursor`: the same index guard;
  whichever speaks first wins. No double.
- Battle I key gate: key-driven only. No overlap.
- Damage-view draw rules against the new removal lines: different facts. A revive reads "X: Recovered
  N HP" (the view) and "X: KO removed" (the condition), as the spec asks.
- F1/F3 from the game's setters against movement speech: walk↔dash changes are not spoken by
  `MoveStateHelper`. No double.
- Title "Press any button", line-fade text, location banner: one path each.
- Equip and status LB/RB: guarded (slot index guard; status character pointer).
- Controller sync: `ControllerRouter.OpenModMenu` / `CloseModMenu` still set `State` before
  `ModMenu.Open` / `Close`, which now set it themselves, and `HandleModMenuState` still self-heals
  when the menu is closed. These are redundant but silent, and kept as defensive state writes (they
  cover `Open`/`Close` returning early).
- Found in the sweep (older than both commits): Escape in the waypoint delete dialog said "Cancelled"
  twice. Fixed in Task B.
