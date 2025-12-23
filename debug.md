# FF5 Screen Reader Debug History

## 2025-12-22: Complete Feature Port from FF6 (Navigation, Status, Job Menu, Timers)

### Overview
Implemented 6 major features by porting proven code from FF6 mod to FF5, enhancing navigation, character status, job menu usability, and timer detection.

### Problems Solved

1. **Map Navigation Missing**: M key did nothing, map transitions silent
2. **Character Status Missing**: H key did nothing in battle
3. **Job Menu Too Verbose**: Arrow keys announced name + level + stats + description all at once
4. **Timer Detection Missing**: No way to announce or control countdown timers

### Implementation Summary

#### Task 1-3: Map Navigation (M Key)

**Files Created:**
- `Field/MapNameResolver.cs` - Ported from FF6 with namespace changes only

**Files Modified:**
- `Core/FFV_ScreenReaderMod.cs`
  - `CheckMapTransition()`: Added map name announcement on transitions
  - `AnnounceCurrentMap()`: Replaced stub with working implementation

**Key Methods:**
```csharp
// MapNameResolver.cs
public static string GetCurrentMapName()
{
    var userDataManager = UserDataManager.Instance();
    int currentMapId = userDataManager.CurrentMapId;
    return TryResolveMapNameById(currentMapId);
}

private static string TryResolveMapNameById(int mapId)
{
    // Gets Map master data → Area master data → localized names
    var map = masterManager.GetList<Map>()[mapId];
    var area = masterManager.GetList<Area>()[map.AreaId];
    string areaName = messageManager.GetMessage(area.AreaName);
    string mapTitle = messageManager.GetMessage(map.MapTitle);
    return $"{areaName} {mapTitle}"; // e.g., "Karnak 2F"
}
```

**Pattern Used:**
- Uses game's localization system (`MessageManager.GetMessage()`)
- No hardcoded English strings
- Combines Area name + Map title for full context

**Result:**
- **M key**: Announces current map name
- **Map transitions**: Auto-announces "Entering [map name]"

---

#### Task 4-5: Character Status (H Key)

**Files Modified:**
- `Patches/BattleCommandPatches.cs`
  - Added `ActiveBattleCharacterTracker` static class
  - Updated `SetCommandData` patch to track active character

- `Core/FFV_ScreenReaderMod.cs`
  - `AnnounceAirshipOrCharacterStatus()`: Replaced stub with airship detection + character status fallback
  - Added `AnnounceCurrentCharacterStatus()`: New method for reading HP/MP/conditions

**Key Methods:**
```csharp
// Track active character for H key access
public static class ActiveBattleCharacterTracker
{
    public static OwnedCharacterData CurrentActiveCharacter { get; set; }
}

// BattleCommandSelectController_SetCommandData_Patch.Postfix
ActiveBattleCharacterTracker.CurrentActiveCharacter = data; // Track on each turn

// AnnounceCurrentCharacterStatus()
private void AnnounceCurrentCharacterStatus()
{
    var activeCharacter = ActiveBattleCharacterTracker.CurrentActiveCharacter;
    var param = activeCharacter.Parameter;

    // Build announcement
    statusParts.Add(characterName);
    statusParts.Add($"HP {param.CurrentHP} of {param.ConfirmedMaxHp()}");
    statusParts.Add($"MP {param.CurrentMP} of {param.ConfirmedMaxMp()}");

    // Add status conditions
    var conditionList = param.ConfirmedConditionList();
    foreach (var condition in conditionList)
    {
        string conditionName = messageManager.GetMessage(condition.MesIdName);
        conditionNames.Add(conditionName);
    }
}
```

**Pattern Used:**
- **Global state tracking** for H key access outside patches
- **Confirmed methods** (`ConfirmedMaxHp()`, `ConfirmedConditionList()`) get calculated values
- **Localized condition names** via MessageManager

**Result:**
- **H key**: Announces "Character name, HP X of Y, MP X of Y, [status conditions]"
- Falls back to "Not in battle" when not in battle
- Airship status deferred (TODO)

---

#### Task 6-9: Job Menu Enhancement (I Key for Details)

**Files Modified:**
- `Patches/JobAbilityPatches.cs`
  - Added `JobMenuTracker` static class (tracks menu state)
  - Modified `JobChangeWindowController_SelectContent_Patch` to announce only name+level
  - Added `JobDetailsAnnouncer` static class (announces stats+description)
  - Added `JobChangeWindowController_OnHide_Patch` (cleanup on menu close)

