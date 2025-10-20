using Godot;
using System;
using System.Collections.Generic;
using SimCore.Core;
using SimCore.Services;

public partial class SimulationDirector : Node3D
{
    [Export] public PackedScene VehicleScene;        // assign Vehicle_RS.tscn
    [Export] public NodePath VehiclesRootPath;     // pick the VehiclesRoot node
    [Export] public NodePath CameraTopPath;        // Cameras/TopCam
    [Export] public NodePath CameraChasePath;      // Cameras/ChaseCam
    [Export] public NodePath CameraFreePath;       // Cameras/FreeCam

    // Spawn & motion params
    [Export] public int VehicleCount = 5;
    [Export] public float SpawnRadius = 2.0f;
    [Export] public float VehicleLength = 2.0f;     // bumper to bumper (meters)
    [Export] public float VehicleWidth = 1.2f;     // track width (meters)
    [Export] public float RideHeight = 0.25f;    // chassis clearance above ground
    [Export] public float MaxSlopeDeg = 40f;      // clamp very steep normals
    [Export] public uint GroundMask = 0;
    [Export(PropertyHint.Range, "0,1,0.01")] public float NormalBlend = 0.0f;

    // Planning params
    [Export] public float TurnRadiusMeters = 2.0f;
    [Export] public float SampleStepMeters = 0.25f;

    // Free-cam feel
    [Export] public float MouseSensitivity = 0.005f;
    [Export] public float TranslateSensitivity = 0.01f;

    // Chase-cam feel
    [Export] public float ChaseLerp = 8.0f;
    [Export] public Vector3 ChaseOffset = new(0, 2.5f, 5.5f); // up/back in vehicle local space

    // Terrain
    [Export] public NodePath TerrainPath;

    // Debug
    [Export] public bool ShowGroundSamples;

    private TerrainDisk _terrain;

    private Node3D _vehiclesRoot;
    private readonly List<VehicleAgent3D> _vehicles = new();

    private Camera3D _camTop, _camChase, _camFree;
    private bool _usingTop = true;
    private bool _movingFreeCam = false, _rotatingFreeCam = false;
    private float _pitch = 0f, _yaw = 0f;

    // New architecture bits
    private WorldState _world;
    private IPathPlanner _planner;
    private IScheduler _scheduler;

    public override void _Ready()
    {
        // Nodes
        _vehiclesRoot = GetNode<Node3D>(VehiclesRootPath);
        _camTop = GetNode<Camera3D>(CameraTopPath);
        _camChase = GetNode<Camera3D>(CameraChasePath);
        _camFree = GetNode<Camera3D>(CameraFreePath);
        _terrain = GetNode<TerrainDisk>(TerrainPath);

        // World & services
        _world = new WorldState
        {
            DumpCenter = new Vector3(0, 0, 0)
        };
        // Add a couple of test dig sites (move to Terrain-driven later)
        _world.DigSites.Add(new Vector3(6, 0, 4));
        _world.DigSites.Add(new Vector3(-5, 0, 3));
        _world.DigSites.Add(new Vector3(3, 0, -6));

        _planner = new ReedsSheppPlanner(SampleStepMeters);
        _scheduler = new SimpleScheduler();

        // Spawn vehicles on a ring & plan one task for each
        int N = Math.Max(1, VehicleCount);
        for (int i = 0; i < N; i++)
        {
            float t = i * (Mathf.Tau / N);
            var outward = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)); // unit on XZ
            var pos = outward * SpawnRadius;

            var car = VehicleScene.Instantiate<VehicleAgent3D>();
            _vehiclesRoot.AddChild(car);

            // initial ring pose (flat) just to compute outward; position will be replaced
            car.GlobalTransform = new Transform3D(Basis.Identity, pos);

            // freeze movement for this test
            car.MovementEnabled = false;

            // place it respecting terrain (3-point)
            PlaceOnTerrain(car, outward);

            // collect list for chase cam, etc.
            _vehicles.Add(car);

            // Spec per vehicle (tune as needed)
            var spec = new VehicleSpec(
                Name: $"Car{i}",
                Kin: KinematicType.ReedsShepp,
                Length: 2.0f, Width: 1.2f, Height: 1.0f,
                TurnRadius: TurnRadiusMeters,
                MaxSpeed: (float)car.SpeedMps
            );

            var brain = new VehicleBrain(car, spec, _planner, _scheduler, _world);
            brain.PlanAndGoOnce(); // first plan

