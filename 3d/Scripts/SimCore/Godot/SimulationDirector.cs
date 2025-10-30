using Godot;
using System;
using System.Collections.Generic;
using SimCore.Core;
using SimCore.Services;
using System.Runtime;

public partial class SimulationDirector : Node3D
{
    [Export] public PackedScene VehicleScene;
    [Export] public NodePath VehiclesRootPath;
    [Export] public NodePath CameraTopPath;
    [Export] public NodePath CameraChasePath;
    [Export] public NodePath CameraFreePath;
    [Export] public NodePath CameraOrbitPath;

    [Export] public NodePath TerrainPath;

    // Spawn / geometry
    [Export] public int   VehicleCount  = 8;
    [Export] public float SpawnRadius   = 2.0f;
    [Export] public float VehicleLength = 2.0f;
    [Export] public float VehicleWidth  = 1.2f;
    [Export] public float RideHeight    = 0.25f;
    [Export] public float NormalBlend   = 0.2f;

    // RS params and “go 5m forward then +90° right”
    [Export] public float GoalAdvance      = 5.0f;
    [Export] public float TurnRadiusMeters = 2.0f;
    [Export] public float SampleStepMeters = 0.25f;

    // Cameras
    [Export] public float   MouseSensitivity     = 0.005f;
    [Export] public float TranslateSensitivity = 0.01f;
    [Export] public float ZoomSensitivity = 1.0f;
    [Export] public float   ChaseLerp            = 8.0f;
    [Export] public Vector3 ChaseOffset          = new(0, 2.5f, 5.5f);

    // Debug
    [Export] public bool DebugPathOnTop = true;

    private TerrainDisk _terrain;
    private Node3D _vehiclesRoot;
    private readonly List<VehicleAgent3D> _vehicles = new();
    private readonly List<VehicleBrain> _brains = new();  // Add brains list
    private readonly List<RobotTargetIndicator> _indicators = new();  // Visual indicators
    public WorldState World;  // Add world state
    private RobotCoordinator _coordinator;  // Coordination system
    
    // Path mesh management to prevent memory leaks
    private readonly List<MeshInstance3D> _pathMeshes = new();
    private const int MAX_PATH_MESHES = 30; // Limit displayed paths to prevent crash

    // New visualization systems
    private bool _heatMapEnabled = false;
    private SimCore.Game.PathVisualizer _pathVisualizer;
    private SimCore.Game.TerrainModifier _terrainModifier;
    private SimCore.UI.RobotPayloadUI _payloadUI;
    
    // Sector tracking for visual feedback
    private readonly List<MeshInstance3D> _sectorLines = new();
    private readonly HashSet<int> _completedSectors = new();

    private Camera3D _camTop, _camChase, _camFree, _camOrbit;
    private bool _usingTop = true;
    private bool _movingFreeCam = false, _rotatingFreeCam = false, _rotatingOrbitCam = false;
    private float _freePitch = 0f, _freeYaw = 0f, _orbitPitch = 0, _orbitYaw = 0, Distance = 15.0f;
    private float MinPitchDeg = -5, MaxPitchDeg = 89, MinDist = 0.5f, MaxDist = 18f;

    [Export] public NodePath ObstacleManagerPath;
    private ObstacleManager _obstacleManager;


    public override void _Ready()
    {
        // Nodes
        _vehiclesRoot = GetNode<Node3D>(VehiclesRootPath);
        _camTop   = GetNode<Camera3D>(CameraTopPath);
        _camChase = GetNode<Camera3D>(CameraChasePath);
        _camFree = GetNode<Camera3D>(CameraFreePath);
        _camOrbit = GetNode<Camera3D>(CameraOrbitPath);

        // Terrain (strict)
        _terrain = GetNodeOrNull<TerrainDisk>(TerrainPath);
        if (_terrain == null) { GD.PushError("SimulationDirector: TerrainPath not set to a TerrainDisk."); return; }
        GD.Print($"[Director] Terrain OK: {_terrain.Name}");

        _obstacleManager = GetNodeOrNull<ObstacleManager>(ObstacleManagerPath);
        if (_obstacleManager == null)
        {
            GD.PushError("SimulationDirector: ObstacleManagerPath not set or not found.");
            return;
        }

        // Build global static navigation grid from obstacles BEFORE spawning vehicles
        var obstacleList = _obstacleManager.GetObstacles();
        GridPlannerPersistent.BuildGrid(obstacleList, gridSize: 0.25f, gridExtent: 60, obstacleBufferMeters: 1.0f);

        // Create coordinator for robot collision avoidance
        // REDUCED separation to allow robots to work closer together in their sectors
        _coordinator = new SimCore.Core.RobotCoordinator(minSeparationMeters: 1.5f);
        
        // Initialize visualization systems
        _terrainModifier = new SimCore.Game.TerrainModifier();
        _terrain.AddChild(_terrainModifier);
        
        _pathVisualizer = new SimCore.Game.PathVisualizer();
        AddChild(_pathVisualizer);
        
        _payloadUI = new SimCore.UI.RobotPayloadUI();
        AddChild(_payloadUI);

        // Spawn on ring
        int N = Math.Max(1, VehicleCount);
        for (int i = 0; i < N; i++)
        {
            float theta = i * (Mathf.Tau / N);
            var outward = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)).Normalized();
            var spawnXZ = outward * SpawnRadius;

