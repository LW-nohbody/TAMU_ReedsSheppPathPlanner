# Terrain Climbing & Stuck Recovery Improvement Plan

## Overview
This document outlines a comprehensive strategy to ensure robots **can climb any hill** and **never get stuck in ditches** in the 3D Reeds-Shepp terrain flattening simulation.

---

## Current State Analysis

### Existing Mechanisms
1. **Stuck Detection** (VehicleBrain.cs)
   - Threshold: 60 frames (1 second at 60fps)
   - Movement requirement: > 0.5m to be considered "good"
   - Recovery: Release claim, reset target, attempt new path

2. **Path Planning**
   - Uses Reeds-Shepp planner with 0.25m sample step
   - Returns XZ waypoints without height consideration
   - Height applied post-hoc via terrain sampling

3. **Terrain Following**
   - GroundFollowAt() samples terrain height and normal
   - Blends pitch/roll based on terrain slope
   - Uses ride height offset (0.25m) for clearance

4. **Movement**
   - Manual integration (no Godot physics)
   - Speed: 0.6 m/s base
   - Arena radius enforcement (15m)
   - Adaptive speed: slows to 30% within 0.5m of waypoint

### Known Challenges
- **Ditch Entrapment**: Robots can become stuck in deep terrain depressions
- **Steep Climb Failures**: High-slope terrain can cause path planning to fail
- **Recovery Loops**: Multiple recovery attempts can lead to oscillation
- **Height Uncertainty**: Path planner doesn't account for terrain elevation when finding paths

---