            // plan again whenever the path finishes
            car.Connect(nameof(VehicleAgent3D.PathFinished), Callable.From(() => brain.PlanAndGoOnce()));
        }

        // Cameras default
        _camTop.Current = true;
        _camChase.Current = false;
        _camFree.Current = false;
    }

    public override void _Input(InputEvent e)
    {
        // Free-cam rotate
        if (e is InputEventMouseMotion mm && _rotatingFreeCam)
        {
            _yaw += -mm.Relative.X * MouseSensitivity;
            _pitch += -mm.Relative.Y * MouseSensitivity;
            _camFree.Rotation = new Vector3(_pitch, _yaw, 0);
        }
        // Free-cam translate (screen-space pan)
        else if (e is InputEventMouseMotion mm2 && _movingFreeCam)
        {
            Vector2 d = mm2.Relative;
            Vector3 right = _camFree.GlobalTransform.Basis.X;
            Vector3 up = _camFree.GlobalTransform.Basis.Y;
            Vector3 motion = (-right * d.X + up * d.Y) * TranslateSensitivity;
            _camFree.GlobalTranslate(motion);
        }
    }

    public override void _Process(double delta)
    {
        // Top <-> Chase
        if (Input.IsActionJustPressed("toggle_camera"))
        {
            _usingTop = !_usingTop;
            _camTop.Current = _usingTop;
            _camChase.Current = !_usingTop;
            _camFree.Current = false;
            _movingFreeCam = _rotatingFreeCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        if (!_usingTop) FollowChaseCamera(delta);

        // Jump to Free cam
        if (Input.IsActionJustPressed("select_free_camera"))
        {
            _camFree.Current = true;
            _camTop.Current = _camChase.Current = false;
        }

        // Free-cam modes
        if (Input.IsActionPressed("translate_free_camera"))
        {
            _movingFreeCam = true; _rotatingFreeCam = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (Input.IsActionPressed("rotate_free_camera"))
        {
            _movingFreeCam = false; _rotatingFreeCam = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (!_camFree.Current)
        {
            _movingFreeCam = _rotatingFreeCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void FollowChaseCamera(double delta)
    {
        if (_vehicles.Count == 0) return;
        var car = _vehicles[0];

        // Offset defined in vehicle local space: (X right, Y up, Z forward)
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

    ////// Logic for C/CR/FC car sampling /////

    // private void PlaceOnTerrain(VehicleAgent3D car, Vector3 outward)
    // {
    //     if (_terrain == null)
    //     {
    //         GD.PushError("SimulationDirector: TerrainPath not set or TerrainDisk missing.");
    //         return;
    //     }

    //     // yaw frame from the outward ring direction
    //     var yawBasis = Basis.LookingAt(outward, Vector3.Up);
    //     float halfL = VehicleLength * 0.5f;
    //     float halfW = VehicleWidth * 0.5f;

    //     // local forward/right from yaw (remember, Godot forward is -Z)
    //     Vector3 f = -yawBasis.Z;
    //     Vector3 r = yawBasis.X;

    //     // footprint sample points (XZ only)
    //     Vector3 centerXZ = car.GlobalTransform.Origin; centerXZ.Y = 0;
    //     Vector3 pC = centerXZ;
    //     Vector3 pF = centerXZ + f * halfL;  // front center
    //     Vector3 pR = centerXZ + r * halfW;  // right center

    //     // sample the terrain directly (height + normal)
    //     if (!_terrain.SampleHeightNormal(pC, out var hC, out var nC)) return;
    //     _terrain.SampleHeightNormal(pF, out var hF, out var nF);
    //     _terrain.SampleHeightNormal(pR, out var hR, out var nR);

    //     // plane normal from 3 points (handles pitch+roll)
    //     Vector3 n = (hF - hC).Cross(hR - hC);
    //     if (n.LengthSquared() < 1e-6f) n = nC;
    //     n = n.Normalized();

    //     // optional slope clamp
    //     float ang = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(n.Dot(Vector3.Up), -1f, 1f)));
    //     if (ang > MaxSlopeDeg)
    //     {
    //         float t = Mathf.Clamp((ang - MaxSlopeDeg) / Math.Max(1f, ang), 0f, 1f);
    //         n = (n.Lerp(Vector3.Up, t)).Normalized();
    //     }

    //     // preserve yaw, adopt tilt: project yaw-forward onto the tilted plane
    //     Vector3 fYaw = new Vector3(outward.X, 0, outward.Z).Normalized();
    //     Vector3 fProj = (fYaw - n * fYaw.Dot(n));
    //     if (fProj.LengthSquared() < 1e-6f) fProj = fYaw;
    //     fProj = fProj.Normalized();

    //     Vector3 right = fProj.Cross(n).Normalized();
    //     Vector3 fwd = n.Cross(right).Normalized();

    //     var basis = new Basis(right, n, fwd);
    //     Vector3 pos = hC + n * RideHeight;  // sit off the surface by RideHeight

    //     car.GlobalTransform = new Transform3D(basis, pos);

    //     // If you want to visualize:
    //     DebugDrawFootprint(hC, hF, hR, n, new Color(1, 0.6f, 0));
    // }

    ///// Logic for FR/FL/RC car sampling /////
    private void PlaceOnTerrain(VehicleAgent3D car, Vector3 outward)
    {
        if (_terrain == null)
        {
            GD.PushError("SimulationDirector: TerrainPath not set or TerrainDisk missing.");
            return;
        }

        // Yaw frame from the outward ring direction
        var yawBasis = Basis.LookingAt(outward, Vector3.Up);

        // Footprint half-sizes (use your exports, or auto-measure as discussed)
        float halfL = VehicleLength * 0.5f; // along forward
        float halfW = VehicleWidth * 0.5f; // along right

        // Local forward/right from yaw (Godot forward is -Z)
        Vector3 f = -yawBasis.Z;
        Vector3 r = yawBasis.X;

        // Center XZ of the vehicle (world space, Y ignored for sampling)
        Vector3 centerXZ = car.GlobalTransform.Origin;
        centerXZ.Y = 0;

        // Symmetric 3-point triangle: front-left, front-right, rear-center
        Vector3 pFL = centerXZ + f * halfL + r * halfW;
        Vector3 pFR = centerXZ + f * halfL - r * halfW;
        Vector3 pRC = centerXZ - f * halfL;

        // Sample terrain at FL, FR, RC, and also at center (for position & optional blend)
        if (!_terrain.SampleHeightNormal(centerXZ, out var hC, out var nC)) return;
        if (!_terrain.SampleHeightNormal(pFL, out var hFL, out var nFL)) return;
        if (!_terrain.SampleHeightNormal(pFR, out var hFR, out var nFR)) return;
        if (!_terrain.SampleHeightNormal(pRC, out var hRC, out var nRC)) return;

        // Plane normal from the symmetric triangle (robust on crowns)
        Vector3 n = (hFR - hFL).Cross(hRC - hFL);
        if (n.LengthSquared() < 1e-6f) n = nC;  // fallback
        n = n.Normalized();

        // Blend toward the terrain's local normal at center for stability
        if (NormalBlend > 0f)
        {
            float t = Mathf.Clamp(NormalBlend, 0f, 1f);
            n = (n.Lerp(nC, t)).Normalized();
        }

        // Preserve yaw, adopt tilt: project yaw-forward onto plane
        Vector3 yawFwd = new Vector3(outward.X, 0, outward.Z).Normalized();
        Vector3 fProj = (yawFwd - n * yawFwd.Dot(n));
        if (fProj.LengthSquared() < 1e-6f) fProj = yawFwd;
        fProj = fProj.Normalized();

        Vector3 right = fProj.Cross(n).Normalized();
        Vector3 fwd = n.Cross(right).Normalized();
        var basis = new Basis(right, n, fwd);

        // Position at center hit + small ride height along the normal
        float ride = Mathf.Clamp(RideHeight, 0.02f, 0.12f);
        Vector3 pos = hC + n * ride;

        car.GlobalTransform = new Transform3D(basis, pos);

        // (optional) debug to visualize this symmetric triangle
        if (ShowGroundSamples) DebugDrawFootprint(hFL, hFR, hRC, n, new Color(1, 0.6f, 0));
    }

    private void DebugDrawFootprint(Vector3 hC, Vector3 hF, Vector3 hR, Vector3 n, Color col)
    {
        // tiny cylinders at the 3 hit points
        void DropMarker(Vector3 p, Color c)
        {
            var m = new MeshInstance3D();
            m.Mesh = new CylinderMesh { Height = 0.02f, TopRadius = 0.03f, BottomRadius = 0.03f };
            m.GlobalTransform = new Transform3D(Basis.Identity, p + Vector3.Up * 0.01f);

            var mat = new StandardMaterial3D { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            // CylinderMesh already has a surface, so this is safe:
            m.SetSurfaceOverrideMaterial(0, mat);

            AddChild(m);
        }

        DropMarker(hC, new Color(0, 1, 0));
        DropMarker(hF, new Color(0, 0, 1));
        DropMarker(hR, new Color(1, 0, 0));

        // normal line at center
        var mi = new MeshInstance3D();
        var im = new ImmediateMesh();
        AddChild(mi);              // add node first (order doesn’t matter)
        mi.Mesh = im;

        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        im.SurfaceAddVertex(hC);
        im.SurfaceAddVertex(hC + n * 1.0f);
        im.SurfaceEnd();

        // set material AFTER the surface exists
        var mat2 = new StandardMaterial3D { AlbedoColor = col, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        if (mi.Mesh != null && mi.Mesh.GetSurfaceCount() > 0)
            mi.SetSurfaceOverrideMaterial(0, mat2);
    }
}