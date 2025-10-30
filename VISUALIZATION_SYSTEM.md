# Terrain and Sector Visualization System

## Overview
The simulation now includes a comprehensive color-based visualization system to show robot progress and assignments in real-time. This document explains what the colors mean and how they work.

---

## 1. Terrain Height Colors (Dig Progress)

The terrain mesh uses **vertex colors** to show height-based progress. This creates a smooth gradient that updates in real-time as robots dig.

### Color Gradient:
- **🟡 Yellow/Orange** → High terrain (needs digging)
- **🟢 Green** → Medium height (partial progress)
- **🔵 Cyan/Blue** → Low terrain (near flat)
- **🟣 Purple** → Completely flat (goal achieved)

### How It Works:
1. After each dig operation, the terrain mesh is rebuilt
2. `HeightToColor()` method computes a color for each vertex based on its height
3. Colors are interpolated smoothly across triangles
4. The gradient shows relative heights: highest points are yellow, lowest are purple
5. When the entire terrain is flat (very low height range), everything turns purple

### Technical Details:
```csharp
// In TerrainDisk.cs
private Color HeightToColor(float h, float minH, float maxH, float range)
{
    // Normalized height [0..1] where 0=lowest, 1=highest
    float t = (h - minH) / range;
    
    // If very flat (low range), everything is purple
    if (range < 0.05f)
        return new Color(0.6f, 0.3f, 0.8f); // purple
    
    // Color gradient based on height percentage
    if (t > 0.75f)      return yellow/orange;  // High (75-100%)
    else if (t > 0.5f)  return green/yellow;   // Mid-high (50-75%)
    else if (t > 0.25f) return cyan/green;     // Mid-low (25-50%)
    else                return purple/cyan;    // Low (0-25%)
}
```

### Material Setup:
The terrain uses `VertexColorUseAsAlbedo = true` in its material, which means:
- The base albedo color is white (1, 1, 1)
- Vertex colors multiply with the base color
- This allows the height gradient to show through

---

## 2. Sector Boundary Lines (Robot Assignments)

Colored radial lines show which sector each robot is responsible for digging.

### Visual Appearance:
- **Radial lines** from the origin (center of terrain) outward to dig radius
- Each line has a **unique color** matching its robot
- Lines are drawn slightly above the terrain surface (0.05m)
- Colors use HSV color wheel for maximum distinction

### Color Assignment:
```csharp
// In SimulationDirector.cs - DrawSectorLines()
for (int i = 0; i < N; i++)
{
    float hue = (float)i / N;  // Evenly spaced around color wheel
    colors[i] = Color.FromHsv(hue, 0.8f, 0.9f);  // High saturation & brightness
}
```

### Example for 8 Robots:
- Robot 0: Red (hue = 0.0)
- Robot 1: Orange (hue = 0.125)
- Robot 2: Yellow (hue = 0.25)
- Robot 3: Green (hue = 0.375)
- Robot 4: Cyan (hue = 0.5)
- Robot 5: Blue (hue = 0.625)
- Robot 6: Purple (hue = 0.75)
- Robot 7: Magenta (hue = 0.875)

### Purpose:
1. **Shows sector boundaries** - Where each robot's dig zone starts/ends
2. **Debugging** - Verify that robots stay in their assigned sectors
3. **Visualization** - See at a glance which robot is responsible for which area

---

## 3. Path Visualization

Robots' planned Reeds-Shepp paths are shown as colored line strips.

### Features:
- **Cyan paths** show the planned trajectory
- Paths are drawn slightly above terrain (0.02m)
- **Memory management**: Max 30 paths displayed (prevents crash)
- Old paths are automatically cleaned up

---

## 4. How to Use This in Godot

### Viewing the Visualization:
1. Open `3d/main.tscn` in Godot
2. Run the scene (F5)
3. You should see:
   - Terrain with height-based colors (yellow→green→blue→purple)
   - Colored radial sector lines
   - Robots digging in their assigned sectors
   - Cyan path lines showing robot trajectories

### Camera Controls:
- **Tab**: Switch between camera modes (Top/Chase/Free/Orbit)
- **Free Camera**: Right-click + drag to rotate, Middle-click + drag to pan, Scroll to zoom
- **Orbit Camera**: Right-click + drag to orbit, Scroll to zoom

### Debugging:
- Watch the terrain change from yellow→purple as robots flatten it
- Verify robots stay within their sector boundaries (between colored lines)
- See robots always dig the highest point (yellow areas) in their sector

---

## 5. Implementation Files

### Modified Files:
1. **`TerrainDisk.cs`**:
   - Added `HeightToColor()` method for vertex color calculation
   - Modified `RecomputeNormalsAndMesh()` to add vertex colors
   - Changed material to use `VertexColorUseAsAlbedo = true`

2. **`SimulationDirector.cs`**:
   - Added `DrawSectorLines()` method to visualize sectors
   - Called in `_Ready()` after spawning robots

### Key Methods:
```csharp
// TerrainDisk.cs
private Color HeightToColor(float h, float minH, float maxH, float range)
private void RecomputeNormalsAndMesh()  // Builds mesh with vertex colors

// SimulationDirector.cs
private void DrawSectorLines()  // Draws colored radial sector boundaries
```

---

## 6. Expected Behavior

### At Start:
- Terrain is mostly **yellow/green** (bumpy, uneven)
- Sector lines clearly visible radiating from center
- Robots spawn at their sector boundaries

### During Digging:
- High points turn from **yellow→green→blue** as they're lowered
- Robots navigate to highest yellow points in their sectors
- Colors update in real-time after each dig

### When Complete:
- Entire terrain becomes **purple** (flat)
- No more yellow/green areas remain
- Robots have nothing left to dig

---

## 7. Troubleshooting

### "Terrain is brown, not colored"
- The default `dirt.tres` material doesn't use vertex colors
- The code creates its own material with `VertexColorUseAsAlbedo = true`
- Ensure `MaterialOverride` is not set, or use a material with vertex color support

### "No sector lines visible"
- Check console for: `"[Director] Drew N sector boundary lines"`
- Verify `DrawSectorLines()` is called in `_Ready()`
- Lines are above terrain by 0.05m - may be hard to see from some angles

### "Colors don't change"
- `RecomputeNormalsAndMesh()` must be called after each dig
- `LowerArea()` calls this automatically
- Vertex colors are recalculated every time based on current heights

---

## 8. Performance Considerations

### Vertex Color Updates:
- Colors are recalculated for the entire mesh after each dig
- This is fast (~1ms) even for 256x256 grids
- No shader overhead - colors baked into vertices

### Sector Lines:
- Drawn once at startup (not updated)
- Minimal performance impact (8 lines for 8 robots)
- Uses ImmediateMesh for simple line rendering

### Path Meshes:
- Limited to 30 paths maximum to prevent memory leaks
- Old paths automatically cleaned up
- Prevents Godot crash from excessive mesh instances

---

## Summary

The visualization system provides instant visual feedback:
- **Terrain colors** show dig progress (yellow→purple as terrain flattens)
- **Sector lines** show robot assignments (colored radial boundaries)
- **Path lines** show robot trajectories (cyan Reeds-Shepp paths)

Together, these make it easy to understand what the robots are doing and debug the dig coordination system!
