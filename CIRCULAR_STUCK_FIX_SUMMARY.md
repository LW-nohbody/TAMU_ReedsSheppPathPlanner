# Circular Stuck Recovery Pattern - Fix Summary

## Problem Identified

**Log Analysis**: Robot_8 was stuck in an infinite loop, repeatedly getting stuck at position **(1.414, -1.414)** at the boundary of sector 7.

### Stuck Events in Log
```
[Robot_8] STUCK for 61 cycles at (1.4152484, 0, -1.41446). Attempting recovery #1...
[Robot_8] STUCK for 61 cycles at (1.415022, 0, -1.41442). Attempting recovery #2...
[Robot_8] STUCK for 61 cycles at (1.4146037, 0, -1.4162395). Attempting recovery #1...
[Robot_8] STUCK for 61 cycles at (1.4400576, 0, -1.4313653). Attempting recovery #2...
[Robot_8] STUCK for 61 cycles at (1.4146535, 0, -1.408159). Attempting recovery #3...
[Robot_8] STUCK for 61 cycles at (1.4028878, 0, -1.3835795). Attempting recovery #4...
... 9+ total stuck events at nearly identical location ...
```

### Root Cause

1. **Target Selection Issue**: Robot kept selecting the same high-point dig site at the sector boundary
2. **Insufficient Recovery**: After dumping, recovery counter reset, allowing retry of the same site
3. **No Memory of Failures**: System had no way to remember "tried this site, it doesn't work"
4. **Circular Pattern**: Recovery attempts (Level 1→2→3) just forced home, then next cycle returned to same site

## Solution Implemented

### 1. Site Blacklisting System
Added a blacklist mechanism in `VehicleBrain.cs`:
```csharp
private readonly List<Vector3> _blacklistedSites = new();
private int _blacklistDecayCounter = 0;
```

**How it works:**
- When a site causes 3+ consecutive stuck events, it's added to the blacklist
- Blacklisted sites are avoided when selecting new dig targets
- Blacklist decays after 5 seconds (300 frames @60fps), allowing retry

### 2. Aggressive Early Recovery
Changed recovery threshold from 3+ to 2+ stuck events:
- **Level 1**: Release claim, try alternative
- **Level 2**: Force home (was Level 2 before, now triggers sooner)
- **Level 3+**: Blacklist location + force home + emergency reset

### 3. Target Validation
Before committing to a dig target, system now checks:
```csharp
// Check if target is blacklisted
bool targetIsBlacklisted = false;
foreach (var blacklistedSite in _blacklistedSites)
{
  if (targetPos.DistanceTo(blacklistedSite) < 0.5f)
  {
    targetIsBlacklisted = true;
    GD.Print($"[{_spec.Name}] Target {targetPos} is blacklisted - going home instead");
    break;
  }
}

if (targetIsBlacklisted)
{
  _returningHome = true;
  targetPos = _homePosition;
}
```

### 4. Stuck Location Tracking
When stuck is detected at the same location, immediately blacklist it:
```csharp
if (_consecutiveStuckRecoveries > 0 && _currentTarget.DistanceTo(curPos) < 1.0f)
{
  GD.PrintErr($"[{_spec.Name}] BLACKLISTING site {curPos} - repeated stuck events!");
  _blacklistedSites.Add(curPos);
  _returningHome = true;  // Force home immediately
}
```

## Expected Behavior After Fix

### Robot_8 Scenario (Previously Stuck in Loop)
```
1. Targets (1.414, -1.414) for digging
2. Gets stuck (0m moved in 60 frames)
   → LEVEL 1 RECOVERY: Try alternative
3. Alternative also leads to same area, gets stuck again
   → LEVEL 2 RECOVERY: Force home (after 2 attempts)
4. Blacklist (1.414, -1.414)
5. Next cycle: Coordinator returns different dig point (not blacklisted)
6. Successfully digs and returns home
7. After 5 seconds: Blacklist expires, can retry if needed
```

### Metrics Improvement
| Metric | Before | After |
|--------|--------|-------|
| Consecutive stuck events | 9+ | ~2-3 before blacklist |
| Time stuck at one location | Infinite loop | <10 seconds |
| Recovery success rate | Low | High (forced home) |
| Robots completing sectors | Some fail | All should complete |

## Code Changes

**File**: `3d/Scripts/SimCore/Godot/VehicleBrain.cs`

**Changes:**
1. Added `_blacklistedSites` list to track problematic dig sites
2. Added `_blacklistDecayCounter` for time-based forgetting
3. Enhanced `IsStuck()` method to populate blacklist
4. Updated `PlanAndGoOnce()` to:
   - Check if target is blacklisted before claiming
   - Force home if blacklisted site is selected
   - Decay blacklist periodically
5. Improved recovery levels to be more aggressive early

**Build Status**: ✅ 0 warnings, 0 errors

## Testing Strategy

1. **Observe Robot_8 in next run**: Should NOT get stuck at (1.414, -1.414) anymore
2. **Check logs for**: Blacklist messages and successful recovery attempts
3. **Verify all robots**: Complete their sectors without circular patterns
4. **Monitor stuck events**: Should see <1% of frames spent stuck vs previous runs

## Log Signals to Look For

**Success indicators:**
```
[Robot_8] BLACKLISTING site (1.414, -1.414) - repeated stuck events!
[Robot_8] Clearing blacklist - 1 sites removed (after 5 seconds)
[Robot_8] Target (1.414, -1.414) is blacklisted - going home instead
[Robot_8] LEVEL 2 RECOVERY: Forcing return home...
[Robot_8] ✓✓✓ DUMPED X.XXm³ at (1.4, -1.4) - Successfully escaped loop!
```

**Failure indicators:**
```
[Robot_8] STUCK for 61 cycles at (1.414, -1.414) x 10+ times
```

## Files Modified
- `3d/Scripts/SimCore/Godot/VehicleBrain.cs` - Added blacklist system
- No changes needed to DigSim3D (separate codebase)

## Future Improvements
1. **Persist blacklist across dumps**: Currently clears after 5 seconds
2. **Adaptive recovery times**: Increase grace period for persistent problem areas
3. **Sector-wide avoidance**: If >50% of sector boundary has issues, avoid boundaries
4. **Terrain analysis**: Pre-scan sector edges for steep terrain before attempting
