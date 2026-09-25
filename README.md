# FF5-screen-reader

## Purpose

Adds NVDA output, pathfinding, sound queues and other accessibility aides to Final Fantasy V Pixel Remaster.

## Known Issues

When reading controls, only the current page of controls will be announced. On screens that have multiple pages, the game automatically scrolls between the pages every few seconds.

Pathfinding does not work correctly when sailing the pirate ship. If you get stuck early in the game between Tule and the wind shrine, use waypoint 1 (a docking point near the wind shrine) and waypoint 2 (A landmark after which tule becomes findable on the destination finder to get there.) This is the fix for now, waypoints.json can be updated as needed for other sticking points in progression.

Shops have not been tested.

Status reader is not reading commands list of the selected character.

Victory screen after battle may speak when a character has learned an ability from job level up, but not which ability was learned.

I in job menu does not yet speak which weapons a job can equip.

Waypoints placed from the pirate ship can only pathfind when on the pirate ship.

Teleporting necessary in many dungeons due to moving platforms, traps, patrolling guards. As of yet unresolved due to mod limitations.

Will likely need at least a basic guide to complete the game, especially when travelling the world map between towns.

## Install

Create an account at store.steampowered.com, login, join steam.

Once account is created, install steam download app (should be prompted to do so after account creation.)

Log into desktop app.

to purchase games, the easiest way is to use the web interface. You can search for a game when logged into the browser, purchase it there and will be asked if you want to install your games, which opens the desktop app to finish installation.

Ensure you purchase Final Fantasy V, the page should mention being remastered in the description. Do not buy Final Fantasy V Old Ver.

Install MelonLoader into game's installation directory. Ensure nightly builds are enabled.
https://github.com/LavaGang/MelonLoader/releases

Copy NVDAControllerClient64.dll and tolk.dll into installation directory with game executable, usually c:\Program Files (x86)\Steam\Steamapps\common\Final Fantasy V PR.

If you created a steam library on another drive, the path will be Drive Letter\Path to steam library\SteamLibrary\steamapps\common\Final Fantasy V PR.

FFV_screenreader.dll   goes in MelonLoader/mods folder.

waypoints.json goes in MelonLoader/UserData folder.

## Keys

### Game

- WASD or arrow keys: movement
- Enter: Confirm
- Backspace: cancel
- F1: toggle between walk and run. The mod announces the new setting, whichever way you toggle it.
- F3: toggle random encounters on and off. The mod announces the new setting, whichever way you toggle it.

### Mod

- J and L or [ and ]: cycle destinations in pathfinder
- Shift+J and L or - and =: change destination categories. Announces the category and its nearest destination.
- \ or P: get directions to selected destination. With audio beacons on, restarts the beacon instead.
- Shift+\ or Shift+P: Toggle pathfinding filter so that not all destinations are visible, just ones with a valid path.
- Ctrl+\ or Ctrl+P: Toggle layer transition filter (hides stairs and layer-change destinations from navigation).
- K: announce the currently selected destination again.
- Shift+K: Reset category to all
- Backtick (the key above Tab): rescan nearby entities.
- ': Toggle footsteps
- ;: Toggle wall tones
- Shift+;: Toggle landing pings
- F6 or 9: Toggle audio beacons
- G: Announce current Gil
- M: Announce current map.
- Shift+M: Toggle map exit filter so multiple exits to the same place collapse to the nearest one.
- H: In battle, announce the active character's HP, MP and status effects.
- R: Repeat the current dialogue page.
- T: Announce active timers. Shift+T: freeze or resume timers.
- V: Announce walk or run, or the vehicle you are riding.
- I: Read the description of whatever is highlighted. In configuration menu accessible from tab menu, read description of highlighted setting. In jobs menu, read description of highlighted job. In spell or ability menus, read description of highlighted spell or ability. In shop menus, reads description of highlighted item. In item menu, reads the description of the highlighted item. In battle item and ability lists, reads the description of the highlighted entry. In the mod menu, describes the highlighted setting.
- U: Announce which unlocked jobs can equip the highlighted item. Works in the item menu and in shops. Silent for consumables and key items.
- Shift+I: Read controls tool-tips on screens that have them.
- Ctrl+Arrow keys: Teleport next to the selected destination (Ctrl+Up = north of it, etc.)

