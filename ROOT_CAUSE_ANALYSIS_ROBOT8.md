# Root Cause Analysis: Why Robot_8 Was Circling

## The Circular Loop Pattern

Looking at the `godot_run.log`, Robot_8 gets stuck at nearly identical coordinates **9+ times**:

```
Attempt 1: (1.4152484, -1.41446)
Attempt 2: (1.415022, -1.41442)     ← Position hasn't changed much
Attempt 3: (1.4146037, -1.4162395)  ← Still at same spot
Attempt 4: (1.4400576, -1.4313653)  ← Slightly moved but still in boundary area
Attempt 5: (1.4146535, -1.408159)   ← Back to original area
Attempt 6: (1.4028878, -1.3835795)  ← Bouncing around ~1.4, -1.4
Attempt 7: (1.4081764, -1.4099814)  ← Same area again
Attempt 8: (1.4100071, -1.4414474)  ← And again
Attempt 9: (1.4149721, -1.4115856)  ← Still trapped
```

**All within ±0.04 units of (1.414, -1.414)**

## Why This Happened

### 1. Location: Sector 7 Boundary
Robot_8's assigned sector is **sector 7 (5.50 to 6.28 rad)**, with home at **(1.414, -1.414)**.

Looking at robot spawn logs:
```
[Director] Vehicle spawned with dig sector 0.00 to 0.79 rad      → Robot_1 @ (2.0, 0.0)
[Director] @CharacterBody3D@34 spawned with dig sector 0.79 to 1.57 rad → Robot_2 @ (1.4, 1.4)
...
[Director] @CharacterBody3D@100 spawned with dig sector 5.50 to 6.28 rad → Robot_8 @ (1.4, -1.4)
```

The point **(1.414, -1.414)** is **exactly at the home position** = sector corner/edge.

### 2. What RobotCoordinator.GetBestDigPoint() Returns
The system samples the sector for the highest point:
```csharp
for (int a = 0; a < samples; a++)  // Sample angle in sector
{
  for (int r = 1; r <= 5; r++)     // Sample radius in sector
  {
    // This often selects the outermost radius at sector boundary
  }
}
```

**Result**: Frequently selects points **at or near the sector boundary** like (1.414, -1.414)

### 3. Why It Gets Stuck There

The location (1.414, -1.414) has characteristics that cause entrapment:

1. **Sector Edge**: Reeds-Shepp paths may struggle with paths that go to boundary edges
2. **Potential Ditch/Dips**: Height variations at sector boundaries can create areas where:
   - Robot enters but can't maneuver out (especially with limited turn radius ~2m)
   - Vehicle gets tilted on uneven terrain
   - Reeds-Shepp path planning can't find valid exit paths
3. **Collision Avoidance**: Other robots might be preventing alternative escape paths

### 4. The Recovery Loop

**Frame-by-frame flow:**

```
CYCLE 1:
├─ Idle: Select dig target
├─ GetBestDigPoint → (1.414, -1.414) [sector boundary]
├─ Claim site ✓
└─ Plan path to (1.414, -1.414)

MovingToDig:
├─ Frame 1-60: Moving toward target
├─ Frame 61+: Still <0.3m from home - GET STUCK
└─ IsStuck() → TRUE

RECOVERY ATTEMPT #1 (Level 1):
├─ GD.Print "LEVEL 1 RECOVERY: Try alternative"
├─ Release claim ✓
└─ NEXT FRAME CALLS PlanAndGoOnce() AGAIN

CYCLE 2:
├─ Idle: Select dig target again
├─ GetBestDigPoint → (1.414, -1.414) [SAME SPOT!]  ← WHY?
│   Because it's still the highest point in the sector
│   The terrain hasn't changed
│   The location hasn't been marked as "don't dig here"
├─ Claim site ✓
└─ Plan path to (1.414, -1.414) AGAIN

MovingToDig:
├─ Get stuck AGAIN (61 frames)
└─ IsStuck() → TRUE

RECOVERY ATTEMPT #2 (Level 2):
├─ GD.Print "LEVEL 2 RECOVERY: Force home!"
├─ _returningHome = true ✓
└─ NEXT FRAME: Path home and dump

DUMP & RESET:
├─ Arrive home
├─ Call OnArrival()
├─ RESET: _consecutiveStuckRecoveries = 0  ← BUG!
└─ Ready for next cycle

CYCLE 3:
├─ Idle: Select dig target
├─ GetBestDigPoint → (1.414, -1.414) [SAME SPOT AGAIN!]
├─ _consecutiveStuckRecoveries = 0 (reset after dump!)
│   So recovery attempt counter starts over
└─ ... INFINITE LOOP ...
```

## Why the Fix Works

### Before (Circular):
```
Select (1.414) → Stuck → Try alternative → Still leads to (1.414)
→ Stuck → Force home → Dump → RESET COUNTER → Select (1.414) again
```

### After (Escape):
```
Select (1.414) → Stuck → Try alternative
→ Stuck again → BLACKLIST (1.414)
→ Force home immediately
→ Dump at home
→ NEXT CYCLE: GetBestDigPoint avoids blacklist
→ Select (different point)
→ Successfully dig new location ✓
```

## Key Insight

**The system had no "memory" of failure.**

- Recovery Level 1-3 forced home but didn't mark the location as "bad"
- After dumping, the recovery counter reset to 0
- Next cycle, the system would happily target the same problematic location again
- This created a **guaranteed infinite loop at sector boundaries**

## Why This Primarily Affected Robot_8

Robot_8's sector (5.50 to 6.28 rad) has special characteristics:
- It's positioned at the **diagonal sector boundary**
- The sampling algorithm frequently selects **boundary-adjacent points**
- Those boundary points appear to have terrain conditions that cause stuck events
- Unlike other robots (sectors 0-7 which have more "interior" points), Robot_8's sector is more likely to sample near the home position

**Other robots** (Robot_1, Robot_2, etc.) also experienced stuck events, but:
- They recovered successfully and moved to different targets
- Their sectors had more viable alternative dig points
- They didn't get locked in a single-location loop

## The Fix Principle

**"Remember what failed, and don't try it again for 5 seconds"**

This simple addition breaks the infinite loop by:
1. Detecting repeated failures at the same location
2. Adding that location to a "don't dig here" list
3. Forcing the system to pick a different target
4. Allowing retry after a grace period (healing effect)

This is the key difference between a system that gets stuck infinitely vs. one that recovers autonomously.
