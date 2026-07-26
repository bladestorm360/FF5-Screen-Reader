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

Game:

WASD or arrow keys: movement

Enter: Confirm

Backspace: cancel

Mod:

J and L or [ and ]: cycle destinations in pathfinder

Shift+J and L or - and =: change destination categories

\ or p: get directions to selected destination

Shift+\ or P: Toggle pathfinding filter so that not all destinations are visible, just ones with a valid path.

K: announce currently selected destination.

Shift+K: Reset to all category

G: Announce current Gil

M: Announce current map.

H: In battle, announce character hp, mp, status effects.

I: Read the description of whatever is highlighted. In configuration menu accessible from tab menu, read description of highlighted setting. In jobs menu, read description of highlighted job. In spell or ability menus, read description of highlighted spell or ability. In shop menus, reads description of highlighted item. In item menu, reads the description of the highlighted item.

U: Announce which unlocked jobs can equip the highlighted item. Works in the item menu and in shops. Silent for consumables and key items.

Shift+i: Read controls tool-tips on screens that have them.

F5: Cycle enemy HP display between numbers, percentage and hidden. Available on the field and in field menus — set it before combat; it is not available during battle or on the title screen.

F7: Toggle Auto Detail. When on, descriptions announce automatically as you move through items, magic, equipment and shops. When off, use I to read them on demand. Works anywhere, including battle.

F8: Open the mod menu. Available on the field and in field menus; not available during battle or on the title screen.

Battle Results Screen:

l: Read individual character exp gained, exp tnl and ABP to next job level

When on a character's status screen:

up and down arrows read through statistics.

Shift plus arrows: jumps between groups, character info, vitals, statistics, combat statistics, progression.

control plus arrows: jump to beginning or end of statistics screen.

Waypoint system:

, and .: cycle between waypoints

shift + , and .: cycle between waypoint categories

/: pathfind to waypoint

shift + /: add waypoint at player position

Control+.: Rename current waypoint

control plus /: delete waypoint

Controller:

In menus, shops and battle, the right stick mirrors the keyboard detail keys:

right stick up: same as I — read the description of what is highlighted

right stick down: same as Shift+I — read control tool-tips

right stick left: same as U — announce which jobs can equip the highlighted item

On the field the right stick drives the entity scanner instead (up/down cycle entities, left/right cycle categories).

Start: open the mod menu, with the same availability as F8.

Mod button (Back/Select/View) puts the controller into mod mode for one press. What the next button does depends on where you are:

On the battle results screen, Circle/B: open the battle results log — the controller equivalent of l. Circle also closes it again.

In dialogue, Square/X: repeat the current line.

In battle, Square/X: read party HP.

On the field, Square/X reads Gil, Triangle/Y reads your location, and the right stick teleports.

Right stick down while in mod mode lists what the current buttons do. The mod button cancels mod mode.

control plus shift plus /: clear all waypoints, requires pressing twice in rapid succession.
