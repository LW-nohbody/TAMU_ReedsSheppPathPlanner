# Godot Crash Fix - Memory Leak in Path Drawing

## Problem
Godot crashed with error:
```
Thread 28 Crashed:
PrimitiveMesh::~PrimitiveMesh() + 252
godotsharp_internal_refcounted_disposed + 240
```

This is a **C# finalizer crash** caused by a memory leak in the path drawing code.

## Root Cause
The `DrawPathProjectedToTerrain()` method in `SimulationDirector.cs` creates new `MeshInstance3D` objects every time a path is drawn, but **never frees them**. Over time, hundreds or thousands of these objects accumulate, causing:
- Memory exhaustion
- Garbage collector overload
- Crashes during C# finalization

## Affected Code
```csharp
// SimulationDirector.cs line ~352
private void DrawPathProjectedToTerrain(Vector3[] points, Color col)
{
    var mi = new MeshInstance3D();  // ❌ Created every frame
    var im = new ImmediateMesh();    // ❌ Never freed
    mi.Mesh = im;
    AddChild(mi);  // ❌ Accumulates forever
    // ... draw path
}
```

## Solutions

### Option 1: Disable Path Drawing (Quick Fix)
Comment out the path drawing calls to verify this is the issue:

```csharp
// In SimulationDirector.cs, comment out DrawPathProjectedToTerrain calls
// Example: Around line 200-250 where paths are drawn
```

### Option 2: Store and Reuse Path Meshes (Proper Fix)
Track path meshes and clean up old ones:

```csharp
private List<MeshInstance3D> _pathMeshes = new List<MeshInstance3D>();
private const int MAX_PATH_MESHES = 50; // Limit displayed paths

private void DrawPathProjectedToTerrain(Vector3[] points, Color col)
{
    if (points == null || points.Length < 2) return;

    // Clean up old meshes if too many
    while (_pathMeshes.Count >= MAX_PATH_MESHES)
    {
        var old = _pathMeshes[0];
        _pathMeshes.RemoveAt(0);
        old.QueueFree();
    }

    var mi = new MeshInstance3D();
    var im = new ImmediateMesh();
    mi.Mesh = im;
    AddChild(mi);
    _pathMeshes.Add(mi);  // Track it

    // ... rest of drawing code
}

// In _ExitTree or cleanup:
public override void _ExitTree()
{
    foreach (var mesh in _pathMeshes)
    {
        if (IsInstanceValid(mesh))
            mesh.QueueFree();
    }
    _pathMeshes.Clear();
}
```

### Option 3: Use Single Reusable Mesh (Most Efficient)
Replace all path meshes with one that gets updated:

```csharp
private MeshInstance3D _pathDebugMesh;

private void EnsurePathDebugMesh()
{
    if (_pathDebugMesh == null || !IsInstanceValid(_pathDebugMesh))
    {
        _pathDebugMesh = new MeshInstance3D();
        _pathDebugMesh.Mesh = new ImmediateMesh();
        AddChild(_pathDebugMesh);
    }
}

private void DrawPathProjectedToTerrain(Vector3[] points, Color col)
{
    if (points == null || points.Length < 2) return;
    
    EnsurePathDebugMesh();
    var im = (ImmediateMesh)_pathDebugMesh.Mesh;
    im.ClearSurfaces();  // Clear old path
    
    // ... draw new path on same mesh
}
```

## Recommended Action

**For immediate testing**, use Option 1 to verify the simulation works without path drawing.

**For production**, implement Option 2 or 3 to properly manage mesh lifecycle.

## How to Test

1. Apply one of the fixes above
2. Run the simulation in Godot
3. Let it run for 30+ seconds
4. Watch for:
   - No crashes
   - Stable memory usage
   - Robots still digging properly

## Additional Memory Leaks to Check

Similar issues may exist in:
- `DrawMarkerProjected()` (line ~376)
- Any other code that creates nodes without freeing them
- VehicleAgent3D if it creates debug visualization

## Prevention

Always follow this pattern in Godot C#:
```csharp
// ✅ Good: Store and clean up
var node = new Node3D();
AddChild(node);
// Later: node.QueueFree();

// ❌ Bad: Create and forget
AddChild(new Node3D()); // Memory leak!
```