### Waypoints (field only)

- , and .: cycle between waypoints
- Shift+, and Shift+.: cycle between waypoint categories
- /: pathfind to waypoint
- Shift+/: add waypoint at player position
- Ctrl+.: Rename current waypoint
- Ctrl+/: delete waypoint
- Ctrl+Shift+/: clear all waypoints for the current map, requires pressing twice in rapid succession.

### Other toggles

- F5: Cycle enemy HP display between numbers, percentage and hidden. Available on the field and in field menus — set it before combat; it is not available during battle or on the title screen.
- F7: Toggle Auto Detail. When on, descriptions announce automatically as you move through items, magic, equipment and shops, in and out of battle. When off, use I to read them on demand. Works anywhere, including battle.
- F8: Open the mod menu. Available on the field and in field menus; not available during battle or on the title screen. Up and down arrows move through settings, left and right adjust, Enter toggles, I describes the highlighted setting, Escape or F8 closes.

### Battle results screen

- L: Read individual character exp gained, exp tnl and ABP to next job level

### When on a character's status screen

- up and down arrows (or W and S) read through statistics.
- Shift plus arrows: jumps between groups, character info, vitals, statistics, combat statistics, progression.
- control plus arrows: jump to beginning or end of statistics screen.
- Switching to the next or previous character reads the new character.

The bestiary detail screen uses the same keys, starting with the monster's name.

### Controls list (Configuration, Gamepad or Keyboard Controls)

- up and down arrows (or W and S) step through the controls one at a time.
- control plus arrows: jump to the first or last control.

### Game controller

In menus, shops and battle, the right stick mirrors the keyboard detail keys:

- Right stick up: same as I — read the description of what is highlighted
- Right stick down: same as Shift+I — read control tool-tips
- Right stick left: same as U — announce which jobs can equip the highlighted item
- D-pad or left stick up and down: step through the status screen, the bestiary detail screen and the Controls list.

On the field the right stick drives the entity scanner instead (up/down cycle entities, left/right cycle categories), the D-pad cycles waypoints (up/down) and waypoint categories (left/right), and the left trigger pathfinds to the last selected target (or restarts the beacon when audio beacons are on).

### Mod controller

- Back/Select/View: Mod mode
- Start/Menu: Mod menu, with the same availability as F8. D-pad or left stick up and down navigate, left and right adjust, A/Cross toggles, right stick up describes the highlighted setting, B/Circle or Start closes.

#### Mod mode combos (press the mod button, then one of the following)

- On the battle results screen, Circle/B: open the battle results log — the controller equivalent of L. Circle also closes it again.
- In dialogue, Square/X: repeat the current line.
- In battle, Square/X: read party HP.
- On the field, Square/X reads Gil, Triangle/Y reads your location, Cross/A reads walk, run or vehicle, and the right stick teleports.
- In battle or dialogue, right stick down while in mod mode lists what the current buttons do (on the field the right stick teleports instead). The mod button cancels mod mode.

#### Stick clicks (L3 and R3)

Stick Click Normalization in the mod menu decides what the stick clicks do. It is off by default.

- L3 + R3 together, on the field: turn Stick Click Normalization on or off, whichever way it is set. Press both sticks in at once. You hear "Stick click normalization on" or "off", and nothing else happens.
- On the field, a single stick click acts when you let go of it, so that it can be part of the L3 + R3 chord.
- Normalization off: L3 toggles beacon navigation and R3 toggles the pathfinding filter.
- Normalization on: L3 and R3 go to the game. L3 toggles walk/run and R3 toggles random encounters. The mod toggles move to mod mode: press Back/Select, then L3 for beacon navigation or R3 for the pathfinding filter.
- Off the field, a stick click goes straight to the game.
