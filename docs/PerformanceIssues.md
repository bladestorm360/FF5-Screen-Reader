# FF5 Screen Reader: performance notes

This file is named in `CLAUDE.md` (rule 4) but did not exist before 2026-09-24. Older performance work
is recorded in `docs/debug.md` ("Performance Optimization (2026-01-30)", the routing "Performance
notes", and the per-frame notes in each pass).

## Rules this mod follows

- **No per-frame Harmony hooks** (rule 2). Hook the method that runs once per event: a state `Init`,
  a cursor setter, or a `ChangeState`.
- **No `WaitForSeconds` or time-based delays** (rule 3). A short retry is allowed only when it starts
  from an event and is bounded in frames (`MenuFocusAnnouncer`, 6 frames at most).
- **Allowed every frame:** keyboard/gamepad input (`InputManager`, `GamepadManager`), the controller
  router, the input passthrough patches, the audio loops (wall tones, footsteps, beacons, landing
  pings, EXP counter) and map-transition detection.

## Round 2 sweep (2026-09-24)

Removed from the frame:

| Was | Now |
|---|---|
| `LibraryMenuController.UpdateController` postfix (bestiary minimap state + map index, every frame) | `LibraryMenuController.ChangeState` + `LibraryMenuHabitatController.OnContentSelected` |
| Bestiary formation: `FindObjectOfType<ArBattleTopController>` every frame for up to 3 s | `ArBattleTopController.SetActive(true)` postfix; `ChangeMonsterParty` reads `__instance` |
| Gallery / Music Player entry: 2 s `Time.deltaTime` polls | focus event + `MenuFocusAnnouncer` (frame-bounded) |
| `GameOverLoadPopup.UpdateCommand` postfix (every frame) | open read from `InitSaveLoadPopup`; moves from `Cursor.NextIndex/PrevIndex` |
| `SavePopup.UpdateCommand` postfix (every frame while a save/load/quick-save popup is up) | each controller's open hook (`SetPopupActive`, `SetEnablePopup`, `InitComplite`, `OverwriteConfirmInit`); moves from `Cursor.NextIndex/PrevIndex` |
| `ConfigCommandController.SetFocus` postfix (every row, every frame, config screen) | `ConfigActualDetailsControllerBase.SelectCommand` + list-focus / popup-close / Library-exit events |
| `SwitchSliderTypeProcess` postfix (every frame on a focused slider) | Unity `Slider.onValueChanged` listener (fires only on a real change) |
| `WaitForSeconds` in `ConfirmationDialog` (0.1 s ×2), `TextInputWindow` (0.3 s ×2), `SpeakTextDelayed` (0.3 s) | immediate speech; results queued |

Still per-frame, on purpose:

| Where | Why |
|---|---|
| `Timer.Update` prefix | Applied only while the player's timer freeze is on. |
| `InputSystemManager.GetKey*` postfixes | Controller passthrough / game-input suppression (input core). |
| `MonitorExpCounterAnimation` (0.1 s tick) | Allowed audio loop: feeds the SDL Counter stream while the EXP bar counts. |
| `InputManager.DetermineContext` | Input context for the controller router and the audio gate; cache-only reads (no `FindObjectOfType`). `IsScreenFading` still goes through reflection. |

## Hidden-passage retry (2026-10-03)

`FindPathTo` repeats its on-foot search (target plus eight neighbours, three layers each: at most 27
`MapRouteSearcher.Search` calls) once with the map's hidden passages open, but only when the first
pass found nothing and only on the 40 sub-maps that have passages. On those maps an unreachable
target costs up to twice the searches it did before (the pathfinding filter and the beacon loop call
`FindPathTo`); every other map and every reachable target costs the same as before. The grid swap
itself is two reference assignments.

## Vehicle route searcher removed (2026-10-03)

The world-map attribute grid (a 256×256 scan, about 17 ms, built on every world-map load) and the
vehicle flood search are gone with `VehicleRouteSearcher`. Riding now costs what walking costs: the
game's `MapRouteSearcher`.

## Entity presence and scenery checks (2026-10-03)

`FieldEntityState.IsPresent` adds, for an inactive entity only, a parent check, the target-vehicle
list count and two int field reads. `IsScenery` adds up to three `TryCast` calls per entity. A
skipped scenery object is not cached, so like every other skipped object it is re-checked on each
scan (each navigation keypress): a few dozen casts on a busy map. Neither runs per frame.
