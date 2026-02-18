# FF5 Screen Reader Mod — Project Plan

## Overview
Accessibility mod for FF5 Pixel Remaster. MelonLoader + Harmony patches hook Il2CPP game code, output via Tolk to NVDA.

## Features

**Menus**: Cursor navigation, item/equipment/job/ability/config/shop/save slot/title menus. I key for details (job descriptions, item equip compatibility). Mutual exclusion between menu trackers.

**Battle**: Turn order, command/target selection, damage/heal/status/defeat messages, per-phase results (EXP/Gil/ABP, level-up stats, abilities, items), steal results, MissType.NonView suppression, battle action object dedup.

**Navigation**: Entity cycling (N/M), category filter (F), pathfinding filter (R), exit grouping (Q), wall collision sound, waypoint system (add/rename/remove/cycle/pathfind).

**Audio**: ModMenu (F8) with toggles/volume sliders/enum selectors. Wall tones, footsteps, audio beacons, landing pings. 16-bit audio, LRU tone cache, volume caching.

**Vehicles**: Movement state announcements (on foot/ship/airship/chocobo/submarine), landing zone detection via terrain attributes + CheckLandingList/OkList, vehicle entity tracking on world map.

**Other**: Dialogue/message auto-read, timer (T key), F1 walk/run, F3 encounters, F5 enemy HP display, delayed dialog announcements (0.3s for NVDA focus), speech redundancy fixes, naming popup enhancements.

**Bestiary (Picture Book)**: Full screen reader support for the enemy encyclopedia. List navigation with entry number/name, detail view with navigable stat buffer (arrow keys, Shift for group jump, Ctrl for top/bottom), formation announcements, map/habitat name reading, page turn support, monster switching in detail view. Shift+I reads control tooltips. Minimap open/close/cycle with habitat names. Full map open/close/cycle with habitat names. Items read from master data (UI uses icons only).

**Music Player (Extra Sound)**: Screen reader support for the music player extras screen. Song list navigation with track number, name, and duration. Play All toggle (on/off) and Arrangement/Original toggle announcements. Automatic first-song announcement on entry. State cleanup on exit.

**Gallery (Extra Gallery)**: Screen reader support for the image gallery extras screen. List navigation with item number and name. "Image open" announcement on detail view. Automatic item re-announcement when returning from detail view. State cleanup on exit.

### Known Limitations
- **Key help (Shift+I)**: Reads only the currently displayed page of controls. Menus with paginated controls (e.g., Music Player with 2 pages) will only read the visible page. This is a limitation of reading live UI state — the off-screen page's controllers aren't populated with current toggle state.
- **Menu entry announcement timing**: When entering or returning to a menu, the focused item is announced slightly before cursor control is active (~1 frame). No game-side input-readiness hook exists to defer further without polling (Rule 2) or timers (Rule 3).

## Completion Status

| Feature | Status |
|---------|--------|
| All menus (cursor, item, equip, job, ability, config, shop, save, title) | Done |
| Battle (commands, targets, messages, results, abilities) | Done |
| Field navigation (entities, filters, grouping, waypoints) | Done |
| Audio system (wall tones, footsteps, beacons, landing pings, ModMenu) | Done (SDL3 volume rebalanced) |
| Vehicles (state announcements, landing detection, entity tracking) | Done |
| Popups (common, game over, save/load, naming, info, job change, save complete) | Done |
| Speech/dialogue (auto-read, redundancy fixes, delayed announcements) | Done |
| Deep refactoring (PreferencesManager, AudioLoopManager, ToneGenerator, KeyBindingRegistry, etc.) | Done |
| Entity filter refactor (IEntityFilter, FilterTiming, IGroupingStrategy) | Done |
| Performance optimization (GameObjectCache, state flags, GameConstants) | Done |
| LocalizationHelper (12-language mod string dictionary) | Done |
| Battle results navigator (L key, navigable grid with EXP/Next/ABP) | Done (ABP fix applied) |
| Battle results stat gains (HP/MP +N format from data) | Done |
| Battle results EXP totals-only speech format | Done |
| EXP counter sound (rapid beep, auto-stops on animation end) | Done |
| Entity name translator (JSON-based, EntityDump key 0) | Done |
| Battle targeting status effects (Poison, Blind, etc.) | Done |
| Job stat bonuses (Strength/Vitality/Agility/Magic) | Missing |
| Bestiary (Picture Book) accessibility | Done |
| Music Player (Extra Sound) accessibility | Done (duration fix applied) |
| Gallery (Extra Gallery) accessibility | Done |
| Event loop freeze fix (Pyramid 5F) | Done (diagnostic code + grace period removed; fix lives in TimerPatches dynamic patch) |
| Global accessibility toggle (Ctrl+F8) | Done (complete kill switch: coroutine cleanup, full state reset, reinit on re-enable) |

## Documentation
- **CLAUDE.md** — Rules, syntax, directory structure
- **docs/debug.md** — Architecture, class references, debug history
- **docs/PerformanceIssues.md** — Performance notes
