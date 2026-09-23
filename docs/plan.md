# FF5 Screen Reader Mod — Project Plan

## Overview
Accessibility mod for FF5 Pixel Remaster. MelonLoader + Harmony patches hook Il2CPP game code, output via Tolk to NVDA.

## Features

**Menus**: Cursor navigation, item/equipment/job/ability/config/shop/save slot/title menus. I key for details (job descriptions, item equip compatibility). Mutual exclusion between menu trackers.

**Battle**: Turn order, command/target selection, damage/heal/status/defeat messages, per-phase results (EXP/Gil/ABP, level-up stats, abilities, items), steal results, MissType.NonView suppression, battle action object dedup. Optional A/B/C letters on same-named targets. Results ABP column appears only when it applies (not for Freelancer/mastered jobs).

**Navigation**: Entity cycling ([ ] or J/L), categories (Shift+[ ] or Shift+J/L, - =; Shift+K back to All), pathfinding filter (Shift+\ / Shift+P), layer filter (Ctrl+\ / Ctrl+P), exit grouping (Shift+M), backtick rescan, wall collision sound, waypoint system (add/rename/remove/cycle/pathfind). Long-range routing: a mod-owned terrain-attribute search while riding, and breadcrumb-chained searches on foot, both lifting the game's ~31.5-tile search window. Unreachable targets report only what is verified — no route — since terrain data cannot distinguish a vehicle-gated destination from an event-gated one. Entity counts reflect the active filter ("1 of 4", not "1 of 20").

**Audio**: ModMenu (F8) with toggles/volume sliders/enum selectors. Wall tones, footsteps, audio beacons, landing pings. 16-bit audio, LRU tone cache, volume caching.

**Vehicles**: Movement state announcements (on foot/ship/airship/chocobo/submarine), landing zone detection via terrain attributes + CheckLandingList/OkList, vehicle entity tracking on world map.

