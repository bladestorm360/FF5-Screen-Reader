FF5-screen-reader:
Purpose:

Adds NVDA output, pathfinding, sound queues and other accessibility aides to Final Fantasy V Pixel Remaster.

Install:

Create an account at store.steampowered.com, login, join steam.
Once account is created, install steam download app (should be prompted to do so after account creation.)
Log into desktop app.
to purchase games, the easiest way is to use the web interface. You can search for a game when logged into the browser, purchase it there and will be asked if you want to install your games, which opens the desktop app to finish installation.
Ensure you purchase Final Fantasy V, the page should mention being remastered in the description. Do not buy Final Fantasy V Old Ver.
Install MelonLoader into game's installation directory. Ensure nightly builds are enabled.
https://melonloader.co/download.html
Copy NVDAControllerClient64.dll and tolk.dll into installation directory with game executable, usually c:\Program Files (x86)\Steam\Steamapps\common\Final Fantasy V PR.
If you created a steam library on another drive, the path will be Drive Letter\Path to steam library\SteamLibrary\steamapps\common\Final Fantasy V PR.
FFV_screenreader.dll   goes in MelonLoader/mods folder.
waypoints.json goes in MelonLoader/UserData folder.

Keys:
J and L or [ and ]: cycle destinations in pathfinder
Shift+J and L or - and =: change destination categories
\ or p: get directions to selected destination
WASD or arrow keys: movement
Enter: Confirm
Backspace: cancel
G: Announce current Gil
M: Announce current map.
H: In battle, announce character hp, mp, status effects. When flying airship, announce current heading.
I: In configuration  menu accessible from tab menu and jobs menu, read description of highlighted setting or job. In shop menus, reads description of highlighted item.
When on a character's status screen:
up and down arrows read through statistics.
Shift plus arrows: jumps between groups, character info, vitals, statistics, combat statistics, progression.
control plus arrows: jump to beginning or end of statistics screen.
Waypoint system:
, and .: cycle between waypoints
shift + , and .: cycle between waypoint categories
/: pathfind to waypoint
shift + /: add waypoint at player position
control plus /: delete waypoint
control plus shift plus /: clear all waypoints, requires pressing twice in rapid succession.

Known Issues:
Pathfinding does not work correctly when sailing the pirate ship. If you get stuck early in the game between Tule and the wind shrine, use waypoint 1 (a docking point near the wind shrine) and waypoint 2 (A landmark after which tule becomes findable on the destination finder to get there.) This is the fix for now, waypoints.json can be updated as needed for other sticking points in progression.
Status reader is not reading commands list of the selected character.
Victory screen after battle may speak when a character has learned an ability from job level up, but not which ability was learned.
I in job menu does not yet speak which weapons a job can equip.
Waypoints are generically numbered (waypoint 1, waypoint2 etc.) Needs sa custom naming system to be more useful.
Waypoints placed from the pirate ship can only pathfind when on the pirate ship.