- `Core/InputManager.cs`
  - Updated I key handler to check `JobMenuTracker.IsJobMenuActive` first

**Key Methods:**
```csharp
// Track job menu state
public static class JobMenuTracker
{
    public static bool IsJobMenuActive { get; set; }
    public static JobChangeWindowController ActiveController { get; set; }
    public static int CurrentJobIndex { get; set; }
}

// JobChangeWindowController_SelectContent_Patch.Postfix (SIMPLIFIED)
JobMenuTracker.IsJobMenuActive = true;
JobMenuTracker.ActiveController = __instance;
JobMenuTracker.CurrentJobIndex = index;

// Announce ONLY name + level
string announcement = $"{jobName}, level {jobLevel}";

// JobDetailsAnnouncer.AnnounceCurrentJobDetails() (NEW)
string announcement = $"Strength {job.Strength}, Vitality {job.Vitality}, " +
                     $"Agility {job.Agility}, Magic {job.Magic}. {description}";

// Cleanup on menu close
[HarmonyPatch(typeof(JobChangeWindowController), "OnHide")]
JobMenuTracker.IsJobMenuActive = false;
```

**Pattern Used:**
- **State tracking** allows I key to access job data outside patch context
- **OnHide patch** prevents stale state when menu closes
- **Separation of concerns**: Arrow keys = quick browsing, I key = detailed info

**Result:**
- **Arrow keys**: "Job name, level X" (fast, concise)
- **I key in job menu**: "Strength X, Vitality X, Agility X, Magic X. [Description]"
- **I key outside job menu**: Still announces config tooltips (dual purpose)

---

#### Task 10-11: Timer Detection (T / Shift+T)

**Files Created:**
- `Patches/TimerPatches.cs` - Ported from FF6 with namespace changes only

**Files Modified:**
- `Core/InputManager.cs`
  - Updated T key handler to call `TimerHelper` methods

**Key Methods:**
```csharp
// Harmony prefix patch to freeze timers
[HarmonyPatch(typeof(Il2CppLast.Timer.Timer), nameof(Timer.Update))]
public static bool Prefix()
{
    return !TimerHelper.TimersFrozen; // Skip Update() when frozen
}

// Toggle freeze state
public static void ToggleTimerFreeze()
{
    timersFrozen = !timersFrozen;
    string message = timersFrozen ? "Timers frozen" : "Timers resumed";
    FFV_ScreenReaderMod.SpeakText(message, interrupt: true);
}

// Find and announce active timers
public static bool AnnounceActiveTimers()
{
    // Search for ScreenTimerController (on-screen UI timers)
    var screenTimers = Object.FindObjectsOfType<ScreenTimerController>();
    foreach (var timer in screenTimers)
    {
        if (timer.view.canvasGroup.alpha > 0) // Visible check
        {
            string minutes = timer.view.playTimeMinuteText.text;
            string seconds = timer.view.playTimeSecondText.text;
            announcement.Append(FormatTimeString(minutes, seconds));
        }
    }

    // Search for FieldGrobalTimer (field map timers)
    var fieldTimers = Object.FindObjectsOfType<FieldGrobalTimer>();
    // ... similar logic ...

    return timerCount > 0;
}

// Format time naturally
private static string FormatTimeString(string minutes, string seconds)
{
    // "5", "30" → "5 minutes 30 seconds"
    // "0", "45" → "45 seconds"
}
```

**Pattern Used:**
- **Harmony prefix patch** prevents native Update() from running (freezes timers)
- **FindObjectsOfType** searches entire scene for timer UI components
- **Visibility check** (canvasGroup.alpha > 0) only announces visible timers
- **Natural language formatting** ("X minutes Y seconds")

**Result:**
- **T key**: Announces "5 minutes 30 seconds" or "No active timers"
- **Shift+T**: Toggles freeze state, announces "Timers frozen" / "Timers resumed"
- Works for both on-screen timers and field map timers

---

### Architecture Patterns

#### 1. Global State Tracking for Hotkey Access

**Problem:** Hotkeys (H, I) need data that only exists in patch context.

**Solution:** Static tracker classes that patches update:

```csharp
// In patch file (BattleCommandPatches.cs)
public static class ActiveBattleCharacterTracker
{
    public static OwnedCharacterData CurrentActiveCharacter { get; set; }
}

// In patch postfix
ActiveBattleCharacterTracker.CurrentActiveCharacter = data;

// In FFV_ScreenReaderMod.cs (H key handler)
var character = ActiveBattleCharacterTracker.CurrentActiveCharacter;
```