## Key Insight: Reactive Swarm Behavior
**Robots cannot pre-scan terrain** - they discover obstacles/slopes as they move. This requires **reactive, decentralized strategies**:
- Each robot acts autonomously based on immediate feedback
- No global path planning with terrain awareness
- Physical feedback determines actions (can't climb → reverse/dig)
- Swarm coordination via local claims and neighbor avoidance

---

## Proposed Improvements

### 1. **Aggressive Forward-Pushing Strategy** (High Priority - REPLACES Pre-Planning)
**Goal**: Push through obstacles by digging in the direction of movement

#### Key Concept:
When a robot encounters an obstacle or gets stuck:
1. **Don't replan** - dig out the obstacle ahead
2. **Push forward** with aggressive digging in the stuck direction
3. **Use gravity** - let terrain collapse into dug areas
4. **Minimal lateral movement** - stay focused on goal direction

#### Implementation:

**File**: `3d/Scripts/SimCore/Godot/VehicleBrain.cs` (NEW method)

```csharp
private bool IsStuckForward(Vector3 currentPos)
{
    // Check if we've been at roughly same position for extended time
    // (same as current stuck detection)
    return distMoved < 0.3f && _stuckCycleCount > 30;
}

private void AggressiveForwardRecovery()
{
    // When stuck, dig AHEAD in the direction we need to go
    var xf = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z;  // Forward direction
    
    // Target: dig point 1-1.5m ahead
    Vector3 digTarget = new Vector3(xf.Origin.X, 0, xf.Origin.Z) + 
                        new Vector3(fwd.X, 0, fwd.Z).Normalized() * 1.2f;
    
    float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width) * 1.2f;  // Aggressive
    _currentTarget = digTarget;  // Set as new dig target
    
    GD.Print($"[{_spec.Name}] STUCK - Aggressive forward dig at {digTarget}");
}
```

#### How It Works:
- Robot path planner picks a waypoint ahead
- Robot follows it, but gets stuck (no movement)
- Instead of replanning, robot digs the next waypoint location
- Terrain flattens, robot can now proceed
- Continues digging forward until it breaks through

---

### 2. **Enhanced Stuck Detection & Recovery** (High Priority)
**Goal**: Detect stuckness more reliably and recover more aggressively

#### Current Issues:
- Single recovery attempt may not be enough
- 60-frame threshold might be too long/short depending on terrain
- Aggressive recovery (return home) is too drastic

#### New Strategy:
1. **Multi-Level Recovery**:
   - Level 0 (Stuck): Try dig ahead aggressively
   - Level 1 (Still Stuck): Reverse, try different angle
   - Level 2 (Persistent): Return home and restart sector
   
2. **Better Movement Tracking**:
   - Track position over short window (0.5-1.0s)
   - If no movement detected, immediately start aggressive dig
   - Fast escalation (no long waits)

3. **Pit Escape via Forward Digging**:
   - When stuck in depression, dig forward aggressively
   - Dug material falls/slides out of pit
   - Robot follows dug path upward

#### Implementation Details:

**File**: `3d/Scripts/SimCore/Godot/VehicleBrain.cs`

```csharp
private int _stuckCycleCount = 0;
private int _recoveryLevel = 0;  // 0=basic dig, 1=reverse, 2=go home
private Vector3 _lastKnownGoodPos = Vector3.Zero;

private bool IsStuckFast(Vector3 currentPos)
{
    float distMoved = currentPos.DistanceTo(_lastKnownGoodPos);
    
    if (distMoved > 0.3f)
    {
        _lastKnownGoodPos = currentPos;
        _stuckCycleCount = 0;
        _recoveryLevel = 0;  // Reset recovery
        return false;
    }
    
    _stuckCycleCount++;
    
    // FAST escalation: 30 frames (0.5s) instead of 60
    if (_stuckCycleCount > 30)
    {
        return true;
    }
    return false;
}

private void HandleStuckRecovery()
{
    switch (_recoveryLevel)
    {
        case 0:
            // Aggressive dig forward
            AggressiveForwardDig();
            _recoveryLevel = 1;
            GD.Print($"[{_spec.Name}] Recovery L0: Digging forward");
            break;
            
        case 1:
            // Try reverse direction or alternative target
            _coordinator.ReleaseClaim(_robotId);
            _returningHome = false;
            _currentTarget = Vector3.Zero;  // Force new target
            _recoveryLevel = 2;
            GD.Print($"[{_spec.Name}] Recovery L1: Trying alternative target");
            break;
            
        case 2:
            // Give up on this sector, go home
            _returningHome = true;
            _coordinator.ReleaseClaim(_robotId);
            _recoveryLevel = 0;
            GD.Print($"[{_spec.Name}] Recovery L2: Returning home");
            break;
    }
}

private void AggressiveForwardDig()
{
    var xf = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z;  // Robot's forward
    var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);
    
    // Dig point 1.2m ahead in robot's heading
    Vector3 digTarget = curPos + 
        new Vector3(fwd.X, 0, fwd.Z).Normalized() * 1.2f;
    
    float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width) * 1.3f;  // Bigger!
    
    // Force claim this dig site
    _coordinator.ReleaseClaim(_robotId);
    if (_coordinator.ClaimDigSite(_robotId, digTarget, digRadius))
    {
        _currentTarget = digTarget;
        _currentStatus = "STUCK - Aggressive dig ahead!";
    }
}
```

---

### 3. **Pit Escape Algorithm** (High Priority)
**Goal**: Enable robots to dig themselves out of terrain depressions

#### Strategy:
- **Pit Detection**: Identify if robot is in depression (surrounded by higher terrain)
- **Escape Target Selection**: Find highest adjacent point within reasonable distance
- **Incremental Climbing**: Take small steps upward, digging if necessary
- **Hysteresis**: Once escaped, avoid immediately returning to same pit

#### Implementation:

**File**: `3d/Scripts/Game/VehicleAgent3D.cs` (new method)

```csharp
public Vector3 FindEscapeRoute(Vector3 currentPos, float searchRadius = 3.0f)
{
    // Spiral search around robot, finding higher ground
    // Return first point that's higher and reachable
    // If no natural escape, return highest point found
    
    Vector3 bestPos = currentPos;
    float bestHeight = _terrain.SampleHeightNormal(currentPos, out _, out _) ? 
        _terrain.GetHeightAt(currentPos) : 0f;
    
    float spiralRadius = 0.5f;
    while (spiralRadius <= searchRadius)
    {
        for (float angle = 0; angle < 360; angle += 15)
        {
            var testPos = currentPos + 
                new Vector3(Mathf.Cos(angle * Mathf.DegToRad), 0, 
                           Mathf.Sin(angle * Mathf.DegToRad)) * spiralRadius;
            
            if (_terrain.SampleHeightNormal(testPos, out var hitPos, out _))
            {
                if (hitPos.Y > bestHeight)
                {
                    bestHeight = hitPos.Y;
                    bestPos = testPos;
                }
            }
        }
        spiralRadius += 0.25f;
    }
    
    return bestPos;
}

public void AttemptPitEscape()
{
    // Called when stuck for extended time
    var currentPos = GlobalTransform.Origin;
    var escapeTarget = FindEscapeRoute(currentPos);
    
    // Dig a small amount to create escape route
    if (escapeTarget != currentPos)
    {
        LowerTerrainAt(escapeTarget, 0.5f, 0.1f);
    }
}
```

---

### 4. **Slope-Aware Movement** (Medium Priority)
**Goal**: Handle steep terrain better during path following

#### Changes to `VehicleAgent3D.cs`:

```csharp
private void UpdateMovement(float dt)
{
    // ... existing code ...
    
    // NEW: Check slope before movement
    float slope = CalculateTerrainSlope(nextXZ, facingDir);
    
    if (slope > MaxClimbableGrade)
    {
        // Too steep - try to find alternative
        // or slow down further
        speedMult *= 0.5f;
        
        // Request robot brain to find alternative path
        _controller.OnSteepSlopeEncountered(nextXZ);
    }
    
    // ... rest of existing code ...
}

private float CalculateTerrainSlope(Vector3 position, Vector3 direction)
{
    // Sample terrain ahead and behind
    Vector3 aheadPos = position + direction * 0.5f;
    Vector3 behindPos = position - direction * 0.5f;
    
    float aheadHeight = GetHeightAt(aheadPos);
    float behindHeight = GetHeightAt(behindPos);
    
    float heightDiff = aheadHeight - behindHeight;
    float horizontalDist = 1.0f;
    
    float slopeAngle = Mathf.Atan2(heightDiff, horizontalDist);
    return Mathf.RadToDeg(slopeAngle);
}
```

---

### 5. **Improved Terrain Following** (Low Priority)
**Goal**: More robust handling of terrain irregularities

#### Current Implementation:
- `GroundFollowAt()` samples single point
- Blends pitch/roll smoothly

#### Enhancement:
- Sample **3 points** (forward, center, back) for better gradient estimation
- Adjust ride height dynamically based on slope
- Add damping to prevent oscillation over rough terrain

```csharp
private void GroundFollowAt_Enhanced(Vector3 centerXZ, Vector3 facingDir, float dt)
{
    // Sample 3 points: center, forward, backward
    Vector3 fwdPos = centerXZ + facingDir * 0.3f;
    Vector3 bkPos = centerXZ - facingDir * 0.3f;
    
    // Get heights and normals at all three points
    float centerHeight = SampleHeight(centerXZ);
    float fwdHeight = SampleHeight(fwdPos);
    float bkHeight = SampleHeight(bkPos);
    
    // Adjust ride height based on slope
    float slope = (fwdHeight - bkHeight) / 0.6f;
    float dynamicRideHeight = RideHeightFollow + Mathf.Abs(slope) * 0.05f;
    
    // ... apply heights with better gradient estimation ...
}
```

---

### 3. **Smart Waypoint Handling in Movement** (High Priority)
**Goal**: Detect obstacles earlier and dig more aggressively

#### Key Ideas:
- **Waypoint-Level Stuck Detection**: If robot hasn't reached waypoint in X frames, dig it
- **Incremental Waypoint Digging**: Dig each waypoint area slightly as we approach
- **No Replanning**: Just dig the waypoint and push forward

#### Implementation:

**File**: `3d/Scripts/Game/VehicleAgent3D.cs` (Add to movement loop)

```csharp
private int _framesAtCurrentWaypoint = 0;
private const int WAYPOINT_TIMEOUT_FRAMES = 30;  // 0.5s at 60fps

private void UpdateMovement(float dt)
{
    // ... existing code ...
    
    var curXZ = GlobalTransform.Origin; curXZ.Y = 0f;
    var tgt = _path[_i]; tgt.Y = 0f;
    
    // NEW: Check if stuck at waypoint
    if (curXZ.DistanceTo(tgt) < 2.0f)  // Close to waypoint but not reached
    {
        _framesAtCurrentWaypoint++;
        
        if (_framesAtCurrentWaypoint > WAYPOINT_TIMEOUT_FRAMES)
        {
            // Stuck approaching waypoint - dig it!
            GD.Print($"[{Name}] Waypoint #{_i} stuck for {_framesAtCurrentWaypoint}f - DIGGING");
            LowerTerrainAt(tgt, 0.8f, 0.15f);  // Dig waypoint
            _framesAtCurrentWaypoint = 0;  // Reset counter
        }
    }
    else
    {
        _framesAtCurrentWaypoint = 0;  // Reset when approaching
    }
    
    // ... rest of existing code ...
}
```

---

### 4. **Terrain Collapse Mechanics** (Medium Priority)
**Goal**: Use physics to help robots climb out of dug areas

#### Key Concept:
When robots dig, create temporary overhang that collapses naturally, filling the pit and creating an escape ramp.

#### Strategy:
- Dig creates a depression
- Dig aggressively so walls become unstable
- Terrain naturally collapses into dug area
- Creates a slope robot can climb
- No special "pit escape" logic needed

#### Implementation:

**File**: `3d/Scripts/Game/TerrainDisk.cs` (Enhance LowerArea)
**Goal**: Make it easy to adjust behavior without recompiling

#### New Configuration Class:

**File**: `3d/Scripts/Config/TerrainClimbConfig.cs`

```csharp
public static class TerrainClimbConfig
{
    // Stuck detection
    public const float STUCK_MOVEMENT_THRESHOLD = 0.5f;  // meters
    public const float STUCK_TIME_SHORT = 1.0f;  // seconds (level 0)
    public const float STUCK_TIME_MEDIUM = 3.0f; // seconds (level 1)
    public const float STUCK_TIME_LONG = 6.0f;   // seconds (level 2)
    
    // Terrain
    public const float MAX_CLIMBABLE_GRADE = 45f; // degrees
    public const float MAX_DRIVEABLE_SLOPE = 60f; // degrees
    
    // Recovery
    public const float PIT_ESCAPE_SEARCH_RADIUS = 3.0f; // meters
    public const float PIT_ESCAPE_DIG_AMOUNT = 0.1f;    // meters
    
    // Movement
    public const float MIN_SPEED_MULTIPLIER = 0.2f; // 20% of base speed
}
```

---

## Implementation Roadmap

### Phase 1: Enhanced Stuck Recovery (Week 1)
1. Implement multi-level stuck detection
2. Add pit escape algorithm
3. Test with various terrain shapes
4. **Status**: ✅ Ready to implement

### Phase 2: Slope-Aware Movement (Week 2)
1. Add slope calculation to movement logic
2. Implement slope warnings to planner
3. Test on steep terrain
4. **Status**: 📋 Planned

### Phase 3: Terrain-Aware Path Planning (Week 3)
1. Enhance ReedsSheppPlanner with terrain analysis
2. Add waypoint validation
3. Test on complex terrain
4. **Status**: 📋 Planned

### Phase 4: Configuration & Documentation (Week 4)
1. Create configuration class
2. Document all parameters
3. Create tuning guide
4. **Status**: 📋 Planned

---

## Testing Strategy

### Unit Tests:
```csharp
[Test]
public void TestPitDetection()
{
    // Create circular depression
    // Verify robot detects being stuck in pit
}

[Test]
public void TestPitEscape()
{
    // Create pit, spawn robot inside
    // Verify robot escapes within N cycles
}

[Test]
public void TestSlopeHandling()
{
    // Create 60° slope
    // Verify robot successfully climbs
}
```

### Integration Tests:
- Multi-robot terrain flattening on complex terrain
- Performance under sustained dig operations
- Recovery behavior under various stuck scenarios

---

## Success Criteria

✅ **Terrain Climbing**:
- Robots successfully climb slopes up to 45°
- No robot gets stuck on any natural terrain
- Recovery time < 10 seconds on average

✅ **Stuck Prevention**:
- Multi-level recovery prevents oscillation
- Pit detection prevents entrapment
- Escape routes always available

✅ **Performance**:
- No frame rate degradation
- Path planning remains < 10ms
- Terrain sampling stays < 5ms

---

## Risk Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|-----------|
| Robots dig too much escaping pits | Medium | High | Limit dig radius in pit escape |
| Performance degradation | Low | High | Profile before optimization |
| Recovery loops | Medium | Medium | Add hysteresis and cooldowns |
| Path planning failures | Low | Medium | Fallback to simple straight paths |

---

## Next Steps

1. **Review & Approve** this plan
2. **Implement Phase 1** (Enhanced Stuck Recovery)
3. **Test thoroughly** on various terrain
4. **Iterate** based on results

---

## Questions for Review

1. Should pit escape dig the terrain or just navigate to higher ground?
2. What's the acceptable maximum slope angle for climbing?
3. Should we add visual debug markers for stuck detection and recovery?
4. Should recovery attempts be logged for performance analysis?