            // Initial flat pose; PlaceOnTerrain will tilt & raise
            var car = VehicleScene.Instantiate<VehicleAgent3D>();
            _vehiclesRoot.AddChild(car);
            car.SetTerrain(_terrain);
            car.GlobalTransform = new Transform3D(Basis.Identity, spawnXZ);

            // Pose on terrain using FR/FL/RC (same as before)
            PlaceOnTerrain(car, outward);

            car.Wheelbase = VehicleLength;
            car.TrackWidth = VehicleWidth;

            // Create vehicle spec for dig system
            var spec = new VehicleSpec($"Robot_{i+1}", KinematicType.ReedsShepp, VehicleLength, VehicleWidth, RideHeight, TurnRadiusMeters, 2.0f);
            
            // Create planner for Reeds-Shepp paths
            var planner = new HybridReedsSheppPlanner();
            
            // Create brain for dig system with coordinator
            float theta0 = i * (Mathf.Tau / N);
            float theta1 = (i + 1) * (Mathf.Tau / N);
            float digRadius = 10.0f;  // INCREASED sector radius for more area per robot
            var brain = new VehicleBrain(car, spec, planner, World, _terrain, _coordinator, i, theta0, theta1, digRadius, spawnXZ);
            
            // Register callback for sector completion
            brain.SetSectorCompleteCallback(MarkSectorComplete);
            
            _brains.Add(brain);
            _vehicles.Add(car);
            
            // Create robot color for visualization
            float hue = (float)i / N;
            Color robotColor = Color.FromHsv(hue, 0.8f, 0.9f);
            
            // Register with path visualizer
            _pathVisualizer.RegisterRobotPath(i, robotColor);
            
            // Register with payload UI
            _payloadUI.AddRobot(i, spec.Name, robotColor);
            
            // Create visual target indicator
            var indicator = new RobotTargetIndicator();
            indicator.Initialize(robotColor);
            AddChild(indicator);
            _indicators.Add(indicator);

            // Give initial plan
            brain.PlanAndGoOnce();