**Used by:**
- `ActiveBattleCharacterTracker` (H key)
- `JobMenuTracker` (I key)

---

#### 2. Menu State Lifecycle Management

**Problem:** Menu state persists after menu closes, causing stale data.

**Solution:** OnHide patch clears state:

```csharp
[HarmonyPatch(typeof(JobChangeWindowController), "OnHide")]
public static void Postfix()
{
    JobMenuTracker.IsJobMenuActive = false;
    JobMenuTracker.ActiveController = null;
    JobMenuTracker.CurrentJobIndex = -1;
}
```

**Prevents:** "Job menu not active" errors, stale index reads.

---

#### 3. Porting Strategy (FF6 → FF5)

**Steps for engine-level code (timers, maps):**
1. Copy entire file from FF6
2. Change namespace: `FFVI_ScreenReader` → `FFV_ScreenReader`
3. Change mod calls: `FFVI_ScreenReaderMod.SpeakText()` → `FFV_ScreenReaderMod.SpeakText()`
4. Verify Il2CppLast.* classes exist (they do - shared engine)
5. Done (no game-specific changes needed)

**Works because:** Both games use identical Pixel Remaster engine.

---

### Files Summary

#### Created (2 files):
1. `Field/MapNameResolver.cs` - Map ID → localized name resolver
2. `Patches/TimerPatches.cs` - Timer detection and freeze patches

#### Modified (4 files):
1. `Core/FFV_ScreenReaderMod.cs`
   - CheckMapTransition() - announces map transitions
   - AnnounceCurrentMap() - M key implementation
   - AnnounceAirshipOrCharacterStatus() - H key implementation
   - AnnounceCurrentCharacterStatus() - HP/MP/status reader

2. `Patches/BattleCommandPatches.cs`
   - Added ActiveBattleCharacterTracker
   - Updated SetCommandData patch to track character

3. `Patches/JobAbilityPatches.cs`
   - Added JobMenuTracker
   - Simplified SelectContent patch (name+level only)
   - Added JobDetailsAnnouncer
   - Added OnHide patch

4. `Core/InputManager.cs`
   - Updated I key handler (job details vs config tooltip)
   - Updated T key handler (timer detection)

---

### Testing Checklist

**Map Navigation:**
- [ ] M key announces current map name (localized)
- [ ] Map transitions auto-announce "Entering [map name]"
- [ ] Works in towns, dungeons, world map

**Character Status:**
- [ ] H key announces HP/MP/conditions during battle
- [ ] H key says "Not in battle" when not in battle
- [ ] Status conditions are localized (not English hardcoded)

**Job Menu:**
- [ ] Arrow keys announce only "Job name, level X" (quick)
- [ ] I key announces stats + description (detailed)
- [ ] I key still works for config tooltips outside job menu
- [ ] Job menu state clears when closing menu

**Timer Detection:**
- [ ] T key announces active timers during timed events
- [ ] T key announces "No active timers" when none active
- [ ] Shift+T freezes timers and announces "Timers frozen"
- [ ] Shift+T again unfreezes and announces "Timers resumed"
- [ ] Timer countdown visually stops when frozen

---

### Build Result
✅ Successful compilation and deployment
- Output: `FFV_ScreenReader.dll`
- Deployed to: `D:\Games\SteamLibrary\steamapps\common\FINAL FANTASY V PR\Mods\`
- All 6 tasks completed
- Ready for testing

---

### Key Discoveries

1. **Il2CppLast.* classes are identical** between FF5 and FF6 (shared engine)
   - Timer.Timer, Map, Area, MessageManager all work identically
   - Only Il2CppSerial.FF5.* vs Il2CppSerial.FF6.* differ

2. **ConfirmedMaxHp() vs BaseMaxHp**
   - `BaseMaxHp` = raw stat without equipment
   - `ConfirmedMaxHp()` = calculated stat with equipment/buffs
   - Always use Confirmed methods for accurate values

3. **ConfirmedConditionList() vs CurrentConditionList**
   - `ConfirmedConditionList()` returns battle-active conditions
   - Filters out expired/irrelevant conditions

4. **OnHide vs OnDestroy**
   - OnHide fires when menu closes but object stays in memory
   - OnDestroy only fires when object is destroyed (unreliable)
   - Always use OnHide for cleanup

---

### Notes

- All features use game's localization system (no English hardcoded)
- Pattern follows FF6 proven implementations
- Code is maintainable: clear separation between quick vs detailed announcements
- Timer freeze is non-destructive (uses Harmony prefix, not native code modification)
