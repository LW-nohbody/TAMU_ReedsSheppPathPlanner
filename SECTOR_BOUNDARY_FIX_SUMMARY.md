# Sector Boundary Stuck Issue - Root Cause & Fix

## Problem Identified

**Robots were getting stuck ON the sector boundary lines** at positions like:
- Robot_8: (1.414, -1.414) - Edge of Sector 7
- Robot_4: (-1.414, 1.414) - Edge of Sector 3  
- Robot_6: (-1.414, -1.414) - Edge of Sector 5
- Robot_3: (0.0, 2.0) - Edge of Sector 2
- Robot_2: (1.414, 1.414) - Edge of Sector 1
- Robot_1: (2.0, 0.0) - Edge of Sector 0

These are **exactly on the sector boundary line positions** in the 3D scene.

## Root Cause

The `GetBestDigPoint()` and `FindHighestInSector()` methods were sampling points **exactly at the sector boundary angles**:

```csharp
// OLD CODE - samples at exact boundaries
for (int a = 0; a < samples; a++)
{
    float t = (float)a / (samples - 1);  // When a=0, t=0 (start boundary)
    float theta = Mathf.Lerp(thetaMin, thetaMax, t);  // When a=samples-1, t=1 (end boundary)
    // ... creates points at sector edges
}
```

When `a = 0`: theta = thetaMin (start boundary)  
When `a = samples-1`: theta = thetaMax (end boundary)  

These boundary positions correspond to the sector line geometry in the scene, causing collisions.

## Solution Implemented

Added a **boundary buffer** to shrink the sampling area inward from the sector edges:

```csharp
// NEW CODE - avoids exact boundaries
float boundaryBuffer = 0.15f;  // ~8.6 degrees
float thetaMinInner = thetaMin + boundaryBuffer;
float thetaMaxInner = thetaMax - boundaryBuffer;

// Ensure range doesn't invert for small sectors
if (thetaMinInner >= thetaMaxInner)
{
    thetaMinInner = (thetaMin + thetaMax) / 2f - boundaryBuffer * 0.5f;
    thetaMaxInner = (thetaMin + thetaMax) / 2f + boundaryBuffer * 0.5f;
}

// Sample within inner bounds
for (int a = 0; a < samples; a++)
{
    float t = samples > 1 ? (float)a / (samples - 1) : 0.5f;
    float theta = Mathf.Lerp(thetaMinInner, thetaMaxInner, t);
    // ... creates points safely inward from boundaries
}
```

## Files Modified

### 3d/ Folder
1. **3d/Scripts/SimCore/Core/RobotCoordinator.cs**
   - Updated `GetBestDigPoint()` to use thetaMinInner/thetaMaxInner
   - Updated fallback to use inner sector bounds

2. **3d/Scripts/SimCore/Core/SimplifiedDigBrain.cs**
   - Updated `FindHighestInSector()` to use inner sector bounds
   - Applied same boundary buffer approach

### DigSim3D/ Folder
1. **DigSim3D/Scripts/Services/RobotCoordinator.cs**
   - Updated `GetSortedCandidates()` to use inner sector bounds
   - Updated `GetBestDigPoint()` fallback

## Expected Results

✅ **Robots will no longer get stuck on sector boundaries**  
✅ **All dig points now sampled safely inward from sector edges**  
✅ **Prevents collision with sector line geometry**  
✅ **Fallback positions also use inner sector bounds**  

## Build Status

- **3d/ build**: ✅ Success (0 warnings, 0 errors)
- **DigSim3D/ build**: ✅ Success (0 warnings, 0 errors)

## Commit

`8310d4d` - Fix: Prevent robots from getting stuck on sector boundary lines
