# Merge Plan: 3d/ → DigSim3D

## Objective
Merge improvements and simplified swarm dig logic from `3d/` into `DigSim3D/` while respecting DigSim3D's cleaner architecture and folder structure.

## Key Improvements in 3d/
1. **Simplified VehicleBrain** (3d/Scripts/SimCore/Godot/VehicleBrain.cs)
   - Global highest-point search (no sector restriction)
   - Reactive swarm logic (find target, go, dig, dump, repeat)
   - No stuck detection or complex recovery (simpler, more robust)
   - Clear payload tracking and status display

2. **SimulationConfig** (3d/Scripts/Core/SimulationConfig.cs)
   - Global configuration for runtime parameter control
   - Properties: MaxDigDepth, MaxRobotSpeed, RobotLoadCapacity
   - Static class for easy access throughout codebase

3. **SimulationSettingsUI** (3d/Scripts/UI/SimulationSettingsUI.cs)
   - Advanced UI panel for real-time parameter adjustment
   - Three sliders: Dig Depth, Max Speed, Load Capacity
   - Beautiful, modern, positioned in top-right corner
   - Immediate feedback and visual layout

4. **RobotCoordinator Updates** (3d/Scripts/SimCore/Core/RobotCoordinator.cs)
   - Sector boundary buffer fix (prevents robots getting stuck on lines)
   - Simplified coordination without complex stuck detection

## DigSim3D Current Architecture
```
DigSim3D/Scripts/
├── App/              (Godot integration layer - Controllers)
│   ├── SimulationDirector.cs
│   ├── VehicleBrain.cs (currently has stuck detection)
│   └── [other visualizers]
├── Domain/           (Core data models)
│   ├── WorldState.cs
│   ├── PlannedPath.cs
│   ├── Pose.cs
│   └── [other models]
├── Services/         (Business logic & algorithms)
│   ├── Planning/     (Path planning algorithms)
│   ├── Interfaces/   (Service contracts)
│   └── [other services]
└── Debug/            (Debug utilities)
```

## Merge Strategy

### Phase 1: Configuration & UI (Non-Breaking)
**Goal**: Add runtime parameter control without disrupting existing logic

1. **Create** `DigSim3D/Scripts/Config/SimulationConfig.cs`
   - Copy from 3d/Scripts/Core/SimulationConfig.cs
   - Adjust namespace to `DigSim3D.Config`
   - Adjust static property setters for DigSim3D components

2. **Create** `DigSim3D/Scripts/App/SimulationSettingsUI.cs`
   - Copy from 3d/Scripts/UI/SimulationSettingsUI.cs
   - Update namespace to `DigSim3D.App`
   - Update references to SimulationConfig

3. **Update** `DigSim3D/Scripts/App/SimulationDirector.cs`
   - Add initialization of SimulationSettingsUI in _Ready()
   - Wire up SimulationConfig parameter updates to vehicles/terrain

**Expected Result**: UI works, but robots still use old VehicleBrain logic

---

### Phase 2: Simplified VehicleBrain (Main Logic Change)
**Goal**: Replace complex stuck-detection logic with simple swarm dig logic

1. **Backup** existing VehicleBrain.cs as VehicleBrain_OLD_BACKUP.txt

2. **Replace** `DigSim3D/Scripts/App/VehicleBrain.cs`
   - Copy from 3d/Scripts/SimCore/Godot/VehicleBrain.cs (simplified version)
   - Update namespace to `DigSim3D.App`
   - Update type references:
     - `VehicleAgent3D` → `VehicleVisualizer`
     - `TerrainDisk terrain` parameter stays same
     - `WorldState world` parameter stays same
     - Path planning interfaces stay same
   
3. **Key Changes in VehicleBrain**:
   - Remove: Complex stuck detection, recovery strategies, failure memory
   - Keep: Global highest-point search, payload tracking, dump logic
   - Simplify: No sector restriction, just find highest point anywhere
   - Outcomes: Robots find terrain reactively, only need goal + dig logic

