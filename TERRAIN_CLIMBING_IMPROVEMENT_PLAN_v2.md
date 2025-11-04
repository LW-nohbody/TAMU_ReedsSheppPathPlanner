# Terrain Climbing & Stuck Recovery Improvement Plan v2
## Reactive Swarm Approach (No Pre-Scanning)

---

## Key Insight: Autonomous Swarm Robots
**Robots cannot pre-scan terrain** - they discover obstacles/slopes as they move. This requires **reactive, decentralized strategies**:
- Each robot acts autonomously based on immediate feedback
- No global terrain analysis or planning
- Physical feedback determines actions (can't move → dig)
- Swarm coordination via local claims and neighbor avoidance
- **Success = Self-recovery through aggressive digging**

---

## Core Strategy: "Dig Your Way Forward"

When a robot gets stuck, instead of replanning routes or searching for escape paths, it **digs the obstacle directly ahead** and keeps pushing forward. The terrain it digs creates natural ramps and escape routes.

---

## Proposed Improvements

### 1. **FAST Stuck Detection** (High Priority)
**Goal**: Detect stuckness quickly and react immediately

#### Current Problem:
- 60-frame threshold is too long
- Robot wastes time oscillating before recovery

#### New Approach:
- **30-frame threshold** (0.5 seconds at 60fps)
- **Tighter movement requirement**: 0.3m (not 0.5m)
- **Instant escalation**: Immediately dig forward on detection

#### Implementation:

**File**: `3d/Scripts/SimCore/Godot/VehicleBrain.cs`

```csharp
private int _stuckCycleCount = 0;
private int _recoveryLevel = 0;  // 0=dig forward, 1=try alt, 2=go home
private Vector3 _lastKnownGoodPos = Vector3.Zero;

private bool IsStuckFast(Vector3 currentPos)
{
    float distMoved = currentPos.DistanceTo(_lastKnownGoodPos);
    
    // Good movement = reset
    if (distMoved > 0.3f)  // Tighter threshold
    {
        _lastKnownGoodPos = currentPos;
        _stuckCycleCount = 0;
        _recoveryLevel = 0;  // Reset recovery level
        return false;
    }
    
    // No movement = increment counter
    _stuckCycleCount++;
    
    // FAST threshold: 30 frames = 0.5s at 60fps
    if (_stuckCycleCount > 30)
    {
        return true;
    }
    return false;
}
```

#### In PlanAndGoOnce():

```csharp
bool isStuck = IsStuckFast(curPos);
if (isStuck)
{
    _currentStatus = "STUCK - Digging forward!";
    HandleStuckRecovery();
    return;  // Skip normal planning, just dig
}
```

---

### 2. **Aggressive Forward Digging Recovery** (High Priority)
**Goal**: Dig the obstacle ahead instead of replanning

#### Key Concept:
When stuck, identify the direction robot needs to go, and **aggressively dig that area**. Dug material creates escape routes.

#### Implementation:

**File**: `3d/Scripts/SimCore/Godot/VehicleBrain.cs`

```csharp
private void HandleStuckRecovery()
{
    switch (_recoveryLevel)
    {
        case 0:
            // LEVEL 0: Dig forward aggressively
            AggressiveForwardDig();
            _recoveryLevel = 1;
            GD.Print($"[{_spec.Name}] Recovery L0: Digging forward");
            break;
            
        case 1:
            // LEVEL 1: Try different target (release and pick new)
            _coordinator.ReleaseClaim(_robotId);
            _currentTarget = Vector3.Zero;
            _recoveryLevel = 2;
            GD.Print($"[{_spec.Name}] Recovery L1: Trying alternative target");
            break;
            
        case 2:
            // LEVEL 2: Go home and restart
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
    var fwd = -xf.Basis.Z;  // Robot's forward direction
    var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);
    
    // Dig target: 1.2m ahead in robot's heading
    Vector3 digTarget = curPos + 
        new Vector3(fwd.X, 0, fwd.Z).Normalized() * 1.2f;
    
    // Aggressive dig: 30% larger radius
    float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width) * 1.3f;
    
    // Release old claim and claim new dig site
    _coordinator.ReleaseClaim(_robotId);
    if (_coordinator.ClaimDigSite(_robotId, digTarget, digRadius))
    {
        _currentTarget = digTarget;
        _currentStatus = "🔥 STUCK-DIG FORWARD 🔥";
        GD.Print($"[{_spec.Name}] Aggressive forward dig at {digTarget} (r={digRadius:F2}m)");
    }
}
```

---

### 3. **Waypoint-Level Stuck Detection** (High Priority)
**Goal**: Dig waypoints that robots can't reach

#### Key Idea:
If a robot is close to a waypoint but can't reach it for several frames, **dig around that waypoint**. This handles "stuck just before waypoint" scenarios.

#### Implementation:

**File**: `3d/Scripts/Game/VehicleAgent3D.cs`

```csharp
private int _framesAtCurrentWaypoint = 0;
private const int WAYPOINT_TIMEOUT_FRAMES = 30;  // 0.5s at 60fps

private void UpdateMovement(float dt)
{
    // ... existing code to get current waypoint ...
    
    var curXZ = GlobalTransform.Origin; curXZ.Y = 0f;
    var tgt = _path[_i]; tgt.Y = 0f;
    
    float distToWaypoint = curXZ.DistanceTo(tgt);
    
    // NEW: Check if stuck at waypoint
    if (distToWaypoint < 1.0f)  // Within reach distance
    {
        _framesAtCurrentWaypoint++;
        
        if (_framesAtCurrentWaypoint > WAYPOINT_TIMEOUT_FRAMES)
        {
            // Stuck at waypoint - DIG IT!
            GD.Print($"[{Name}] Waypoint #{_i} stuck {_framesAtCurrentWaypoint}f - DIGGING");
            
            // Dig the waypoint to create path
            float digRadius = 0.9f;  // Dig around waypoint
            float digDepth = 0.12f;   // Moderate depth
            LowerTerrainAt(tgt, digRadius, digDepth);
            
            _framesAtCurrentWaypoint = 0;  // Reset timer
        }
    }
    else if (distToWaypoint < 2.0f)
    {
        // Getting close, don't dig yet but track time
        _framesAtCurrentWaypoint++;
    }
    else
    {
        // Far from waypoint, reset
        _framesAtCurrentWaypoint = 0;
    }
    
    // ... rest of existing movement code ...
}
```

---

### 4. **Speed Reduction Near Obstacles** (Medium Priority)
**Goal**: Slower approach to waypoints allows careful navigation

#### Implementation:

**File**: `3d/Scripts/Game/VehicleAgent3D.cs`

```csharp
private void UpdateMovement(float dt)
{
    // ... existing waypoint code ...
    
    var curXZ = GlobalTransform.Origin; curXZ.Y = 0f;
    var tgt = _path[_i]; tgt.Y = 0f;
    float distToWaypoint = curXZ.DistanceTo(tgt);
    
    // Adaptive speed: slow down as we approach
    float speedMult = 1.0f;
    
    if (distToWaypoint < 0.5f)
    {
        speedMult *= 0.2f;  // 20% speed - very careful
    }
    else if (distToWaypoint < 1.0f)
    {
        speedMult *= 0.4f;  // 40% speed
    }
    else if (distToWaypoint < 2.0f)
    {
        speedMult *= 0.7f;  // 70% speed
    }
    
    float effectiveSpeed = SpeedMps * speedMult * GlobalSpeedMultiplier;
    
    // ... rest of movement code ...
}
```

---

### 5. **Terrain Collapse Design** (Medium Priority)
**Goal**: Create natural ramps through intelligent dig patterns

#### Key Concept:
When digging, create a collapse-prone pattern: deep center, lighter edges. This causes terrain to naturally cave inward, creating a ramp.

#### Implementation:

**File**: `3d/Scripts/Game/TerrainDisk.cs`

```csharp
public void LowerArea(Vector3 centerXZ, float radius, float deltaHeight)
{
    int ci = (int)((centerXZ.X + ArenaRadius) / _step);
    int cj = (int)((centerXZ.Z + ArenaRadius) / _step);
    
    for (int j = 0; j < _N; j++)
    {
        for (int i = 0; i < _N; i++)
        {
            float dx = (i - ci) * _step;
            float dz = (j - cj) * _step;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            
            if (dist < radius)
            {
                // IMPROVED: Create collapse-prone terrain pattern
                // Deep center → Light edges creates unstable slopes
                
                float ratio = dist / radius;
                float aggressiveness = 0f;
                
                if (ratio < 0.4f)
                {
                    // Inner zone: DEEP dig (1.5x)
                    aggressiveness = 1.5f;
                }
                else if (ratio < 0.7f)
                {
                    // Middle zone: Normal dig (1.0x)
                    aggressiveness = 1.0f;
                }
                else if (ratio < 0.9f)
                {
                    // Outer zone: Light dig (0.4x)
                    aggressiveness = 0.4f;
                }
                else
                {
                    // Edge: Barely dig (0.1x) - leaves loose material
                    aggressiveness = 0.1f;
                }
                
                _heights[i, j] = Mathf.Max(0f, _heights[i, j] - deltaHeight * aggressiveness);
            }
        }
    }
    
    UpdateMesh();
}
```

**Result**: 
- Center digs deep
- Edges remain high with loose material
- Gravity simulation causes collapse
- Forms ramp automatically

---

### 6. **Configuration Parameters** (Low Priority)
**Goal**: Easy tuning for different scenarios

#### New Configuration Class:

**File**: `3d/Scripts/Config/SwarmClimbConfig.cs`

```csharp
public static class SwarmClimbConfig
{
    // ===== STUCK DETECTION =====
    public const float STUCK_MOVEMENT_THRESHOLD = 0.3f;   // meters (tight)
    public const int STUCK_FRAMES_THRESHOLD = 30;         // 0.5s at 60fps (FAST)
    
    // ===== AGGRESSIVE DIG RECOVERY =====
    public const float FORWARD_DIG_DISTANCE = 1.2f;       // how far ahead to dig
    public const float FORWARD_DIG_RADIUS_MULT = 1.3f;    // 30% bigger digs
    public const float FORWARD_DIG_DEPTH = 0.15f;         // depth per dig
    
    // ===== WAYPOINT TIMEOUT =====
    public const int WAYPOINT_TIMEOUT_FRAMES = 30;        // 0.5s to reach
    public const float WAYPOINT_DIG_RADIUS = 0.9f;        // dig around stuck waypoint
    public const float WAYPOINT_DIG_DEPTH = 0.12f;
    
    // ===== SPEED CONTROL =====
    public const float SPEED_FINAL_APPROACH = 0.2f;       // 20% at <0.5m
    public const float SPEED_CLOSE_APPROACH = 0.4f;       // 40% at <1.0m
    public const float SPEED_FAR_APPROACH = 0.7f;         // 70% at <2.0m
    
    // ===== TERRAIN COLLAPSE ZONES =====
    public const float COLLAPSE_INNER_RADIUS = 0.4f;      // % of radius
    public const float COLLAPSE_MIDDLE_RADIUS = 0.7f;
    public const float COLLAPSE_OUTER_RADIUS = 0.9f;
    
    public const float COLLAPSE_INNER_MULT = 1.5f;        // Deep dig
    public const float COLLAPSE_MIDDLE_MULT = 1.0f;       // Normal
    public const float COLLAPSE_OUTER_MULT = 0.4f;        // Light
    public const float COLLAPSE_EDGE_MULT = 0.1f;         // Barely dig
}
```

---

## Implementation Roadmap

### Phase 1: FAST Stuck Detection (1-2 hours)
✅ **Priority**: HIGH
1. Reduce stuck threshold from 60→30 frames
2. Tighten movement threshold 0.5m→0.3m
3. Test and verify robots recover faster

### Phase 2: Aggressive Forward Digging (2-3 hours)
✅ **Priority**: HIGH
1. Implement AggressiveForwardDig() in VehicleBrain.cs
2. Add recovery levels (dig → try alt → go home)
3. Test on various terrain shapes

### Phase 3: Waypoint Timeout Digging (1-2 hours)
✅ **Priority**: HIGH
1. Add frame counter for waypoint approach
2. Implement dig when stuck at waypoint
3. Test with slopes and depressions

### Phase 4: Speed Reduction (30 min)
✅ **Priority**: MEDIUM
1. Add distance-based speed multiplier
2. Test smooth approach to difficult terrain

### Phase 5: Collapse Terrain Pattern (1 hour)
✅ **Priority**: MEDIUM
1. Enhance LowerArea() with zone-based digging
2. Test ramp formation on difficult terrain

### Phase 6: Configuration (30 min)
✅ **Priority**: LOW
1. Create SwarmClimbConfig.cs
2. Make all parameters exportable

---

## Testing Strategy

### Unit Tests:
```csharp
[Test]
public void TestStuckDetectionFast()
{
    // Freeze robot, verify stuck detected in <0.5s
}

[Test]
public void TestForwardDig()
{
    // Verify aggressive dig claims waypoint ahead
}

[Test]
public void TestWaypointTimeout()
{
    // Block waypoint with high terrain
    // Verify dig after 30 frames
}

[Test]
public void TestTerrainCollapse()
{
    // Create dig pattern
    // Verify ramp forms via material collapse
}
```

### Scenario Tests:
1. **Pit Escape**: Robot in circular depression → digs forward → escapes
2. **Steep Climb**: Robot facing 60° slope → digs waypoints → climbs
3. **Multiple Stuck**: Robot stuck 3 times in same location → goes home
4. **Swarm Coordination**: 5 robots in same area → coordinate digs, no collision

---

## Expected Behavior Changes

### Before:
- Robot stuck for 2 seconds (120 frames)
- Waits for stuck detection, then searches for new target
- Can get caught in oscillation loops
- Fails on steep terrain or pits

### After:
- Robot stuck for 0.5 seconds (30 frames) → **IMMEDIATELY digs ahead**
- No replanning, just dig and push forward
- Terrain collapse creates natural escape ramps
- **Self-recovers through digging**
- Handles any hill, any pit

---

## Success Criteria

✅ **Stuck Recovery Time**: < 1 second (30 frames at 60fps)
✅ **Stuck Escalation**: Max 3 recovery attempts before going home
✅ **Waypoint Timeout**: Digs within 0.5s of waypoint unreachability  
✅ **Hill Climbing**: Successfully climbs any terrain with digging
✅ **Pit Escape**: Robot never remains stuck in depression > 3s
✅ **Performance**: All additions < 2ms per robot per frame
✅ **Swarm Coordination**: No collision during aggressive digging

---

## Risk Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|-----------|
| Robots dig too fast/deep | Medium | Medium | Limit per-dig depth to 0.15m |
| Terrain becomes too soft | Low | Low | Ensure heights stay ≥ 0.0 |
| Performance degradation | Low | Medium | Profile LowerArea() calls |
| Recovery loops | Low | Low | Max 3 attempts before home |
| Waypoint timeout too aggressive | Medium | Low | Start with 30f, tune upward |

---

## Code Changes Summary

| File | Changes | LOC |
|------|---------|-----|
| VehicleBrain.cs | Fast stuck detection + aggressive dig | +50 |
| VehicleAgent3D.cs | Waypoint timeout + speed reduction | +30 |
| TerrainDisk.cs | Collapse pattern digging | +25 |
| SwarmClimbConfig.cs | NEW - Configuration | +30 |
| **Total** | | **+135 LOC** |

---

## Questions for Final Review

1. Should waypoint timeout trigger dig automatically, or only after multiple attempts?
2. Should aggressive dig increase payload capacity temporarily?
3. Should we log all recovery attempts for analysis?
4. Should recovery level reset when robot successfully moves again?