**Other**: Dialogue/message auto-read, repeat current dialogue page (R key, or mod + Square on controller), timer (T key), F1 walk/run and F3 encounters (narrated from the game's own setters, any input source), F5 enemy HP display, delayed dialog announcements (0.3s for NVDA focus), speech redundancy fixes, naming popup enhancements.

**Bestiary (Picture Book)**: Full screen reader support for the enemy encyclopedia. Works from both extras menu (title screen) and config menu (in-game). List navigation with entry number/name, detail view with navigable stat buffer (arrow keys, Shift for group jump, Ctrl for top/bottom), formation announcements, map/habitat name reading, page turn support, monster switching in detail view. Shift+I reads control tooltips. Minimap open/close/cycle with habitat names. Full map open/close/cycle with habitat names. Items read from master data (UI uses icons only). Config menu path supports list, detail, page turns, and monster switching (no map/formation views).

**Music Player (Extra Sound)**: Screen reader support for the music player extras screen. Song list navigation with track number, name, and duration. Play All toggle (on/off) and Arrangement/Original toggle announcements. Automatic first-song announcement on entry. State cleanup on exit.

**Gallery (Extra Gallery)**: Screen reader support for the image gallery extras screen. List navigation with item number and name. "Image open" announcement on detail view. Automatic item re-announcement when returning from detail view. State cleanup on exit.

**Initial focus**: Every menu announces its already-focused row on open and on return from a sub-menu. State-entry `*Init` hooks feed `MenuFocusAnnouncer`, a frame-bounded settle coroutine with a generation latch. Covers field menu, item list, item-use targets, equipment command bar, job change, ability command/spell/target/equip, status character-select, and save/load slots. The equipment *slot* and *item-select* panes are deliberately excluded — their `SelectContent` / `SetCursor` navigation patches already fire on entry, so an `*Init` hook there double-read; those two panes invalidate each other's dedup guard instead. See `docs/debug.md` for the hook table and the disjointness caveat.

### Known Limitations
- **Key help (Shift+I)**: Reads only the currently displayed page of controls. Menus with paginated controls (e.g., Music Player with 2 pages) will only read the visible page. This is a limitation of reading live UI state — the off-screen page's controllers aren't populated with current toggle state.
- **Ability equip screens**: `AbilityChangeController` exposes no cursor field, so the initial-focus read uses the index cached by the navigation postfix (0 on first entry, which is the game's own default).

## Completion Status

| Feature | Status |
|---------|--------|
| All menus (cursor, item, equip, job, ability, config, shop, save, title) | Done |
| Battle (commands, targets, messages, results, abilities) | Done |
| Field navigation (entities, filters, grouping, waypoints) | Done |
| Audio system (wall tones, footsteps, beacons, landing pings, ModMenu) | Done (WaveOut backend, volume rebalanced) |
| Vehicles (state announcements, landing detection, entity tracking) | Done |
| Popups (common, game over, save/load, naming, info, job change, save complete) | Done |
| Speech/dialogue (auto-read, redundancy fixes, delayed announcements) | Done |
| Deep refactoring (PreferencesManager, AudioLoopManager, ToneGenerator, KeyBindingRegistry, etc.) | Done |
| Entity filter refactor (IEntityFilter, FilterTiming, IGroupingStrategy) | Done |
| Performance optimization (GameObjectCache, state flags, GameConstants) | Done |
| LocalizationHelper (12-language mod string dictionary) | Done |
| Battle results navigator (L key, navigable grid with EXP/Next/ABP) | Done (ABP fix applied) |
| Battle results navigator multi-page (PageUp/PageDown or L1/R1, one page per result phase + one per levelling character) | Verified in-game 2026-07-26 |
| Battle results level-up page announcement (Lv./HP/MP/job, before → after) | Verified in-game 2026-07-26 |
| Battle results compact row format ("HP: 44 > 53 (9)" instead of before/after/change labels) | Verified in-game 2026-07-26 |
| Quick Save completion popup announcement (orphaned postfix registered on InitComplite) | Verified in-game 2026-07-26 |
| Magic command bar initial focus read twice (AbilityCommand_Init_Postfix reduced to clear-only) | Verified in-game 2026-07-26. Superseded later the same day: it now clears its *siblings* instead of itself, so the entry-path clear is gone entirely — re-verify the command bar reads once on entry and on back-out from the spell list |
| Battle results EXP totals-only speech format (level-ups moved off page 1) | Done |
| Quick Save SaveLoadMenuState.IsActive leak (silenced main-menu cursor until menu reopened) | Done — **not explicitly exercised**. Silent failure mode; needs the specific check: quick save, dismiss the popup, then move the main-menu cursor without returning to the field |
| Battle results job level up: typed hook on SetJobProficiencyData | Deferred — diagnostic logging in place; needs a battle with jobs unlocked to confirm page content |
| Job list read the focused job **three** times on entry (`ClearLast()` landing inside the game's own `SelectContent` burst; `SelectJobInit` unpatched, guard now clears on `SetActive(false)` / `ConfirmPopupInit` / `FixJobChangeInit`) | Fixed 2026-07-26, **not yet verified**. Enter Jobs: expect exactly one read and no `[JobSelect] initial focus read` line at all. Then back out and re-enter on the same job — if that goes **silent**, `SetActive(false)` is not firing on exit and the exit signal must move to `MainMenuController`. Also check backing out of the job confirm popup re-announces the row |
| Guard clears moved off entry paths onto exit paths across the whole job/ability family (each `*Init` clears its siblings, never itself) | Fixed 2026-07-26, **not yet verified**. Regression surface is *silence*, not doubling: command bar → spell list → target, and ability equip command pane ↔ list pane, must each announce on every entry **and** on every back-out |
| Spell list initial focus: is SpellList_Init_Postfix redundant like the command bar? | Superseded 2026-07-26 — no longer needs answering. The deferred read is self-limiting (it hits the guard and logs `gave up` when navigation already spoke), so the sibling-clear change is correct either way. Verify the spell list reads each spell exactly once now that a spell is learned |
| List guards keyed on index rather than announced text (two same-named items, two empty equip slots) | Verified in-game 2026-07-26 |
| Popup initial focus: job-change popup button + in-message choice windows ("Leave?") | Verified in-game 2026-07-26 |
| Waypoint pathing speaks bare directions (no "Path to X" preamble) | Verified in-game 2026-07-26 |
| Battle results log on controller (mod button + Circle) | Verified in-game 2026-07-26 |
| Battle state enters at the encounter, not the first command window (wall tones) | Verified in-game 2026-07-26 for random + scripted encounters |
| Battle state on BOSS encounters | Fixed 2026-07-26, **not yet verified**. The first build's hook did not resolve — Il2CppInterop mangles the explicit interface impl as `Last_Map_IEventAccessor_EventEncountBoss` (underscores, not dots), so bosses fell back to scene-load timing. Startup warning caught it. Confirm `[BattleStart] Hooked Last_Map_IEventAccessor_EventEncountBoss` appears in the log |
| Battle message guards scoped to one battle (pooled BattleActData) | Verified in-game 2026-07-26 |
| Battle menu left/right announces the landed-on sub-menu option | Verified in-game 2026-07-26 |
| Equip menu Auto Detail gate + I/U details keys (and the Items-menu stale read) | Verified in-game 2026-07-26. Open: no `[EquipDetails]` line has appeared in a log yet, so whether `OwnedItemData.TypeId` shares the content-type space `BuildEquipJobsAnnouncement` gates on (2=weapon, 3=armor) is still unconfirmed — it only matters for the U key on a slot |
| Status screen Commands/Abilities panel in stat navigation | Verified in-game 2026-07-26 |
| Jobs screen equippable types (all 22 jobs, extracted offline from the Ghidra project) | Verified in-game 2026-07-26 |
| Jobs screen description/equippable panel gate (I key read the wrong panel) | Verified in-game 2026-07-26 |
| EXP counter sound (rapid beep, auto-stops on animation end) | Done |
| Entity name translator (JSON-based) | Done. `EntityDump` is a dev tool and is no longer bound to a key (0 used to reach it from normal play) |
| Offline mass entity-label extraction (tools/extract_entities.py → translation.generated.json, 1367 labels) | Done |
| Entity labels translated into all 12 languages + promoted to embedded translation.json | Done |
| Battle targeting status effects (Poison, Blind, etc.) | Done |
| Initial-focus announcements (all menus, MenuFocusAnnouncer) | Verified (further testing in progress) |
| AnnouncementDeduplicator removal (105 sites → ~20 local guards) | Verified (further testing in progress) |
| Job stat bonuses (Strength/Vitality/Agility/Magic) | Missing |
| Bestiary (Picture Book) accessibility | Done (extras + config menu) |
| Music Player (Extra Sound) accessibility | Done (duration fix applied) |
| Gallery (Extra Gallery) accessibility | Done |
| Event loop freeze fix (Pyramid 5F) | Done (diagnostic code + grace period removed; fix lives in TimerPatches dynamic patch) |
| Global accessibility toggle (Ctrl+F8) | **Not present.** No Ctrl+F8 binding or `ToggleAccessibility` exists in the current code; this row described a feature that was later removed |
| Battle text dual-wield suppression | Done (ally same-name + direct attack = skip second swing) |
| SDL3 input/audio migration | Done (SDL3.cs + AudioEngine + GamepadManager; the earlier revert was superseded) |
| Controller/gamepad support | Done (ControllerRouter state machine; right stick up/down/left mirror I / Shift+I / U in menus, entity scanner on field) |
| Auto Detail (F7) | Done (F7 toggle + spoken confirmation; defaults on) |
| Details key parity with FF1/FF4 | Done (I reads descriptions everywhere; equip-job list moved to the new U key) |
| Shop equip compatibility (U) | Done (master-data lookup, so it works on unowned shop goods) |
| F5 / F8 gating | Done (field + field menus; blocked in battle and on title screen. F1/F3 are the game's keys — see the game-toggle row) |
| Mod string localization | Done (384 keys x 12 languages after the 2026-09-23 parity pass; audit reports 0 missing `T()` keys. Mod-menu labels and waypoint category names are keys passed raw and translated at speak time) |
| Job menu UI-based level/ABP/mastered reading | Done (replaces data-based OwnedJob.Level which returned wrong values) |
| Status screen UI-based job level reading | Done (same level 0 fix) |
| Mod mode reachable during dialogue/cutscenes | Done (OnUpdate no longer early-returns on event state) |
| Mod menu blocked during dialogue/cutscenes | Done (explicit gate in IsFieldOrFieldMenuActive; covers F8, F5, Start) |
| Repeat dialogue (R key / mod + Square) | Done (DialogueTracker.RepeatCurrentPage, ported from FF1) |
| Unified audio suppression gate | Done (AudioLoopManager.IsAudioSuppressed across all 5 sound features) |
| Enemy HP display toggle honoured | Done (was a dead preference — always leaked exact HP) |
| Menu-state self-heal on return to field | Done (one missed Close hook used to disable all field features) |
| Enemy letters (A/B/C) toggle | Done (mod-derived; FF5 has no game-side label. Default off) |
| Spurious "0 gil" announced at mod load | Fixed 2026-07-26, **not yet verified**. Cause was frame-0 edge synthesis: `keyPrevious[]` starts all-false, so a key held on the mod's first input poll dispatched as a fresh press. Keyboard and gamepad snapshots are now primed. Confirm the log shows `Keyboard primed` and no `"0 gil"` before the title screen; re-test holding G through the whole load |
| Gil gated to field + field menus | Fixed 2026-07-26, **not yet verified**. Title screen says "Not on map", battle says "Unavailable in battle", field menus still read gil |
| Vehicle routing (`VehicleRouteSearcher`) — unbounded terrain-attribute flood while riding | Done, **not yet verified in-game**. Needs a ship/airship run past 32 tiles, a seam crossing to confirm world-map wrap, and a chocobo run to confirm per-transport terrain |
| Diagonal routing for free-moving vehicles | Added 2026-07-26, **not yet verified**. The flood is 8-connected for `Plane`/`LowFlying`/`SpecialPlane` via `RoutingAdapter.AllowsDiagonalMovement` (public `GetTransportationType`, no offsets), with a corner rule against squeezing between two impassable tiles. 40 north / 30 west costs **70 steps 4-connected, 40 steps 8-connected**. Expect "north 10, northwest 30" from an airship. Everything else stays 4-connected — foot and ship movement is grid-locked |
| Staircase legs on open terrain | **Investigated 2026-07-26: not a real defect.** The BFS was replicated outside the game with its actual neighbour order; fixed-order FIFO BFS already canonicalises every shortest path to two legs (`north 40, west 30`), on open terrain and around obstacles. A tie-break/straightening pass was planned and then deliberately **not written**. The `N, S, E, W` visit order is what produces this — `docs/debug.md` marks it as load-bearing |
| World-map wrap flags | Fixed 2026-07-26, **not yet verified**. Were `IsWorldMap(mapId)` — a guess. Now `MapModel.GetLoopType()` vs `MapConstants.LoopType`, cached per map by `RoutingAdapter.RefreshMapInfo` from `EnsureGrid`. The guess remains the fallback if the read throws. Also warns once when `CheckCurrentWorldMap()` disagrees with `GameConstants.IsWorldMap`. Confirm the log shows `Map 0 loop type N` and that a seam crossing goes through the seam |
| Landing pings read the wrong vehicle's list | Fixed 2026-07-26, **not yet verified**. `GetNearbyLandingSpots` fell back from `TransportationInfo.Id` to `MoveStateHelper.GetCurrentTransportType()` — **different number spaces**, so a null `CurrentTransportation` silently indexed another vehicle's `landingList`. Now skips the check when the id is unknown rather than answering wrongly |
| Breadcrumb chaining (`BreadcrumbRouteChainer`) — on-foot targets beyond the ~31.5-tile window | Written, then found **never to have executed** (first test, 2026-07-26): `MapRouteSearcher.Search` *throws* outside its window rather than returning empty, and a silent catch in `FindPathTo` swallowed it and returned before the fallback. Now routed through `SafeSearch`. **Still unverified — the hop search has never actually run.** Highest-risk check is the *retreat* case: a room whose exit faces away from the objective; a wrong implementation shows up as a spurious "no path" |
| Silent catch around the whole routing path | Fixed 2026-07-26. Both the per-search wrapper and the outer backstop now log once. This is why an entire subsystem failed invisibly for a full session — a catch wrapping a feature must never be silent |
| "Requires Pirate Ship" for vehicle-gated destinations | **Removed 2026-07-26.** Terrain reachability cannot see event gating, so the claim was unfalsifiable — naming a ship when the real blocker is an unfinished event is worse than silence. Terrain *naming* would not have helped: the check read each vehicle's `OkList` directly, and a name would be derived from that same data. Rationale in `docs/debug.md` |
| Route speech: hop seams merged, long routes truncated to 6 legs + "and N more" | Done, **not yet verified in-game**. A straight run across hop seams must read "north 89", never "north 24, north 30, north 35" |
| Filter-aware entity counts (`FilteredCount`/`FilteredIndex`) | Done, **not yet verified in-game**. With the pathfinding filter on, expect "1 of 4"; with all filters off, unchanged |
| MapRouteSearcher coordinate space (`PathSpaceNormalizer`) | **Answered in-game 2026-07-26: WORLD space.** No pre-existing north/south inversion; on-foot routes were always described correctly. The conversion branch is dead on FF5 and stays only as a guard for the sibling ports |
| Attribute grid build cost | **Measured 2026-07-26: 17 ms** for 256x256 (65,536 cells, max attribute 27). Well under the ~150 ms threshold that would have forced a bulk `int[,]` pointer read, so the simple per-cell build stays |
| Routing portability to sibling FFPR mods | Done (structural). `Field/Routing/` is speech-free, log-free and god-class-free; port checklist in `docs/debug.md`. Not yet exercised against a sibling mod |
| Beacon follows the route (aim at the next turn; retire the down-pitched "out of range" Mode B) | **Designed, not implemented** — blocked on in-game verification of the routing work above. Design in `docs/debug.md` → "Planned: beacon follows the route". Prerequisite: `RouteLeg` must carry each leg's end vertex. Key risk is the cached-route invalidation list, not the aiming itself |
| **FF1 parity pass (2026-09-23)** — rows below are built clean but **not yet verified in-game** unless stated | |
| Field-menu hotkeys said "Not on map" (G, M, V, F5, F8, Start) | Fixed, **not yet verified**. `MainMenuController.Show`'s `GameObjectCache.ClearAll()` dropped the FieldPlayerController; Show now carries the field entries over, and `IsOnValidMap` self-heals for on-demand callers (the per-frame `DetermineContext` stays cache-only). Open the field menu and press G, M and F8 |
| Config Gamepad/Keyboard Controls list navigation (arrows/WASD/D-pad, Ctrl+arrow top/bottom, `KeyContext.KeyHelp`) | Done, **not yet verified**. Hooks `ConfigKeysSettingController.GamePadHelpInit/KeyboardHelpInit` (open) and `GamePadSelectInit/KeyboardSelectInit/Close` (close). Expect the first control on open and "(N of M)" on each step |
| Controls rows: mouse bindings, fixed-button glyphs (LB/RB/LT/RT/L3/R3/Back/Menu/D-pad), help-row names, `gamePadIconsRoot` gate | Done, **not yet verified**. Expect no spurious "(A)" on Reset to Defaults / Gamepad Controls rows |
| Hotkey parity: backtick rescan, K = entity only, Shift+K = All, P/\ restart beacon when beacons on, Ctrl+P layer filter, F6 beacons (9 kept), WASD in stat buffers, 0 unbound | Done, **not yet verified** |
| Status screen LB/RB character switch | Done, **not yet verified**. Hooks `Serial.Template.UI.StatusDetailsControllerBase.SetNextPlayer/SetPrevPlayer`; navigation now also arms when the summary read is empty. Evasion: settled offline in the session-2 pass below (it is `EvasionRate`, a percentage) |
| Battle pause menu | Done, **not yet verified**. `BattlePauseController.SetCommandSelectCursor` (open + every move); expect the focused command with position, once per move |
| Battle I key / right stick up in item and ability lists; Auto Detail gates their descriptions | Done, **not yet verified** |
| Mod menu: I / right stick up describes the setting; "(N of M)" position; labels translate at speak time | Done, **not yet verified**. Labels used to be translated at mod load, before the game's language was known, so they were always English |
| Localization sweep (entity kinds, steps, directions, category names, status/bestiary labels, waypoint categories, results totals, shop, timers, choices, popups) | Done. 112 new keys |
| Shops: "Quantity: N, Total: X" (also on open), "(N of M)" on buy/sell rows, "Empty" sell slots, command bar gated to `ShopController.State.SelectCommand` | Done, **not yet verified**. The gate is a pointer read (stateMachine 0x98 → current 0x10 → Tag 0x10); if the command bar ever goes silent, the offsets are the first suspect |
| Bestiary: `LibraryInfoController.SetData` is the sole detail announcer, top buffer entry "Name" | Done, **not yet verified**. Page-button hooks removed — page turns reach SetData via `LibraryInfoManager.NextPage/PreviousPage → ShowData`. Expect exactly one read per monster switch |
| Controller mod mode + A = walk/run/vehicle (V equivalent) | Done, **not yet verified** |
| F1/F3 announced whatever the source (keyboard, stick click, anything that drives the field toggle) | Done, **not yet verified**. Prefixes on `ConfigClient.SetIsAutoDash` / `CheatSettingsClient.SetIsEnableEncount`, gated to the field with no menu open and to a real change. Replaces the F1/F3 key polling |
| Title screen "Press any button" | Done, **not yet verified**. `TitleWindowController.InitShortcutCommand`; FF1 found its equivalent boot-only, so check a return to title too |
| Location banner after "Entering X" not repeated; first map load after boot announced | Done, **not yet verified** |
| Line-fade text (intro/ending/game-over lines) | Fixed, **not yet verified**. Moved to `LineFadeMessageWindowController.SetData`: `LineFadeMessageManager.Play` has no caller, so the old hook never fired |
| Battle back-out re-announce (command re-read on cancel from targeting); first ally read when single-ally targeting opens | Done, **not yet verified** |
| Equip LB/RB character switch; config Library row re-announce; naming popup ". Current: X"; InputPopup reader | Done, **not yet verified** |
| Battle messages: MP damage, drains, locale-independent attack/defend/item, per-battle guard resets, stale results no longer pin `KeyContext.BattleResult` | Done, **not yet verified**. Defend is recognised by `Command.CommandType == 8` (Defence) minus Escape (22) — confirm Defend still reads "X defends" |
| Menu Text Discovery English-only fallbacks ("Battle Type", On/Off regex) | Fixed in the session-2 pass below |
| **Open-issues pass (2026-09-23, session 2)** — built clean (0 warnings, 0 errors), **not yet verified in game** | |
| Status screen Evasion reads the on-screen value | Fixed, **not yet verified**. The row is `ParameterType.EvasionRate` (17), read from the `status_details` prefab in `key_menu_assets_all`; the game shows `ConfirmedEvasionRate(false)` with a "%" (`IsPercent(17)` → `SystemConfig.IsVisibleEvasionRatePercent()`, base `true`, not overridden in FF5). `ReadEvasion` now reads "Evasion: N%" instead of `ConfirmedDefenseCount()`. The other seven rows were checked the same way and already matched |
| Menu Text Discovery English checks | Fixed, **not yet verified**. The "Battle Type" skip was dead — FF5 has no such string in any message table or UI bundle (its row is "Battle Mode") — and is removed. The On/Off/Active/Wait value filter now compares against the game's own localized `MSG_CFG_INF_43/44/138/139` |
| Touch save list: autosave / quick-save rows numbered | Fixed (hardening), **not yet verified**. The PC title Load list is the KeyInput `SaveListController` (`key_loadgame_assets_all`), which already read the slot id at 0x38. The Touch path assumed "numbered" for every row; it now reads the Touch `SlotData` at 0x50 |
| Battle I key could read a closed list's description | Fixed, **not yet verified**. Gated on the list's own `StateMachine` (item 0x58, ability 0x30 → current 0x10 → Tag 0x10; 0 = None) and on `view.rootObject` being visible, instead of the controller GameObject alone (which stays active after the list closes) |
| "Encounters on/off" on a load from the game-over flow | **Not a bug** — proved offline. The load re-applies the value it just loaded, so the "real change" check always returns first |
| First ally target read index | Fixed, **not yet verified**. Now uses the game's own `selectCursor.Index`, and waits (within the settle cap) until `playerDataList` is the list the game navigates |
| Value-0 battle events | Partial, **not yet verified**. Speech now follows the game's own view rules in `BattleBasicFunction.CreateDamageView`. No "0 damage" for value-0 Hit/Non views (nothing is drawn for them). `RecoveryCondition` reads as HP recovery, and only with a value. A real 0-damage hit (`Zero`) still reads "X: 0 damage". **A pure status cure is still silent**: it was not proved offline that buffs never carry `RecoveryCondition`, so there is no "cured" line. One `[Battle] value-0 view:` log line per value-0 view settles it |
| Vehicle entity names capitalised in the entity list | Done, **not yet verified**. Display-only; "On {0}" movement speech unchanged |
| Controller unplugged in mod mode / keyboard-closed mod menu | Fixed, **not yet verified**. Event-driven: `GamepadManager.HandleGamepadRemoved` drops mod mode; `ModMenu.Close` returns the router to Normal |
| F8-opened mod menu did not sync the controller state | Fixed, **not yet verified**. `ModMenu.Open` sets the router to ModMenu, so D-pad/stick/LT drive the menu instead of field navigation underneath it |
| Tab "clear stuck battle flag" (FF1 issue) | N/A — FF5 has no such key |

## Documentation
- **CLAUDE.md** — Rules, syntax, directory structure
- **docs/debug.md** — Architecture, class references, debug history
- **docs/PerformanceIssues.md** — Performance notes
