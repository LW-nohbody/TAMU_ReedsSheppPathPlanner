# Swarm-Based Dig Logic Proposal for DigSim3D

## Current Status

**3d/ Folder**: Enhanced system with pre-scanning, collision avoidance, stuck detection/recovery, and terrain visualization. **Robots do NOT get stuck frequently** (only 1 stuck event in 587-line log across entire simulation with 8 robots).

**DigSim3D/ Folder**: Main branch structure where robots are pure swarm agents that:
- Only know their assigned sector and dump location
- Cannot pre-scan terrain for steepness or obstacles
- Discover terrain reactively as they move
- Must avoid getting stuck in ditches or on steep terrain

## Problem

The current DigSim3D logic is too simplistic:
1. **No stuck detection**: Robots can get stuck in ditches/steep terrain indefinitely
2. **No terrain awareness**: Robots plan paths without knowing local height variation
3. **Vulnerable positioning**: May approach dig sites from steep angles
4. **No reactive recovery**: No mechanism to escape bad situations

## Proposed Solution: Reactive Swarm Dig Logic

### Core Principle
**"Find highest, approach safely, dig if flat, recover if stuck"**

Robots plan paths reactively by:
1. Scanning nearby terrain as they move (height gradient sampling)
2. Detecting dangerous slopes early
3. Adjusting paths to avoid steep areas
4. Self-recovering when stuck

### Algorithm Flow

```
STATE: Idle
├─ Scan sector for highest point (sample ~5-10 nearby candidates)
├─ Check if approach is safe (terrain gradient < threshold)
├─ Plan Reeds-Shepp path to target
└─ Transition to MovingToDig

STATE: MovingToDig
├─ Continuously monitor terrain gradient ahead
├─ If gradient too steep detected:
│  ├─ Trigger "Avoidance" recovery
│  ├─ Re-plan to next-best candidate
│  └─ Stay in MovingToDig
├─ If stuck for N cycles (e.g., 30):
│  ├─ Trigger "Stuck" recovery
│  ├─ Attempt reverse/spin escape
│  ├─ If still stuck, claim site unsafe & go to MovingToDump
│  └─ Stay in MovingToDig
├─ If within 0.3m of target:
│  └─ Transition to Digging

STATE: Digging
├─ Dig for fixed duration or until full
├─ Transition to MovingToDump when full

STATE: MovingToDump
├─ Similar safeguards as MovingToDig
└─ When within 0.5m: Transition to Dumping

STATE: Dumping
├─ Dump payload
└─ Transition to Idle
```

### Key Features

#### 1. Terrain Gradient Sensing (Reactive)
- As robot moves toward target, sample 3-5 points ahead
- Calculate slope in direction of travel
- **Threshold**: Abort if slope > 0.2 radians (~11°)
- **Action**: Trigger recovery or pick alternative target

#### 2. Stuck Detection
- Track position every N frames (e.g., 10 frames)
- If moved < 0.3m in 30 frames → stuck
- Log stuck event with position and context
- **Action**: Attempt escape (reverse/spin) or abandon claim

#### 3. Recovery Strategies (in priority order)
- **Level 1 (Avoidance)**: Re-plan to next-best candidate in sector
- **Level 2 (Escape)**: Reverse direction then spin 45° and try to move
- **Level 3 (Surrender)**: Mark site as unsafe, immediately go to dump
- **Level 4 (Retry)**: After dumping, try different angle to same site
- **Level 5 (Abandon)**: If same site causes 2+ consecutive failures, mark off-limits for 60 frames

#### 4. Cooperative Safety
- All 8 robots maintain min separation via RobotCoordinator
- Sharing terrain height data (implicit via sector assignments)
- No pre-communication, only reactive avoidance

### Implementation Details

