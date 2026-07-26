# FF5 Screen Reader Mod — Project Plan

## Overview
Accessibility mod for FF5 Pixel Remaster. MelonLoader + Harmony patches hook Il2CPP game code, output via Tolk to NVDA.

## Features

**Menus**: Cursor navigation, item/equipment/job/ability/config/shop/save slot/title menus. I key for details (job descriptions, item equip compatibility). Mutual exclusion between menu trackers.

**Battle**: Turn order, command/target selection, damage/heal/status/defeat messages, per-phase results (EXP/Gil/ABP, level-up stats, abilities, items), steal results, MissType.NonView suppression, battle action object dedup. Optional A/B/C letters on same-named targets. Results ABP column appears only when it applies (not for Freelancer/mastered jobs).

**Navigation**: Entity cycling (N/M), category filter (F), pathfinding filter (Shift+\ / Shift+P), exit grouping (Q), wall collision sound, waypoint system (add/rename/remove/cycle/pathfind).

**Audio**: ModMenu (F8) with toggles/volume sliders/enum selectors. Wall tones, footsteps, audio beacons, landing pings. 16-bit audio, LRU tone cache, volume caching.

**Vehicles**: Movement state announcements (on foot/ship/airship/chocobo/submarine), landing zone detection via terrain attributes + CheckLandingList/OkList, vehicle entity tracking on world map.

**Other**: Dialogue/message auto-read, repeat current dialogue page (R key, or mod + Square on controller), timer (T key), F1 walk/run, F3 encounters, F5 enemy HP display, delayed dialog announcements (0.3s for NVDA focus), speech redundancy fixes, naming popup enhancements.

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
| Battle results navigator multi-page (PageUp/PageDown or L1/R1, one page per result phase + one per levelling character) | Done — needs in-game verification |
| Battle results level-up page announcement (Lv./HP/MP/job, before → after) | Done |
| Battle results compact row format ("HP: 44 > 53 (9)" instead of before/after/change labels) | Done — needs in-game verification |
| Quick Save completion popup announcement (orphaned postfix registered on InitComplite) | Done — needs in-game verification |
| Quick Save SaveLoadMenuState.IsActive leak (silenced main-menu cursor until menu reopened) | Done — needs in-game verification |
| Battle results EXP totals-only speech format (level-ups moved off page 1) | Done |
| Battle results job level up: typed hook on SetJobProficiencyData | Deferred — diagnostic logging in place; needs a battle with jobs unlocked to confirm page content |
| EXP counter sound (rapid beep, auto-stops on animation end) | Done |
| Entity name translator (JSON-based, EntityDump key 0) | Done |
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
| Global accessibility toggle (Ctrl+F8) | Done (complete kill switch: coroutine cleanup, full state reset, reinit on re-enable) |
| Battle text dual-wield suppression | Done (ally same-name + direct attack = skip second swing) |
| SDL3 input/audio migration | Done (SDL3.cs + AudioEngine + GamepadManager; the earlier revert was superseded) |
| Controller/gamepad support | Done (ControllerRouter state machine; right stick up/down/left mirror I / Shift+I / U in menus, entity scanner on field) |
| Auto Detail (F7) | Done (F7 toggle + spoken confirmation; defaults on) |
| Details key parity with FF1/FF4 | Done (I reads descriptions everywhere; equip-job list moved to the new U key) |
| Shop equip compatibility (U) | Done (master-data lookup, so it works on unowned shop goods) |
| F5 / F8 gating | Done (field + field menus; blocked in battle and on title screen. F1/F3 left alone — they are game keys the mod only narrates) |
| Mod string localization | Done (250 keys x 12 languages; audit reports 0 missing `T()` keys) |
| Job menu UI-based level/ABP/mastered reading | Done (replaces data-based OwnedJob.Level which returned wrong values) |
| Status screen UI-based job level reading | Done (same level 0 fix) |
| Mod mode reachable during dialogue/cutscenes | Done (OnUpdate no longer early-returns on event state) |
| Mod menu blocked during dialogue/cutscenes | Done (explicit gate in IsFieldOrFieldMenuActive; covers F8, F5, Start) |
| Repeat dialogue (R key / mod + Square) | Done (DialogueTracker.RepeatCurrentPage, ported from FF1) |
| Unified audio suppression gate | Done (AudioLoopManager.IsAudioSuppressed across all 5 sound features) |
| Enemy HP display toggle honoured | Done (was a dead preference — always leaked exact HP) |
| Menu-state self-heal on return to field | Done (one missed Close hook used to disable all field features) |
| Enemy letters (A/B/C) toggle | Done (mod-derived; FF5 has no game-side label. Default off) |

## Documentation
- **CLAUDE.md** — Rules, syntax, directory structure
- **docs/debug.md** — Architecture, class references, debug history
- **docs/PerformanceIssues.md** — Performance notes
