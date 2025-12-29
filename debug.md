# Debug History - FF5 Screen Reader Mod

## Main Menu Magic/Spell List Issue (2024-12-28)

### Problem Summary

The main menu magic spell list (viewing spells after selecting a magic category like White Magic) does not announce spell names when navigating. Battle magic menu works correctly.

### What Works vs What Doesn't

| Feature | Status | Controller/Method |
|---------|--------|-------------------|
| Battle magic selection | **WORKS** | `BattleQuantityAbilityInfomationController.SelectContent(Cursor, WithinRangeType)` |
| Magic category selection (White Magic, Black Magic, etc.) | **WORKS** | `AbilityCommandController.SelectContent(int index)` |
| Ability equip menu | **WORKS** | `AbilityChangeController.SelectContent` with `TargetData.IsFocus` |
| Job selection menu | **WORKS** | `JobChangeWindowController.SelectContent` |
| **Main menu spell list** | **BROKEN** | `AbilityContentListController` - patches don't fire |

### Investigation Findings

#### AbilityContentListController Structure (Il2CppSerial.FF5.UI.KeyInput)

Located at dump.cs line 285082. Key members:

```
Fields:
- private int <SelectedIndex>k__BackingField; // 0x18
- private OwnedAbility <SelectedOwnedAbility>k__BackingField; // 0x20
- private List<BattleAbilityInfomationContentController> contentList; // 0x50
- public Action<int> OnSelect; // 0x58
- public Action OnSelected; // 0x60
- private List<OwnedAbility> abilityList; // 0x70

Methods:
- public void SelectContent(int index) { } // Line 285170
- private void SelectContent(Cursor targetCursor, WithinRangeType type, bool pageSkip) { } // Line 285236 - PRIVATE
- public void set_SelectedIndex(int value) { } // Line 285142
- private void set_SelectedOwnedAbility(OwnedAbility value) { } // Line 285150
```

#### AbilityWindowController States

The main ability window has these states (line 286256):
- None = 0
- MagicList = 1 (selecting magic categories - this works)
- UseList = 2 (browsing spells within category - this is broken)
- UseTarget = 3
- Command = 4

### Attempted Fixes and Failures

#### Attempt 1: Patch SelectContent(int index)

```csharp
[HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "SelectContent", new Type[] { typeof(int) })]
```

**Result:** Failed with "Ambiguous match" error because there are two SelectContent methods:
- `public void SelectContent(int index)`
- `private void SelectContent(Cursor, WithinRangeType, bool)`

Even with explicit type array, Harmony couldn't disambiguate properly.

#### Attempt 2: Patch SelectContent without type array

```csharp
[HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "SelectContent")]
```

**Result:** Same ambiguous match error.

#### Attempt 3: Patch SelectContent with two parameters

```csharp
[HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "SelectContent",
    new Type[] { typeof(int), typeof(CustomScrollView.WithinRangeType) })]
```

**Result:** Failed - method not found. The KeyInput version only has `SelectContent(int index)` as public, not the two-parameter version.

Error: `AccessTools.DeclaredMethod: Could not find method for type Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController and name SelectContent and parameters (int, Il2CppLast.UI.CustomScrollView+WithinRangeType)`

#### Attempt 4: Patch set_SelectedIndex

```csharp
[HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "set_SelectedIndex")]
```

**Result:** Game crashed on startup. This approach also failed previously for the job menu because SelectedIndex doesn't update properly when lists are modified.

### Key Differences: Touch vs KeyInput Namespaces

There are TWO versions of AbilityContentListController:

1. **Touch version** (line 280592): `Serial.FF5.UI.Touch`
   - `SelectContent(int index, WithinRangeType type = 0)` - TWO params
   - `SelectContent(Cursor, int index, WithinRangeType type)` - protected

2. **KeyInput version** (line 285082): `Serial.FF5.UI.KeyInput`
   - `SelectContent(int index)` - ONE param only
   - `SelectContent(Cursor, WithinRangeType, bool)` - PRIVATE

The KeyInput version's public SelectContent only takes a single int parameter, and Harmony can't disambiguate it from the private version.

### Why SelectContent Might Not Be Called

Looking at the UseListInit lambda callbacks (line 286447):
- `<UseListInit>b__0(int index)` - receives index
- `<UseListInit>b__3(int index)` - another index callback

The selection might happen through the `OnSelect` Action<int> callback (line 285097) rather than calling SelectContent directly. The flow appears to be:

1. User navigates with arrow keys
2. Cursor updates its index
3. `OnSelect` callback is invoked with new index
4. AbilityWindowController's UseListInit callback receives the index

SelectContent may only be called during initial list setup, not during navigation.

### Current Solution: Generic Cursor Navigation

Removed the skip for ability/job menus from `CursorNavigationPatches.cs` so the generic `MenuTextDiscovery.WaitAndReadCursor()` will attempt to find spell names via UI hierarchy text discovery.

Changes made in CursorNavigationPatches.cs (lines 173-183, 349-359, 525-535, 701-711):

