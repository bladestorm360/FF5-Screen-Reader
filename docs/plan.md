# FF5 Screen Reader Mod - Feature Plans

## Vehicle Landing Zone Announcements (Complete)

### Overview

Add announcements when a player in a vehicle enters a zone where they can land/dismount. This helps blind players know when they can safely exit their vehicle without guessing or trial-and-error.

This feature has been successfully implemented in FF4 and is being ported to FF5.

### Behavior

| Event | Announcement |
|-------|--------------|
| Enter landable zone | "Can land" |
| Leave landable zone | (silent) |
| Successfully land | Already handled by "On foot" announcement |

- **Non-interrupting**: Won't cut off other important announcements
- **No vehicle type declared**: Simple "Can land" for all vehicles
- **Only when in vehicle**: Silent when walking/on foot

### Technical Approach

The game already handles terrain checking internally. When the player moves over terrain where landing is possible, the game calls `MapUIManager.SwitchLandable(bool landable)` to show/hide the landing UI guide. We patch this method to announce state changes.

**Key Classes (verified in FF5 dump.cs):**
- `MapUIManager.SwitchLandable(bool landable)` - Line 338698 - Called when landing state changes
- `MapUIManager` class at line 338068 in namespace `Last.Map`

### Implementation

#### File: `Patches/VehicleLandingPatches.cs` (New)

```csharp
using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Map;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Announces when player enters a zone where vehicle can land.
    /// Patches MapUIManager.SwitchLandable which is called by the game
    /// when the landing state changes based on terrain under the vehicle.
    /// </summary>
    [HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.SwitchLandable))]
    public static class MapUIManager_SwitchLandable_Patch
    {
        private static bool lastLandableState = false;

        [HarmonyPostfix]
        public static void Postfix(bool landable)
        {
            try
            {
                // Only announce when in a vehicle (not on foot)
                if (MoveStateHelper.IsOnFoot())
                    return;

                // Only announce when entering landable zone (false -> true)
                if (landable && !lastLandableState)
                {
                    Core.FFV_ScreenReaderMod.SpeakText("Can land", interrupt: false);
                }

                lastLandableState = landable;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Landing] Error in SwitchLandable patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state when leaving vehicle or changing maps
        /// </summary>
        public static void ResetState()
        {
            lastLandableState = false;
        }
    }
}
```

### Key Differences from FF4 Implementation

| Aspect | FF4 | FF5 |
|--------|-----|-----|
| Namespace | `FFIV_ScreenReader` | `FFV_ScreenReader` |
| Main class | `FFIV_ScreenReaderMod` | `FFV_ScreenReaderMod` |
| MoveStateHelper location | Same | Same |
| MapUIManager | Same API | Same API |

### Files to Create

| File | Action |
|------|--------|
| `Patches/VehicleLandingPatches.cs` | Create new file |

### Testing Checklist

- [ ] Board ship, sail over water - no announcement
- [ ] Sail to shore - "Can land" announced once
- [ ] Continue along shore - no repeated announcements
- [ ] Sail back to deep water - silent
- [ ] Return to shore - "Can land" announced again
- [ ] Disembark successfully - "On foot" announced (existing)
- [ ] Repeat for chocobo and airship

### FF5 Vehicles

FF5 has the following vehicles that should trigger landing announcements:
- **Ship** (pirate ship) - Can land at docks/shores
- **Chocobo** (yellow, black) - Can dismount in appropriate terrain
- **Airship** - Can land on flat terrain
- **Submarine** - Can surface at certain locations

### Why This Approach

1. **Minimal code**: Single patch point, ~35 lines
2. **No polling**: Game tells us when state changes
3. **Reliable**: Uses same logic as visual landing guide
4. **Works for all vehicles**: No vehicle-specific handling needed
5. **Proven**: Already working in FF4

---

## Implementation Status

Feature implemented and deployed successfully.
