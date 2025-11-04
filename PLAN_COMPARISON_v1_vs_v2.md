# Plan Comparison: v1 (Pre-Scanning) vs v2 (Reactive Swarm)

## Problem Statement
Robots get stuck in ditches and can't climb hills because they **cannot pre-scan terrain**.

---

## v1 Approach: ❌ WRONG (Requires Pre-Scanning)

```
Robot thinks: "Let me plan ahead to avoid obstacles"
Reality:      "I don't know what's ahead until I'm there!"

Pre-planning assumes you know terrain in advance
↓
But robots discover terrain as they move
↓
Pre-planned paths become invalid
↓
Result: Robots still get stuck
```

### v1 Features (that don't work):
- ❌ Terrain-aware path planning
- ❌ Slope validation before movement
- ❌ Cost functions for terrain difficulty
- ❌ Dynamic rerouting based on future slopes

**Why it fails**: These all require knowing terrain in advance!

---

## v2 Approach: ✅ CORRECT (Reactive Swarm)

```
Robot thinks: "I'm stuck NOW. I dig NOW."
Reality:      "Dig obstacles as encountered, push forward"

No pre-scanning needed
↓
React to immediate feedback
↓
Dig yourself forward through any obstacle
↓
Result: Always makes progress
```

### v2 Features (that work with swarm approach):

#### 1. **FAST Stuck Detection** (0.5s instead of 2s)
```
Frames needed to detect: 30 (not 60)
Movement threshold:      0.3m (not 0.5m)
→ Faster reaction time
```

#### 2. **Aggressive Forward Digging**
```
Stuck? → Dig the waypoint ahead (1.2m forward)
Dig size: 30% bigger radius
Dig depth: 0.15m per attempt
↓
Terrain flattens ahead
↓
Robot can now move
```

#### 3. **Waypoint Timeout Digging**
```
At waypoint for >0.5s? → Dig around it
Creates path through obstacles
No replanning needed
```

#### 4. **Terrain Collapse Mechanics**
```
Dig creates:
- Deep center (1.5x depth)
- Light edges (0.1x depth)

Gravity makes it collapse
↓
Forms natural ramp
↓
Robot climbs out automatically
```

---

## Side-by-Side Comparison

### Stuck in Pit Scenario

**v1 Approach:**
```
Robot: "I'm stuck! Let me analyze terrain slopes nearby"
       "I'll find escape routes using pre-computed paths"
Result: Fails because no pre-computation happened
        Robot stays stuck, tries replanning, fails again
```

**v2 Approach:**
```
Robot: "I'm stuck! START DIGGING FORWARD"
       30 frames later, digs aggressive 1.2m ahead
       Dug area creates ramp
Result: Terrain collapses into ramp
        Robot climbs out in <2 seconds
        Mission: Accomplished ✓
```

---

### Steep Hill Scenario

**v1 Approach:**
```
Robot: "Is this hill climbable? *analyzes slope*"
       "Better try to add waypoints to reduce grade"
Result: Replanning takes time
        Path might fail if robot discovers even steeper section
        Possible stuck situation
```

**v2 Approach:**
```
Robot: "I see a waypoint uphill. Moving toward it..."
       "Can't reach it? DIGGING."
       30 frames → dig waypoint location
       30 frames → dig next waypoint
       ...
Result: Gradually digs stairs up the hill
        Never gets stuck because immediately digs obstacles
        Mountain ✓
```

---

### Multi-Robot Swarm Scenario

**v1 Approach:**
```
Robot A: "Planning path avoiding terrain..."
Robot B: "Planning path avoiding terrain..."
Robot C: "Planning path avoiding terrain..."
Result: Independent planners = wasted computation
        No coordinated terrain manipulation
        Robots interfere with each other
```

**v2 Approach:**
```
Robot A: "Stuck! Digging forward"         → Creates ramp
Robot B: "Stuck! Digging forward"         → Creates ramp
Robot C: "Stuck! Digging forward"         → Creates ramp
Result: Natural terrain shaping through collective digging
        Each robot's digs help neighbors
        Swarm self-organizes around terrain obstacles
        Efficient + Elegant ✓
```

---

## Key Philosophy Shift

### v1: **"Know thy terrain"**
- Assumes global knowledge
- Requires pre-computation
- Fails for true swarm agents

### v2: **"Dig thy path"**
- Local, reactive behavior
- Zero pre-computation needed
- Perfect for autonomous swarms
- **Self-recovery through action**

---

## Why v2 Works for Your Use Case

✅ **Robots are decentralized**
- No central planner
- Each acts independently
- Local coordination only (claims)

✅ **Terrain discovery is in-situ**
- Robots learn terrain by moving through it
- Pre-scanning not possible
- Reaction is the only option

✅ **Digging is the solution**
- Robot can dig obstacles
- Dug terrain can be climbed
- Aggressive digging = fast recovery

✅ **Swarm benefits from this**
- Each robot's digs help others
- Terrain gradually smooths
- Emergent behavior from simple rules

✅ **No pathfinding needed**
- Just dig forward until you can move
- No complex algorithms
- Robust and simple

---

## Implementation Path

```
v2 = Reactive + Aggressive + Simple
   = Fast stuck detection (30f)
   + Forward digging (1.2m ahead)
   + Waypoint timeout digging (30f stuck)
   + Terrain collapse pattern
   + Done

No complex algorithms
No global planning
No pre-computation
```

---

## Success Metrics

### If v2 Works:
✓ Robots escape pits in <2 seconds  
✓ Robots climb 60° slopes with digging  
✓ Robots never oscillate - always making progress  
✓ Swarm self-organizes around terrain  
✓ Zero pre-planning needed  
✓ Simple code, easy to tune  

### If it doesn't work:
- Add more aggressive digging (increase radius/depth)
- Speed up stuck detection (reduce threshold)
- Tune terrain collapse patterns
- All easily configurable!

