# Quick Reference - Color Meanings

## 🌈 What Do The Colors Mean?

### Terrain Colors (Height-Based)
Your terrain should look like a colorful gradient that changes as robots dig:

| Color | Meaning | What to Do |
|-------|---------|------------|
| 🟡 **Yellow/Orange** | **Highest points** - These areas need the most digging | Robots should target these |
| 🟢 **Green** | **Medium height** - Partially flattened | Keep digging nearby yellow areas |
| 🔵 **Cyan/Blue** | **Low areas** - Almost flat | Nearly done! |
| 🟣 **Purple** | **Flat terrain** - Goal achieved! | Success! No more work needed |

### Sector Lines (Robot Assignments)
Colored radial lines from the center show which robot is responsible for which area:

- Each line has a **unique bright color**
- Lines divide the terrain into **wedge-shaped sectors**
- **8 robots** = 8 equally-sized pie slices

**Example colors for 8 robots:**
1. 🔴 Red
2. 🟠 Orange  
3. 🟡 Yellow
4. 🟢 Green
5. 🔵 Cyan
6. 🔷 Blue
7. 🟣 Purple
8. 🌸 Magenta

### Path Lines (Robot Trajectories)
- **Cyan line strips** = Planned Reeds-Shepp paths
- Shows where robot will drive next
- Curves indicate forward/reverse maneuvers

---

## 👀 What You Should See

### At Start:
```
🟡🟡🟡🟡    ← Terrain mostly yellow (bumpy)
🟢🟡🟢🟡    
🟡🟢🟡🟢    
🟢🟡🟢🟡    

+ 8 colored radial lines from center
+ Robots at spawn positions
```

### During Digging:
```
🟢🟢🔵🟡    ← Yellow areas shrinking
🔵🟢🔵🟢    
🟢🔵🟢🔵    ← More green/blue appearing
🔵🟢🔵🟡    

+ Robots moving to yellow spots
+ Cyan paths showing routes
+ Sector lines still visible
```

### When Complete:
```
🟣🟣🟣🟣    ← Everything purple!
🟣🟣🟣🟣    
🟣🟣🟣🟣    ← Completely flat
🟣🟣🟣🟣    

+ Mission accomplished!
+ All terrain at same height
```

---

## 🎯 Expected Behavior

### ✅ Normal Operation:
- Terrain starts mostly **yellow/green** (bumpy)
- Robots drive to **yellow areas** in their sectors
- After each dig, **yellow → green → blue → purple**
- Eventually entire terrain becomes **purple** (flat)
- Each robot stays in its colored sector zone

### ❌ Something's Wrong If:
- **Terrain stays brown**: Vertex colors not enabled (check material)
- **No sector lines**: DrawSectorLines() not called
- **All one color**: Height range too small or flat already
- **Robots ignoring yellow**: Dig logic not working

---

## 🎮 Controls Reminder

| Key | Action |
|-----|--------|
| **Tab** | Switch camera modes |
| **Right-drag** | Rotate camera (Free/Orbit) |
| **Middle-drag** | Pan camera (Free) |
| **Scroll** | Zoom in/out |
| **Esc** | Stop simulation |

---

## 🐛 Quick Debug Checks

### If terrain has no colors:
1. Check console for: `"[Director] Terrain OK:"`
2. Verify TerrainDisk.cs has `HeightToColor()` method
3. Material should have `VertexColorUseAsAlbedo = true`

### If no sector lines:
1. Check console for: `"[Director] Drew N sector boundary lines"`
2. Verify SimulationDirector.cs has `DrawSectorLines()` method
3. Try Top camera view (press Tab until you see from above)

### If robots stuck:
1. New system shouldn't get stuck!
2. Check console for `"[Brain] Robot_X going to..."`
3. Verify SimpleDigLogic.cs exists and is being used

---

## 📖 More Info

For detailed explanations, see:
- **`VISUALIZATION_SYSTEM.md`** - Complete visualization guide
- **`COMPLETE_OVERVIEW.md`** - Full system documentation  
- **`SYSTEM_LOGIC_EXPLAINED.md`** - How robots choose dig points

---

## 💡 Pro Tips

1. **Use Top Camera** (Tab) for best view of terrain colors
2. **Watch yellow areas** - robots should always target these
3. **Count sector lines** - should match number of robots
4. **Check console** - lots of helpful debug messages
5. **Be patient** - flattening takes time depending on terrain size

---

**Remember**: Yellow = High (dig me!), Purple = Flat (done!), Colored lines = Robot zones 🎨