#### Modified VehicleBrain.cs
```csharp
// New fields
private Vector3 _lastPositionCheck = Vector3.Zero;
private int _framesSinceMovement = 0;
private List<Vector3> _recentFailedSites = new(); // Track unsafe dig sites
private int _failureTimeRemaining = 0;
private int _recoveryAttempt = 0;

// New methods
private float SampleTerrainGradient(Vector3 from, Vector3 to)
{
  // Sample 3 points along path: start, mid, end
  // Calculate slopes and return max
}

private bool IsStuck()
{
  float moved = Agent.GlobalPosition.DistanceTo(_lastPositionCheck);
  if (moved > 0.3f) {
    _lastPositionCheck = Agent.GlobalPosition;
    _framesSinceMovement = 0;
    return false;
  }
  _framesSinceMovement++;
  return _framesSinceMovement > 30;
}

private Vector3 GetSafeAlternativeTarget()
{
  // Sample 3-5 alternatives in sector
  // Return one not in _recentFailedSites
}

private void RecoverFromStuck()
{
  _recoveryAttempt++;
  if (_recoveryAttempt < 2) {
    // Try alternative target
    Vector3 alt = GetSafeAlternativeTarget();
    Coordinator.ReleaseClaim(_robotId);
    CurrentDigTarget = alt;
    PlanPathToDig(alt);
  } else {
    // Mark site unsafe and go dump
    _recentFailedSites.Add(CurrentDigTarget);
    _failureTimeRemaining = 60; // Frames
    Coordinator.ReleaseClaim(_robotId);
    CurrentState = State.MovingToDump;
  }
}

private void ProcessMovingToDig()
{
  // Check stuck
  if (IsStuck()) {
    RecoverFromStuck();
    return;
  }
  
  // Check terrain gradient
  float gradient = SampleTerrainGradient(
    Agent.GlobalPosition, 
    CurrentDigTarget);
  
  if (gradient > 0.2f) {
    // Too steep! Trigger avoidance recovery
    Vector3 alt = GetSafeAlternativeTarget();
    if (alt != CurrentDigTarget) {
      Coordinator.ReleaseClaim(_robotId);
      CurrentDigTarget = alt;
      PlanPathToDig(alt);
      _recoveryAttempt = 0;
    }
    return;
  }
  
  // Normal: check if arrived
  if (IsAtTarget(Agent.GlobalPosition, CurrentDigTarget, 0.3f)) {
    _recoveryAttempt = 0;
    CurrentState = State.Digging;
  }
}

private void ProcessIdle()
{
  // Decay failure timer
  if (_failureTimeRemaining > 0) {
    _failureTimeRemaining--;
    if (_failureTimeRemaining <= 0) {
      _recentFailedSites.Clear();
    }
  }
  
  // Find highest point avoiding failed sites
  Vector3 digTarget = Coordinator.GetBestDigPoint(
    RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius);
  
  // Skip if in recent failures
  foreach (var failed in _recentFailedSites) {
    if (digTarget.DistanceTo(failed) < 0.5f) {
      // Get alternative
      digTarget = Coordinator.GetSafeAlternative(
        RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius, failed);
      break;
    }
  }
  
  // Claim and proceed
  if (Coordinator.ClaimDigSite(RobotId, digTarget, 0.5f)) {
    CurrentDigTarget = digTarget;
    if (TargetIndicator != null) {
      TargetIndicator.ShowIndicator(RobotId, digTarget, SectorColor);
    }
    PlanPathToDig(digTarget);
    _recoveryAttempt = 0;
    CurrentState = State.MovingToDig;
  }
}
```

#### Modified RobotCoordinator.cs
```csharp
// New method
public Vector3 GetSafeAlternative(
  int robotId,
  TerrainDisk terrain,
  float thetaMin,
  float thetaMax,
  float maxRadius,
  Vector3 excludePoint,
  int samples = 32)
{
  // Same as GetBestDigPoint, but skip excludePoint
  // Return second-best or third-best candidate
}
```

### Expected Improvements

| Metric | Before | After |
|--------|--------|-------|
| Robots stuck in ditches | Frequent | Rare |
| Stuck recovery time | ∞ (no recovery) | <5 seconds |
| Unsafe approaches | Common | Detected early |
| Terrain efficiency | Variable | Consistent |
| Simulation stability | Fragile | Robust |

### Visualization

- **Target Indicators**: Colored spheres at claimed dig sites (existing)
- **Terrain Height Bands**: Yellow/Green/Blue/Purple by height (existing)
- **Sector Lines**: Radial boundaries (existing)
- **New Overlay** (optional): Red spheres at failed/unsafe sites (grace period)
- **Debug Gradient Arrows** (optional): Show terrain slope vectors ahead of robot

### Metrics to Track

In SimulationDirector.cs / SimulationHUD:
- Total stuck events (should be ~1-2 for entire 8-robot sim)
- Recovery attempts by type (avoidance, escape, surrender)
- Unsafe site rejections
- Time spent stuck (should be <5 sec per event)

### Testing Strategy

1. **Baseline run**: Current dig logic, measure baseline metrics
2. **New logic run**: Implement proposed system
3. **Compare**: 
   - Plot stuck events per robot over time
   - Measure total terrain flattened
   - Verify no infinite loops
   - Check recovery time after stuck detection

## Implementation Checklist

- [ ] Add stuck detection (position tracking, threshold at 30 frames)
- [ ] Add terrain gradient sampling (3-point forward look)
- [ ] Implement avoidance recovery (re-plan to alternative)
- [ ] Implement escape recovery (reverse/spin maneuver)
- [ ] Add failure memory (_recentFailedSites tracking)
- [ ] Add metrics logging to SimulationDirector
- [ ] Update UI to show recovery stats
- [ ] Test with various terrain heights and obstacles
- [ ] Validate Reeds-Shepp paths work for recovery moves

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Infinite avoidance loops | Surrender after N attempts, track failures |
| All robots stuck simultaneously | Min separation + coordinator ensures spread |
| Path planning fails during recovery | Fall back to simple rotate + move |
| Terrain changes while moving | Gradient check every frame, re-plan if needed |
| Performance overhead | Cache terrain gradient, sample only near robot |

---

**Recommendation**: Implement Level 1-3 recovery first, then add Level 4-5 if needed. Level 1-3 should handle 95%+ of stuck cases based on 3d/ analysis.
