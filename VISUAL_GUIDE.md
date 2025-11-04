# Visual Guide: Reactive Swarm Terrain Climbing

## The Core Loop: "Dig Your Way Forward"

```
┌─────────────────────────────────────────────────────────────┐
│                    ROBOT MOVEMENT CYCLE                     │
└─────────────────────────────────────────────────────────────┘

START → Plan path to target
           ↓
    Move toward waypoint (follow path)
           ↓
    ┌──────────────────────────┐
    │ Reaching waypoint? (< 1m)│
    └──────────────────────────┘
           ↓ YES
    ┌──────────────────────────────────┐
    │ Can reach it? (30 frame timeout) │
    └──────────────────────────────────┘
        ↓ YES              ↓ NO
      NEXT WP           STUCK!
        ↓
    ┌────────────────────────┐
    │ AGGRESSIVE DIG FORWARD │ ← NEW: Dig 1.2m ahead
    │ Release claim          │
    │ Dig 30% bigger radius  │
    │ Claim new dig site     │
    └────────────────────────┘
        ↓
    Terrain flattens ahead
        ↓
    Can now reach waypoint
        ↓
    Continue moving ✓
```

---

## Pit Escape Mechanism

```
                    BEFORE (Stuck Forever)
┌────────────────────────────────────────┐
│                                        │
│        Robot enters pit                │
│             O                          │
│          /  |  \                       │
│        /    |    \  ← Pit walls        │
│       /  ╔──┴──╗   \                   │
│      │   ║ BOT ║   │                   │
│      │   ║ STUCK   │  ← Gets stuck     │
│       \  └──────┘  /                   │
│        \    |    /                     │
│         \   |   /                      │
│          \  |  /                       │
│           └─┴─┘                        │
│                                        │
│    Result: STUCK FOREVER ✗             │
└────────────────────────────────────────┘

                    AFTER (30-60 seconds)
┌────────────────────────────────────────┐
│                                        │
│  0.5s: AGGRESSIVE DIG FORWARD          │
│             O                          │
│          /  |  \                       │
│        /    |    \                     │
│       /  ╔──────╗  \                   │
│      │   ║ BOT  ║   │                  │
│      │   ║ DIGGING→  │ ← Digs ahead    │
│       \  ╔──────╝  /                   │
│        \ │∨∨∨∨∨∨ /  ← Dig creates      │
│         \│      /     loose material   │
│          └─────┘                       │
│          /     \                       │
│                                        │
│  1.0s: TERRAIN COLLAPSE (physics)      │
│             O                          │
│          /  ╱╲ \                       │
│        / ╱       ╲ \                   │
│      / ╱    BOT    ╲ \                 │
│    / ╱   ∧∧∧∧∧∧   ╱ ╱  ← Collapse     │
│   ╱                                    │
│                                        │
│  Result: RAMP FORMED ✓                 │
└────────────────────────────────────────┘

                   ROBOT ESCAPES (1-2 sec)
┌────────────────────────────────────────┐
│                                        │
│             O                          │
│            /|\                         │
│           / | \                        │
│          /  |  \  ← Climbing ramp      │
│         /   |   \                      │
│        /    |    \                     │
│       /     |     \                    │
│       ╱╱╱╱╱╱╱╱╱╱╱╱  ← Ramp (from dig) │
│       Escaped ✓                        │
└────────────────────────────────────────┘
```

---

## Hill Climb: Step-by-Step

```
┌─────────────────────────────────────────────────────┐
│  Scenario: Robot facing 45° hill                    │
└─────────────────────────────────────────────────────┘

WAYPOINT 3 ■
     ▲
     │
     │  ▲▲▲
WAYPOINT 2 ║ ▲▲▲
     │▲▲▲│
     │▲ ▲│
WAYPOINT 1│▲│ ← Too steep
     │○→→→│
START ■╱╱╱╱│
         ↑
    Natural hill (can't climb)

─────────────────────────────────────────────────────

Step 1: Robot reaches WAYPOINT 1 approach
┌───────┐
│ GET   │
│ STUCK │ (Can't climb 45° slope)
└───────┘
     ■
    /│  At frame 30 of being stuck:
   / │  → AGGRESSIVE DIG (1.2m ahead)
  /  │
 ╱   │
START ■

─────────────────────────────────────────────────────

Step 2: Dug area becomes ramp
┌───────┐
│ RAMP  │
│FORMED │ (From dig collapse)
└───────┘
     ■
   ╱ ╱  Now climbable!
  ╱ ╱
 ╱ ╱
START ■

─────────────────────────────────────────────────────

Step 3: Reach WAYPOINT 1
     ■ ← Now reachable
   ╱ │
  ╱  │
 ╱   │
START ■ Repeat for WAYPOINT 2 and 3

─────────────────────────────────────────────────────

Result: Mountain ✓
     ■ ← REACHED
    ╱ │ ← Dug stairs
   ╱  ■ ← WAYPOINT 3
  ╱  ╱ │
 ╱  ╱  ■ ← WAYPOINT 2
   ╱  ╱ │
  ╱  ╱  │
 ╱  ╱   ■ ← WAYPOINT 1
START ■
```

---

## Terrain Collapse Mechanics