            GD.Print($"[Director] {car.Name} spawned with dig sector {theta0:F2} to {theta1:F2} rad");

        }

        // Draw sector visualization lines (colored radial lines showing robot assignments)
        DrawSectorLines();

        _camTop.Current = true; _camChase.Current = false; _camFree.Current = false; _camOrbit.Current = false;
    }

    public override void _ExitTree()
    {
        // Clean up all path meshes to prevent memory leaks
        foreach (var mesh in _pathMeshes)
        {
            if (IsInstanceValid(mesh))
                mesh.QueueFree();
        }
        _pathMeshes.Clear();
    }

    // ---------- Input / camera (unchanged) ----------
    public override void _Input(InputEvent e)
    {
        // Heat map toggle with 'H' key
        if (e is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.H)
        {
            if (_terrain != null)
            {
                _terrain.HeatMapEnabled = !_terrain.HeatMapEnabled;
                _payloadUI.UpdateHeatMapStatus(_terrain.HeatMapEnabled);
                GD.Print($"[Director] Heat Map: {(_terrain.HeatMapEnabled ? "ON" : "OFF")}");
            }
        }

        if (e is InputEventMouseMotion mm && _rotatingFreeCam)
        {
            _freeYaw += -mm.Relative.X * MouseSensitivity;
            _freePitch += -mm.Relative.Y * MouseSensitivity;
            _camFree.Rotation = new Vector3(_freePitch, _freeYaw, 0);
        }
        else if (e is InputEventMouseMotion mm2 && _movingFreeCam)
        {
            Vector2 d = mm2.Relative;
            Vector3 right = _camFree.GlobalTransform.Basis.X;
            Vector3 up = _camFree.GlobalTransform.Basis.Y;
            Vector3 motion = (-right * d.X + up * d.Y) * TranslateSensitivity;
            _camFree.GlobalTranslate(motion);
        }
        else if (e is InputEventMouseButton mb && _camFree.Current)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                // Get the forward direction (-Z axis in local space)
                Vector3 forward = -_camFree.GlobalTransform.Basis.Z.Normalized();

                // Calculate new position
                Vector3 newPosition = _camFree.GlobalTransform.Origin + forward * -ZoomSensitivity;

                _camFree.GlobalTransform = new Transform3D(_camFree.GlobalTransform.Basis, newPosition);

            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                // Get the forward direction (-Z axis in local space)
                Vector3 forward = -_camFree.GlobalTransform.Basis.Z.Normalized();

                // Calculate new position
                Vector3 newPosition = _camFree.GlobalTransform.Origin + forward * ZoomSensitivity;

                _camFree.GlobalTransform = new Transform3D(_camFree.GlobalTransform.Basis, newPosition);

            }
        }
        else if (e is InputEventMouseMotion mm3 && _rotatingOrbitCam)
        {
            _orbitYaw -= mm3.Relative.X * MouseSensitivity;
            _orbitPitch -= mm3.Relative.Y * MouseSensitivity;
            _orbitPitch = Mathf.Clamp(_orbitPitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));

            Vector3 targetPosition = _terrain.GlobalTransform.Origin;

            float x = Distance * Mathf.Cos(_orbitPitch) * Mathf.Sin(_orbitYaw);
            float y = Distance * Mathf.Sin(_orbitPitch);
            float z = Distance * Mathf.Cos(_orbitPitch) * Mathf.Cos(_orbitYaw);

            Vector3 camOrbitPos = targetPosition + new Vector3(x, y, z);
            _camOrbit.GlobalTransform = new Transform3D(_camOrbit.Basis, camOrbitPos);
            _camOrbit.LookAt(targetPosition, Vector3.Up);
        }
        else if (e is InputEventMouseButton mb2 && _camOrbit.Current)
        {
            if (mb2.ButtonIndex == MouseButton.WheelUp)
            {
                Distance += ZoomSensitivity;
            }
            else if (mb2.ButtonIndex == MouseButton.WheelDown)
            {
                Distance -= ZoomSensitivity;
            }
            Distance = Mathf.Clamp(Distance, MinDist, MaxDist);

            Vector3 targetPosition = _terrain.GlobalTransform.Origin;

            float x = Distance * Mathf.Cos(_orbitPitch) * Mathf.Sin(_orbitYaw);
            float y = Distance * Mathf.Sin(_orbitPitch);
            float z = Distance * Mathf.Cos(_orbitPitch) * Mathf.Cos(_orbitYaw);

            Vector3 camOrbitPos = targetPosition + new Vector3(x, y, z);
            _camOrbit.GlobalTransform = new Transform3D(_camOrbit.Basis, camOrbitPos);
            _camOrbit.LookAt(targetPosition, Vector3.Up);
        }
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("toggle_camera"))
        {
            _usingTop = !_usingTop;
            _camTop.Current = _usingTop;
            _camChase.Current = !_usingTop;
            _camFree.Current = _camOrbit.Current = false;
            _movingFreeCam = _rotatingFreeCam = _rotatingOrbitCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        if (!_usingTop) FollowChaseCamera(delta);

        if (Input.IsActionJustPressed("select_free_camera"))
        {
            _camFree.Current = true;
            _camTop.Current = _camChase.Current = _camOrbit.Current = false;
        }

        if (Input.IsActionJustPressed("select_orbit_camera"))
        {
            _camOrbit.Current = true;
            _camTop.Current = _camChase.Current = _camFree.Current = false;
        }

        if (Input.IsActionPressed("translate_free_camera") && _camFree.Current)
        {
            _movingFreeCam = true; _rotatingFreeCam = _rotatingOrbitCam = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (Input.IsActionPressed("rotate_camera"))
        {
            _movingFreeCam = false;
            _rotatingFreeCam = _camFree.Current; _rotatingOrbitCam = _camOrbit.Current;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            _movingFreeCam = _rotatingFreeCam = _rotatingOrbitCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Check each robot to see if it finished its path and needs to dig/dump
        for (int i = 0; i < _brains.Count; i++)
        {
            var brain = _brains[i];
            
            // Get the controller from brain using reflection (since it's private)
            var ctrlField = typeof(VehicleBrain).GetField("_ctrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ctrl = ctrlField?.GetValue(brain) as VehicleAgent3D;
            
            if (ctrl != null)
            {
                // Check if robot is idle (path finished)
                var doneField = typeof(VehicleAgent3D).GetField("_done", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (doneField != null && (bool)doneField.GetValue(ctrl))
                {
                    // Robot arrived - process dig/dump and plan next action
                    brain.OnArrival();
                    brain.PlanAndGoOnce();
                }
                
                // Update payload UI with robot status
                float capacity = SimpleDigLogic.ROBOT_CAPACITY;
                float payloadPercent = (brain.Payload / capacity) * 100f;
                _payloadUI.UpdatePayload(i, payloadPercent, brain.Status, brain.CurrentPosition);
                
                // Update path visualization
                var currentPath = brain.GetCurrentPath();
                _pathVisualizer.UpdatePath(i, currentPath);
            }
        }
        
        // Update remaining dirt display (once per frame for efficiency)
        float remainingDirt = _terrain.GetRemainingDirtVolume();
        _payloadUI.UpdateRemainingDirt(remainingDirt);
    }

    private void FollowChaseCamera(double delta)
    {
        if (_vehicles.Count == 0) return;
        var car = _vehicles[0];

        var basis = car.GlobalTransform.Basis;
        var targetPos =
            car.GlobalTransform.Origin +
            basis.X * ChaseOffset.X +
            basis.Y * ChaseOffset.Y +
            basis.Z * ChaseOffset.Z;

        var cur = _camChase.GlobalTransform.Origin;
        var next = cur.Lerp(targetPos, (float)(ChaseLerp * delta));

        _camChase.GlobalTransform = new Transform3D(_camChase.GlobalTransform.Basis, next);
        _camChase.LookAt(car.GlobalTransform.Origin, Vector3.Up);
    }
    // ------------------------------------------------

    // === Placement on terrain using FL/FR/RC (unchanged) =======================
    private void PlaceOnTerrain(VehicleAgent3D car, Vector3 outward)
    {
        var yawBasis = Basis.LookingAt(outward, Vector3.Up);
        float halfL = VehicleLength * 0.5f;
        float halfW = VehicleWidth  * 0.5f;

        Vector3 f = -yawBasis.Z;      // Godot forward is -Z
        Vector3 r =  yawBasis.X;

        Vector3 centerXZ = car.GlobalTransform.Origin; centerXZ.Y = 0;

        Vector3 pFL = centerXZ + f * halfL + r * halfW;
        Vector3 pFR = centerXZ + f * halfL - r * halfW;
        Vector3 pRC = centerXZ - f * halfL;

        _terrain.SampleHeightNormal(centerXZ, out var hC,  out var nC);
        _terrain.SampleHeightNormal(pFL,      out var hFL, out var _);
        _terrain.SampleHeightNormal(pFR,      out var hFR, out var _);
        _terrain.SampleHeightNormal(pRC,      out var hRC, out var _);

        Vector3 n = (hFR - hFL).Cross(hRC - hFL);
        if (n.LengthSquared() < 1e-6f) n = nC;
        n = n.Normalized();

        if (NormalBlend > 0f) n = n.Lerp(nC, Mathf.Clamp(NormalBlend, 0f, 1f)).Normalized();

        Vector3 yawFwd = new Vector3(outward.X, 0, outward.Z).Normalized();
        Vector3 fProj  = (yawFwd - n * yawFwd.Dot(n)); if (fProj.LengthSquared() < 1e-6f) fProj = yawFwd; fProj = fProj.Normalized();

        Vector3 right = n.Cross(fProj).Normalized(); // build RH frame with -Z as forward
        Vector3 zAxis = -fProj;
        var basis = new Basis(right, n, zAxis).Orthonormalized();

        float ride = Mathf.Clamp(RideHeight, 0.02f, 0.12f);
        Vector3 pos = hC + n * ride;

        car.GlobalTransform = new Transform3D(basis, pos);
    }
    // ==========================================================================

    // -------- Path viz projected to terrain (old drawer adapted to terrain) ---
    private float SampleSurfaceY(Vector3 xz)
    {
        if (_terrain != null && _terrain.SampleHeightNormal(xz, out var hit, out var _))
            return hit.Y;
        // fallback simple ray if terrain not present; should not happen in this setup
        var space = GetWorld3D().DirectSpaceState;
        var from = xz + new Vector3(0, 100f, 0);
        var to   = xz + new Vector3(0,-1000f, 0);
        var q = PhysicsRayQueryParameters3D.Create(from, to);
        var hitDict = space.IntersectRay(q);
        if (hitDict.Count > 0) return ((Vector3)hitDict["position"]).Y;
        return 0f;
    }

    private void DrawPathProjectedToTerrain(Vector3[] points, Color col)
    {
        if (points == null || points.Length < 2) return;

        // Clean up old meshes to prevent memory leak
        while (_pathMeshes.Count >= MAX_PATH_MESHES)
        {
            var oldMesh = _pathMeshes[0];
            _pathMeshes.RemoveAt(0);
            if (IsInstanceValid(oldMesh))
                oldMesh.QueueFree();
        }

        var mi = new MeshInstance3D();
        var im = new ImmediateMesh();
        mi.Mesh = im;
        AddChild(mi);
        _pathMeshes.Add(mi); // Track for cleanup

        im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            var y = SampleSurfaceY(p);
            im.SurfaceAddVertex(new Vector3(p.X, y + 0.02f, p.Z));
        }
        im.SurfaceEnd();

        var mat = new StandardMaterial3D { AlbedoColor = col, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        mat.NoDepthTest = DebugPathOnTop;
        if (mi.Mesh != null && mi.Mesh.GetSurfaceCount() > 0)
            mi.SetSurfaceOverrideMaterial(0, mat);
    }

    private void DrawMarkerProjected(Vector3 pos, Color col)
    {
        var m = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 0.01f, RadialSegments = 16 }
        };
        var y = SampleSurfaceY(pos);
        m.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(pos.X, y + 0.01f, pos.Z));

        var mat = new StandardMaterial3D { AlbedoColor = col, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        mat.NoDepthTest = DebugPathOnTop;
        m.SetSurfaceOverrideMaterial(0, mat);
        AddChild(m);
    }

    /// <summary>
    /// Draw colored radial lines from origin to show robot sector assignments
    /// </summary>
    private void DrawSectorLines()
    {
        int N = VehicleCount;
        float digRadius = 10.0f;
        
        // Generate distinct colors for each robot sector
        Color[] colors = new Color[N];
        for (int i = 0; i < N; i++)
        {
            float hue = (float)i / N;
            colors[i] = Color.FromHsv(hue, 0.8f, 0.9f);
        }

        // Draw each sector boundary line
        for (int i = 0; i < N; i++)
        {
            float theta = i * (Mathf.Tau / N);
            var direction = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta));
            var endPoint = direction * digRadius;

            // Create line mesh with thick width for visibility
            var mi = new MeshInstance3D();
            var im = new ImmediateMesh();
            mi.Mesh = im;
            AddChild(mi);
            
            // Store the mesh instance for later updates
            _sectorLines.Add(mi);

            im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            
            // Start at origin (above ground for visibility)
            float y0 = SampleSurfaceY(Vector3.Zero) + 0.5f;
            im.SurfaceAddVertex(new Vector3(0, y0, 0));
            
            // End at sector boundary
            float y1 = SampleSurfaceY(endPoint) + 0.5f;
            im.SurfaceAddVertex(new Vector3(endPoint.X, y1, endPoint.Z));
            
            im.SurfaceEnd();

            // Apply color material - BRIGHTER, thicker, and more visible
            var mat = new StandardMaterial3D 
            { 
                AlbedoColor = colors[i],
                Roughness = 0.0f,
                Metallic = 0.9f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = false
            };
            mat.NoDepthTest = true;
            mat.DisableReceiveShadows = true;
            
            if (mi.Mesh != null && mi.Mesh.GetSurfaceCount() > 0)
                mi.SetSurfaceOverrideMaterial(0, mat);
        }
        
        GD.Print($"[Director] Drew {N} sector boundary lines");
    }
    
    /// <summary>
    /// Mark a sector as complete and change its color to black
    /// </summary>
    public void MarkSectorComplete(int sectorId)
    {
        if (_completedSectors.Contains(sectorId)) return;
        
        _completedSectors.Add(sectorId);
        
        // Change the sector line color to black (completely dark)
        if (sectorId >= 0 && sectorId < _sectorLines.Count)
        {
            var mi = _sectorLines[sectorId];
            if (mi != null && mi.Mesh != null && mi.Mesh.GetSurfaceCount() > 0)
            {
                var completedMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 1),  // Very dark gray/black
                    Roughness = 0.0f,
                    Metallic = 0.0f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
                completedMat.NoDepthTest = true;
                completedMat.DisableReceiveShadows = true;
                mi.SetSurfaceOverrideMaterial(0, completedMat);
                
                GD.Print($"[Director] Sector {sectorId} marked COMPLETE (changed to BLACK)");
            }
        }
    }
}