```csharp
// REMOVED this skip:
// Skip if this is job/ability menu navigation (handled by JobAbilityPatches)
parent = __instance.transform.parent;
while (parent != null)
{
    string parentName = parent.name.ToLower();
    if (parentName.Contains("ability") || parentName.Contains("job"))
    {
        return;
    }
    parent = parent.parent;
}

// REPLACED with comment:
// NOTE: Job/ability menu skip removed - letting generic cursor navigation handle spell list
// since the controller-based patches aren't working for main menu magic spell list
```

### Files Modified

| File | Changes |
|------|---------|
| `Patches/JobAbilityPatches.cs` | Disabled AbilityContentListController patch, added comment explaining issue |
| `Patches/CursorNavigationPatches.cs` | Removed ability/job menu skip from all 4 cursor patches |

### Potential Future Approaches

If generic cursor navigation doesn't work, consider:

1. **Patch UpdateController method** - Called during navigation updates
2. **Patch a cursor-related method** - Like `SetCursor` or `FocusSelectCursor`
3. **Look at the view layer** - `BattleAbilityInfomationContentController` has `Data` property with OwnedAbility
4. **Find where OnSelect is invoked** - Patch that invocation point

### Reference: Working Battle Patch

The battle magic patch in `BattleCommandPatches.cs` works because it patches a different controller with different signature:

```csharp
[HarmonyPatch(typeof(BattleQuantityAbilityInfomationController),
    nameof(BattleQuantityAbilityInfomationController.SelectContent),
    new Type[] { typeof(Il2CppLast.UI.Cursor), typeof(CustomScrollView.WithinRangeType) })]
public static class BattleQuantityAbilityInfomationController_SelectContent_Patch
{
    public static void Postfix(BattleQuantityAbilityInfomationController __instance,
        Il2CppLast.UI.Cursor targetCursor)
    {
        int index = targetCursor.Index;
        var contentList = __instance.contentList;
        var content = contentList[index];
        var abilityData = content.Data; // OwnedAbility
        // ... announce ability name
    }
}
```

This works because:
1. `BattleQuantityAbilityInfomationController.SelectContent` takes `(Cursor, WithinRangeType)` - clear signature
2. The method IS called during battle navigation
3. We get the index from `targetCursor.Index` and data from `contentList[index].Data`

### Solution Found: Patch SetCursor Instead (2024-12-28)

The solution was to patch `SetCursor` instead of `SelectContent`. This private method has a unique 4-parameter signature that Harmony can match without ambiguity.

#### Working Patch

```csharp
[HarmonyPatch(typeof(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController), "SetCursor",
    new Type[] { typeof(GameCursor), typeof(bool), typeof(CustomScrollView.WithinRangeType), typeof(bool) })]
public static class AbilityContentListController_SetCursor_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Il2CppSerial.FF5.UI.KeyInput.AbilityContentListController __instance,
        GameCursor targetCursor, bool isScroll, CustomScrollView.WithinRangeType type, bool pageSkip)
    {
        int index = targetCursor.Index;
        var abilityList = __instance.AbilityList;
        var ability = abilityList[index];
        // ... announce ability name
    }
}
```

#### Why SetCursor Works

1. `SetCursor(Cursor, bool, WithinRangeType, bool)` has a unique signature - no overload ambiguity
2. Called during cursor movement in the spell list
3. Provides access to `AbilityList` containing all spells
4. `targetCursor.Index` gives the selected position

#### Additional Fixes Required

1. **Duplicate announcement prevention**: Added skip for `ability_command` parent in `CursorNavigationPatches.cs` to prevent both controller patch AND generic cursor navigation from announcing.

2. **Icon tag stripping**: Spell names contain icon tags like `<IC_WMGC>` (White Magic icon). Strip with regex:
   ```csharp
   abilityName = Regex.Replace(abilityName, @"<[^>]+>", "").Trim();
   ```

3. **Learned status check**: Use `OwnedAbility.SkillLevel` - if 0, ability is not learned:
   ```csharp
   if (ability.SkillLevel <= 0)
   {
       announcement += ", Not learned";
   }
   ```

4. **Empty slot handling**: Check for null ability and announce "Empty"

#### Final Announcement Format

- Learned spell: `"Cure, MP 4"`
- Unlearned spell: `"Cura, MP 9, Not learned"`
- Empty slot: `"Empty"`

### Files Modified (Final Solution)

| File | Changes |
|------|---------|
| `Patches/JobAbilityPatches.cs` | Added `AbilityContentListController_SetCursor_Patch` with learned status and empty slot handling |
| `Patches/CursorNavigationPatches.cs` | Added skip for `ability_command` and `command_window` parents to prevent duplicate announcements |

### Key Learnings

1. When `SelectContent` has ambiguous overloads, look for other methods in the navigation chain (`SetCursor`, `UpdateController`, etc.)
2. Private methods can still be patched if they have unique signatures
3. The `AbilityList` property provides direct access to spell data without needing to traverse view hierarchies
4. `OwnedAbility.SkillLevel` indicates learned status (0 = not learned)