```
┌──────────────────────────────────────────────────┐
│  Dig Pattern: Deep center, Light edges           │
│  Creates unstable terrain that collapses inward  │
└──────────────────────────────────────────────────┘

BEFORE DIG:
        ▲
       ███
      █████
     ███████
    █████████
   ███████████
  █████████████
 ─────────────────

DIGGING:
        ▼ Dig here!
       ╱█╲
      ╱█████╲
     ╱███████╲
    ╱█████████╲
   ╱███████████╲
  ╱█████████████╲
 ─────────────────
 
 Deep center (1.5x depth) ↓
 Medium middle (1.0x depth) ↓
 Light outer (0.4x depth) ↓
 Barely edges (0.1x depth) ← Loose material!

COLLAPSE (Physics takes over):
       ╱ ╲ Loose material
      ╱   ╲ slides down
     ╱     ╲
    ╱───────╲ ← Ramp forms!
   ╱         ╲
  ╱-----------╲
 ─────────────────

Gravity does the work!
No special escape logic needed
Terrain naturally becomes climbable
```

---

## Stuck Detection Timeline

```
┌─────────────────────────────────────────────────────┐
│  BEFORE: Long wait = slow recovery                  │
└─────────────────────────────────────────────────────┘

Time:  0         1         2         3 seconds
       ├─────────┼─────────┼─────────┼
       
Robot stuck at frame 0
       |
       WAIT WAIT WAIT
       |
       (60 frames = 1 second)
       |
Frame 60: Finally detected as stuck
       |
       Try recovery...
       |
Result: WASTED 2+ seconds ✗

┌─────────────────────────────────────────────────────┐
│  AFTER: Fast detection = instant recovery           │
└─────────────────────────────────────────────────────┘

Time:  0         1         2         3 seconds
       ├─────────┼─────────┼─────────┼
       
Robot stuck at frame 0
       |
       WAIT (only 30 frames = 0.5 sec)
       |
Frame 30: IMMEDIATELY START AGGRESSIVE DIG
       |
       Digs forward at 1.2m
       |
       Terrain collapses into ramp
       |
Frame 60: Robot can move again ✓
       |
Result: ESCAPED in 1-2 seconds ✓
```

---

## Recovery Escalation

```
Level 0: AGGRESSIVE FORWARD DIG (Default)
┌─────────────────────────────┐
│ Robot stuck for 30 frames?  │
│ → Dig 1.2m ahead            │
│ → Release claim             │
│ → Claim new dig site        │
│ → Return to normal path     │
└─────────────────────────────┘
         │
         ├─→ Works? Continue ✓
         │
         └─→ Still stuck?
                 │
                 ↓
Level 1: FIND ALTERNATIVE TARGET
┌─────────────────────────────┐
│ Still stuck?                │
│ → Release claim             │
│ → Pick NEW dig target       │
│ → Try different direction   │
└─────────────────────────────┘
         │
         ├─→ Works? Continue ✓
         │
         └─→ Still stuck?
                 │
                 ↓
Level 2: GO HOME
┌─────────────────────────────┐
│ Persistent stuckness?       │
│ → Return home               │
│ → Dump payload              │
│ → Restart sector            │
└─────────────────────────────┘
         │
         └─→ Continue with new sector ✓

```

---

## Waypoint Timeout Mechanism

```
Robot moving toward waypoint:

    Target ■
      ▲
      │ Distance < 1.0m
      │
      │ Frame 0-29: Keep trying
      │ Frame 30+: STUCK HERE
      │
      ↓
    Robot ○ ← CAN'T REACH (blocked terrain)


At Frame 30 (timeout):
    Target ■
      ▲
      │  ╱╲ ← DIG WAYPOINT
      │ ╱  ╲
      │╱    ╲ ← Creates path
      ↓
    Robot ○ → Now can reach!

```

---

## Swarm Benefit: Collective Terrain Smoothing

```
Initial terrain (rough, multiple pits):

     ▲
     │  ▲▲▲     ▲▲      ▲▲▲▲
     │▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
     │▲○▲  ▲○▲  ▲○▲  ▲○▲  ▲▲▲
  ───┴─────────────────────────
     Robots A, B, C, D at 4 pits

After 30 seconds of autonomous digging:

     ▲
     │
     │  ↗  ↗  ↗  ↗
     │↗  ↗  ↗  ↗  ↗
  ───┴─────────────────────────
     Terrain gradually smooths

Each robot's digs create ramps
Ramps help all robots
Terrain becomes progressively easier
Emergent smooth terrain!
```

---

## Decision Tree: What Happens Next

```
                START
                 │
                 ▼
         ┌──────────────┐
         │ Robot moving?│
         └──────────────┘
          │             │
    YES   │             │   NO
          │             │
          ▼             ▼
    Continue    ┌───────────────┐
    path        │ Frames stuck? │
                └───────────────┘
                 │             │
            < 30f │             │ >= 30f
                 │             │
                 ▼             ▼
             WAIT         AGGRESSIVE
             Loop         DIG FORWARD
                          │
                          ▼
                     Dig 1.2m ahead
                          │
                          ▼
                  Try again to move
                          │
                     ┌────┴────┐
                     │          │
                   YES          NO
                     │          │
                     ▼          ▼
                 SUCCESS     LEVEL 1
                             (retry)
```

