# Simple, Smart Dig System - From Scratch

## Philosophy
**"Flatten peaks, never dig pits"**

The key insight: Always dig the highest point in your sector. This naturally flattens terrain without creating dangerous pits that trap robots.

## How It Works

### 1. Simple Dig Logic (`SimpleDigLogic.cs`)
```csharp
// Always dig the highest point - naturally flattens terrain
Vector3 highestPoint = FindHighestInSector(terrain, thetaMin, thetaMax, radius);

// Dig size is relative to robot width (covers robot footprint)
float digRadius = GetDigRadius(robotWidth);  // ~0.6 * robot width

// Dig small amounts (0.03m per operation)
float dug = PerformDig(terrain, highestPoint, currentPayload, capacity, digRadius);
```

**Why this works:**
- Robots always target peaks, never valleys
- **Dig size matches robot footprint** - not too big, not too small
- Small dig amounts (0.03m) prevent deep pits
- Terrain progressively flattens from top down
- No complex stuck detection needed - robots stay on high ground

### 2. Clean Robot Brain (`VehicleBrain.cs`)
Each robot follows a simple cycle:
1. **Find** → Locate highest point in your assigned sector
2. **Drive** → Use Reeds-Shepp path to drive there
3. **Dig** → Remove a small amount (flatten the peak)
4. **Repeat** → Until full
5. **Dump** → Drive home and dump, then repeat

```csharp
// Simple decision logic
if (payload >= capacity)
    targetPos = homePosition;  // Go dump
else
    targetPos = FindHighestInSector();  // Go dig highest point
```

### 3. Key Parameters
```csharp
DIG_AMOUNT = 0.03f;              // Meters lowered per dig (small = safe)
ROBOT_CAPACITY = 0.5f;           // Cubic meters robot can carry

// Dig radius is calculated based on robot size
digRadius = robotWidth * 0.6f;   // About 0.72m for 1.2m wide robot
                                 // Covers robot footprint perfectly
```

**Example for default robot (1.2m wide):**
- Dig radius: ~0.72m (60% of width)
- Dig area: ~1.63 m²
- Volume per dig: ~0.049 m³
- Digs per full load: ~10 digs

## Why Robots Won't Get Stuck

1. **Always climbing**: Robots target the highest point, so they're always moving toward peaks, not into valleys
2. **Small digs**: 0.03m lowering can't create a pit deep enough to trap a robot
3. **Natural flattening**: As peaks are lowered, the next highest becomes the target
4. **Reeds-Shepp paths**: Proper non-holonomic paths ensure smooth, navigable routes

## Visualization

The terrain progressively flattens:
```
Initial:    /\  /\  /\        (peaks and valleys)
After 1:    /‾  /‾  /‾        (peaks slightly lowered)
After 10:   ‾‾  ‾‾  ‾‾        (nearly flat)
Final:      ___________       (completely flat)
```

##What's Different from Before

**REMOVED (was causing problems):**
- ❌ Complex stuck detection with counters and timers
- ❌ Emergency escape sequences
- ❌ Path validation and rejection
- ❌ Dig sites with volumes and depletion logic
- ❌ Scheduler task system
- ❌ Slice clamping and angle checking

**KEPT (essential for demonstration):**
- ✅ Reeds-Shepp path planning (unchanged)
- ✅ Terrain modification and visualization
- ✅ Robot payload tracking
- ✅ Dump and dig cycle

**ADDED (simple and smart):**
- ✅ Always-dig-highest logic
- ✅ Small, safe dig amounts
- ✅ Direct terrain queries (no cached dig sites)
- ✅ Clean state machine (just: digging vs returning home)

## Code Changes

### Files Created:
- `SimpleDigLogic.cs` - Core flattening algorithm

### Files Modified:
- `VehicleBrain.cs` - Completely rewritten (simple version)
- `SimulationDirector.cs` - Updated brain construction

### Files Backed Up:
- `VehicleBrain_OLD_BACKUP.txt` - Previous complex version

## Testing
Run the simulation and observe:
1. ✅ Robots follow smooth Reeds-Shepp curves
2. ✅ Terrain slowly flattens from peaks
3. ✅ No deep pits form
4. ✅ Robots complete dig/dump cycles without getting stuck
5. ✅ World total increases as dirt is extracted

## Future Enhancements (Optional)
- Add color gradient to terrain showing height (red=high, blue=low)
- Display "Work Remaining" percentage per sector
- Add statistics: total trips, average cycle time, etc.
- Visualize robot "thoughts" (current target, payload level)
