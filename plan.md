# FF5 Entity Name Translation - Implementation Plan

## Summary

MapNameResolver.cs is **already fully implemented** and the current map hotkey is **already working**. The only remaining task is to integrate MapNameResolver into MapExitEntity so map exits announce their destination in English.

## Current Status

✅ **MapNameResolver.cs** - Fully ported from FF6, uses Map/Area master data + MessageManager
✅ **Current map hotkey** - Already implemented and working
✅ **Treasure chests** - Already show "Opened"/"Unopened" status
❌ **Map exits** - MapExitEntity exists but doesn't use MapNameResolver yet
⚠️ **NPCs** - Character name translation skipped (can be handled separately later)
❌ **Doors** - Skipped (FF6 doesn't solve this either, requires manual observation)

## What Needs to Be Done

### Update MapExitEntity to Use MapNameResolver

**File:** `FF5/ff5-screen-reader/Field/NavigableEntity.cs` (lines 136-155)

**Current implementation:**
```csharp
public class MapExitEntity : NavigableEntity
{
    public int DestinationMapId => GameEntity?.Property?.TryCast<PropertyGotoMap>()?.MapId ?? -1;

    protected override string GetDisplayName()
    {
        return Name;  // Returns raw Japanese name
    }

    protected override string GetEntityTypeName()
    {
        return "Map Exit";
    }
}
```

**Required changes:**

1. Add using statement at top of file if not present:
   ```csharp
   using Il2CppLast.Map; // For PropertyGotoMap
   ```

2. Add DestinationName property:
   ```csharp
   public string DestinationName => MapNameResolver.GetMapExitName(
       GameEntity?.Property?.TryCast<PropertyGotoMap>()
   );
   ```

3. Update GetDisplayName() to include destination:
   ```csharp
   protected override string GetDisplayName()
   {
       if (!string.IsNullOrEmpty(DestinationName))
       {
           return $"{Name} → {DestinationName}";
       }
       return Name;
   }
   ```

**Expected behavior:**
- **Before:** "階段 (3.5 steps North) - Map Exit"
- **After:** "階段 → Wind Shrine 1F (3.5 steps North) - Map Exit"

## Implementation Steps

1. Open `FF5/ff5-screen-reader/Field/NavigableEntity.cs`
2. Check if `using Il2CppLast.Map;` exists at top, add if missing
3. Locate `MapExitEntity` class (lines 136-155)
4. Add `DestinationName` property after `DestinationMapId`
5. Update `GetDisplayName()` method to use DestinationName
6. Build and deploy using `ff5/ff5-screen-reader/build_and_deploy.bat`
7. Test by navigating to map exits (J/[ and L/] hotkeys)

**Time estimate:** ~10 minutes

## Testing

1. Launch game and navigate to an area with map exits (stairs, cave exits, etc.)
2. Use entity navigation (J/[ and L/] hotkeys) to cycle to map exits
3. Verify announcements include English destination names:
   - "Exit → Tycoon Castle 2F"
   - "Exit → Wind Shrine 1F"
   - "Stairs → World Map"
4. Verify fallback for unmapped areas shows "Exit → Map {id}"

## Files Involved

**To Modify:**
- `FF5/ff5-screen-reader/Field/NavigableEntity.cs` (MapExitEntity class only)

**Already Complete:**
- `FF5/ff5-screen-reader/Field/MapNameResolver.cs` (no changes needed)
- `FF5/ff5-screen-reader/Core/FFV_ScreenReaderMod.cs` (current map hotkey already works)

## Scope Exclusions

**NPCs:** Character name translation is more complex in FF5 (no P-codes like FF6). Can be handled in a separate task later if needed.

**Doors:** Translation requires manual observation and dictionary building. FF6 doesn't solve this either. Skipped for now.