4. **Update** terrain access in VehicleBrain:
   - Use `_terrain.GetHeightAt()` for finding highest points
   - Use `_terrain.DigAt()` for digging
   - Ensure compatibility with DigSim3D's TerrainDisk API

**Expected Result**: Robots now use simplified swarm logic, no stuck issues

---

### Phase 3: Integration & Testing
**Goal**: Ensure all systems work together

1. **Verify** namespace consistency
   - All imports use DigSim3D.* namespaces
   - Service injection still works (planner, coordinator, etc.)

2. **Update** any references in SimulationDirector:
   - Remove sector-based UI or repurpose for visualization only
   - Ensure VehicleBrain initialization works with new constructor
   - Wire SimulationConfig updates to VehicleBrain instances

3. **Test** build:
   - `dotnet build DigSim3D/Reeds-Shepp_3D_Test.csproj`
   - Fix any compilation errors

4. **Test** runtime:
   - Load DigSim3D scene in Godot
   - Verify UI appears
   - Verify robots start digging
   - Verify parameters change robot behavior
   - Check that robots don't get stuck

---

## Detailed File Mapping

| Source (3d/) | Destination (DigSim3D/) | Action | Notes |
|---|---|---|---|
| Scripts/SimCore/Godot/VehicleBrain.cs | Scripts/App/VehicleBrain.cs | Replace | Simplified, no stuck detection |
| Scripts/Core/SimulationConfig.cs | Scripts/Config/SimulationConfig.cs | Create | New config file |
| Scripts/UI/SimulationSettingsUI.cs | Scripts/App/SimulationSettingsUI.cs | Create | Add UI to App layer |
| Scripts/SimCore/Core/RobotCoordinator.cs | Scripts/Services/RobotCoordinator.cs | Review | Check if sector boundary fix needed |

---

## Type/API Mapping

### VehicleAgent3D → VehicleVisualizer
- DigSim3D uses `VehicleVisualizer` for robot visuals
- Public APIs should be compatible (GlobalTransform, SetPath, etc.)
- Constructor parameters may differ - will need to verify

### WorldState
- Both versions use `WorldState` for global state tracking
- APIs should be compatible (TotalDirtExtracted, etc.)

### IPathPlanner
- Both use same interface
- Should work without changes

### TerrainDisk
- Both versions use same terrain system
- APIs: GetHeightAt(), DigAt(), SamplePeak() should be compatible

---

## Risk Assessment

### Low Risk
- Adding Config and UI (non-breaking, additive only)
- Namespace changes (local to DigSim3D only)

### Medium Risk
- Replacing VehicleBrain (behavior changes)
- API compatibility between VehicleAgent3D and VehicleVisualizer

### Mitigation
- Keep old VehicleBrain backup for comparison
- Test incrementally: Config → UI → VehicleBrain logic
- Run DigSim3D builds at each phase to catch errors early

---

## Post-Merge Cleanup

1. **Remove** old stuck-detection code from DigSim3D (if simplifying further)
2. **Update** documentation in code comments
3. **Commit** with clear message: "Merge: simplified swarm dig logic from 3d/"
4. **Test** full simulation end-to-end
5. **Optional**: Remove sector UI or repurpose for visualization

---

## Expected Outcomes

✅ DigSim3D now has:
- Simplified, robust robot brain (no stuck detection)
- Global dig strategy (find highest, dig, dump)
- Runtime parameter control (dig depth, speed, load capacity)
- Beautiful UI for real-time adjustments
- All benefits of 3d/ improvements, with DigSim3D's clean architecture

✅ Robots will:
- Discover terrain reactively while moving
- Find highest points globally (no sector restriction)
- Dig and dump automatically
- Never get stuck or require complex recovery
- Respond to UI parameter changes in real-time

