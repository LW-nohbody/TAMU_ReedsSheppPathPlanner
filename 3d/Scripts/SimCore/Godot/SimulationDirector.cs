using Godot;
using System;
using System.Collections.Generic;
using SimCore.Core;
using SimCore.Services;

public partial class SimulationDirector : Node3D
{
    [Export] public PackedScene VehicleScene;
    [Export] public NodePath VehiclesRootPath;
    [Export] public NodePath CameraTopPath;
    [Export] public NodePath CameraChasePath;
    [Export] public NodePath CameraFreePath;

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
    [Export] public float   TranslateSensitivity = 0.01f;
    [Export] public float   ChaseLerp            = 8.0f;
    [Export] public Vector3 ChaseOffset          = new(0, 2.5f, 5.5f);

    // Debug
    [Export] public bool DebugPathOnTop = true;
    // Test mode: run only one robot/slice for focused debugging
    [Export] public bool SingleRobotMode = false;
    [Export] public int TestRobotIndex = 0;

    // Temporary test flag: when true the first spawned robot will be forced to be full
    // and will immediately plan a dump trip to validate dumping logic and world total updates.
    [Export] public bool ForceFirstRobotDump = false;

    [Export] public float SiteRecomputeInterval = 0.5f; // seconds between re-evaluating site centers
    private float _siteRecomputeTimer = 0f;

    private TerrainDisk _terrain;
    private Node3D _vehiclesRoot;
    private readonly List<VehicleAgent3D> _vehicles = new();
    private readonly List<VehicleBrain> _brains = new();
    private readonly Dictionary<VehicleBrain, DigSite> _robotSites = new();
    private readonly Dictionary<VehicleBrain, Vector3[]> _robotPaths = new();
    private readonly Dictionary<VehicleBrain, Vector3> _robotOrigins = new();
    private readonly Dictionary<VehicleBrain, float> _robotPayloads = new();
    // Make robot capacity editable in the Inspector and apply at runtime
    public static float RobotCapacity = 0.6f; // reduced default capacity to avoid deep pits
    [Export] public float RobotCapacityEdit = 0.6f;

    // Tunable cap for per-dig vertical lowering (m)
    public static float MaxDeltaPerDig = 0.05f;
    [Export] public float MaxDeltaPerDigEdit = 0.05f;

    // Traversability tuning (meters and slope ratio)
    public static float MaxTraversableStep = 0.25f; // max vertical step between path samples
    public static float MaxTraversableSlope = 0.8f; // max slope (dh/dx) allowed along path
    [Export] public float MaxTraversableStepEdit = 0.25f;
    [Export] public float MaxTraversableSlopeEdit = 0.8f;

    // Root node to hold path/marker visuals so we can clear them each frame
    private Node3D _pathVizRoot;

    // Cameras and free-camera state (was accidentally removed in prior edit)
    private Camera3D _camTop, _camChase, _camFree;
    private bool _usingTop = true;
    private bool _movingFreeCam = false, _rotatingFreeCam = false;
    private float _pitch = 0f, _yaw = 0f;

    public WorldState World; // Add reference to WorldState

    // Add this field to store robot labels
    private readonly Dictionary<VehicleBrain, Label3D> _robotLabels = new();
    // Add this field to show world total
    private Label3D _worldTotalLabel;
    // Label to show highest terrain height
    private Label3D _highestLabel;

    // Simple rate-limited problem logger for offline analysis
    public static class ProblemLogger
    {
        private static readonly Dictionary<string, double> _lastLogged = new();
        private static readonly List<string> _entries = new();
        public static double RateSeconds = 5.0; // per-id rate limit
        public static void Log(string id, string msg)
        {
            double now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            if (_lastLogged.TryGetValue(id, out double last) && (now - last) < RateSeconds) return;
            _lastLogged[id] = now;
            string entry = $"{now:F2} [{id}] {msg}";
            _entries.Add(entry);
            GD.Print($"[ProblemLog] {entry}");
        }
        public static string[] GetEntries() => _entries.ToArray();
    }

    public override void _Ready()
    {
        // Nodes
        _vehiclesRoot = GetNode<Node3D>(VehiclesRootPath);
        _camTop   = GetNode<Camera3D>(CameraTopPath);
        _camChase = GetNode<Camera3D>(CameraChasePath);
        _camFree  = GetNode<Camera3D>(CameraFreePath);

        // Create path visualization container so we can clear visuals each frame
        _pathVizRoot = GetNodeOrNull<Node3D>("PathViz");
        if (_pathVizRoot == null) { _pathVizRoot = new Node3D { Name = "PathViz" }; AddChild(_pathVizRoot); }

        // Terrain (strict)
        _terrain = GetNodeOrNull<TerrainDisk>(TerrainPath);
        if (_terrain == null) { GD.PushError("SimulationDirector: TerrainPath not set to a TerrainDisk."); return; }
        GD.Print($"[Director] Terrain OK: {_terrain.Name}");

        World = new WorldState();
        World.DumpCenter = new Vector3(0, 0, 0); // Example dump location
        int N = Math.Max(1, VehicleCount); // Declare N only once
        float digRadius = 7.0f; // Example dig area radius
        var usedHighPoints = new List<Vector3>();
        for (int i = 0; i < N; i++)
        {
            float theta0 = i * (Mathf.Tau / N);
            float theta1 = (i + 1) * (Mathf.Tau / N);
            Vector3 best = Vector3.Zero;
            float bestY = float.MinValue;
            for (int s = 0; s < 20; s++)
            {
                float t = (float)s / 19f;
                float theta = Mathf.Lerp(theta0, theta1, t);
                Vector3 pt = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * digRadius;
                float y = SampleSurfaceY(pt);
                if (y > bestY) { bestY = y; best = pt; }
            }
            // Check for overlap with previous high points
            bool tooClose = false;
            foreach (var prev in usedHighPoints)
            {
                if (best.DistanceTo(prev) < 1.5f) { tooClose = true; break; }
            }
            if (tooClose)
            {
                // Find next best non-overlapping point in section
                float nextBestY = float.MinValue;
                Vector3 nextBest = Vector3.Zero;
                for (int s = 0; s < 20; s++)
                {
                    float t = (float)s / 19f;
                    float theta = Mathf.Lerp(theta0, theta1, t);
                    Vector3 pt = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * digRadius;
                    float y = SampleSurfaceY(pt);
                    bool overlap = false;
                    foreach (var prev in usedHighPoints)
                        if (pt.DistanceTo(prev) < 1.5f) { overlap = true; break; }
                    if (!overlap && y > nextBestY) { nextBestY = y; nextBest = pt; }
                }
                best = nextBest;
            }
            usedHighPoints.Add(best);
            // Smaller initial dig sites: less volume, smaller tool radius and shallower depth
            World.DigSites.Add(new DigSite(best, 2.0f, 0.4f, 0.2f));
        }
        var assignedSites = new HashSet<DigSite>();
        for (int i = 0; i < N; i++)
        {
            float theta = i * (Mathf.Tau / N);
            var outward = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)).Normalized();
            var spawnXZ = outward * SpawnRadius;
            var car = VehicleScene.Instantiate<VehicleAgent3D>();
            car.SpeedMps = 2.5f; // Increase speed
            _vehiclesRoot.AddChild(car);
            car.SetTerrain(_terrain);
            car.GlobalTransform = new Transform3D(Basis.Identity, spawnXZ);
            PlaceOnTerrain(car, outward);
            // Allow vehicle to move freely (do NOT call SetSliceLimits) so it can travel outside its slice
            float theta0_local = i * (Mathf.Tau / N);
            float theta1_local = (i + 1) * (Mathf.Tau / N);
              car.Wheelbase  = VehicleLength;
              car.TrackWidth = VehicleWidth;
              // Assign dig site for this section
              DigSite assignedSite = World.DigSites[i];
              assignedSites.Add(assignedSite);
              var spec = new VehicleSpec($"Robot_{i+1}", KinematicType.ReedsShepp, VehicleLength, VehicleWidth, RideHeight, TurnRadiusMeters, 2.0f);
             // Use default planner for Reeds-Shepp paths
             var planner = new RSPathPlannerStub();
             // Create brain with simple dig logic
            var brain = new VehicleBrain(car, spec, planner, World, _terrain, theta0_local, theta1_local, digRadius, spawnXZ);
             _brains.Add(brain);
             _robotOrigins[brain] = spawnXZ;
             GD.Print($"[Director] {car.Name} assigned to dig site {assignedSite.Center}");

            // Ensure each brain gets an initial plan immediately so robots start moving
            // and the scheduler/brain state is seeded. When SingleRobotMode is enabled
            // only the selected TestRobotIndex will be planned initially to limit activity.
            if (!SingleRobotMode || i == Mathf.Clamp(TestRobotIndex, 0, N - 1))
                brain.PlanAndGoOnce();

            // Temporary test: optionally force the first robot to be full and plan a dump
            if (ForceFirstRobotDump && i == 0) {
                try {
                    var payloadField = typeof(VehicleBrain).GetField("_payload", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var payloadFullField = typeof(VehicleBrain).GetField("_payloadFull", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (payloadField != null && payloadFullField != null) {
                        payloadField.SetValue(brain, RobotCapacity);
                        payloadFullField.SetValue(brain, true);
                        GD.Print($"[DirectorTest] Forced Robot_{i+1} payload to {RobotCapacity:F3} and payloadFull=true for dump test.");
                        // Re-plan so the brain sees payloadFull and schedules a dump/transit
                        brain.PlanAndGoOnce();
                    }
                } catch (Exception ex) {
                    GD.PrintErr($"[DirectorTest] Failed to force dump state: {ex.Message}");
                }
            }
        }

        _camTop.Current = true; _camChase.Current = false; _camFree.Current = false;
        // Apply configured capacity
        RobotCapacity = Mathf.Max(0.01f, RobotCapacityEdit);
        // Apply exported tuning for per-dig lowering and traversability
        MaxDeltaPerDig = MathF.Max(0f, MaxDeltaPerDigEdit);
        MaxTraversableStep = MathF.Max(0f, MaxTraversableStepEdit);
        MaxTraversableSlope = MathF.Max(0f, MaxTraversableSlopeEdit);
         // create world total label
         _worldTotalLabel = new Label3D();
        _worldTotalLabel.Modulate = new Color(1f, 1f, 0.6f);
        _worldTotalLabel.FontSize = 28;
        _worldTotalLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        AddChild(_worldTotalLabel);

        // Label for highest terrain height (persistent field)
        _highestLabel = new Label3D();
        _highestLabel.Modulate = new Color(0.8f, 0.8f, 1f);
        _highestLabel.FontSize = 20;
        _highestLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _highestLabel.Name = "HighestLabel3D";
        AddChild(_highestLabel);
        _highestLabel.Text = "Highest: ?";
    }

    // ---------- Input / camera (unchanged) ----------
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseMotion mm && _rotatingFreeCam)
        {
            _yaw   += -mm.Relative.X * MouseSensitivity;
            _pitch += -mm.Relative.Y * MouseSensitivity;
            _camFree.Rotation = new Vector3(_pitch, _yaw, 0);
        }
        else if (e is InputEventMouseMotion mm2 && _movingFreeCam)
        {
            Vector2 d = mm2.Relative;
            Vector3 right = _camFree.GlobalTransform.Basis.X;
            Vector3 up    = _camFree.GlobalTransform.Basis.Y;
            Vector3 motion = (-right * d.X + up * d.Y) * TranslateSensitivity;
            _camFree.GlobalTranslate(motion);
        }
    }

    public override void _Process(double delta)
    {
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

        if (Input.IsActionJustPressed("select_free_camera"))
        {
            _camFree.Current = true;
            _camTop.Current = _camChase.Current = false;
        }

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
        else
        {
            _movingFreeCam = _rotatingFreeCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        // Clear previous frame path visuals so they don't accumulate
        ClearPathVisuals();

        // Real-time dig logic: update each robot (or only selected robot in SingleRobotMode)
        List<VehicleBrain> brainsToProcess;
        if (SingleRobotMode && _brains.Count > 0)
        {
            int idx = Mathf.Clamp(TestRobotIndex, 0, _brains.Count - 1);
            brainsToProcess = new List<VehicleBrain> { _brains[idx] };
        }
        else
        {
            brainsToProcess = _brains;
        }

        foreach (var brain in brainsToProcess)
        {
            // Inspect controller and done flag for debugging
            var ctrlFieldDbg = typeof(VehicleBrain).GetField("_ctrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ctrlDbg = ctrlFieldDbg?.GetValue(brain) as VehicleAgent3D;
            bool done = false;
            string rname = "<unknown>";
            if (ctrlDbg != null)
            {
                rname = ctrlDbg.Name;
                var doneFieldDbg = typeof(VehicleAgent3D).GetField("_done", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (doneFieldDbg != null) done = (bool)doneFieldDbg.GetValue(ctrlDbg);
            }

            // Debug summary each loop iteration for this robot
            // Verbose per-robot status printing commented out to reduce log spam.
            // GD.Print($"[DirectorDbg] Robot {rname} idle={done}, world_sites={World.DigSites.Count}, world_total={World.TotalDirtExtracted:F3}");

            // Only call PlanAndGoOnce if robot is idle (path finished)
            if (IsRobotIdle(brain)) {
                // Print pre-arrival payload for context
                GD.Print($"[DirectorDbg] Robot {rname} reporting arrival. prePayload={brain.GetPayload():F3}");
                // notify brain of arrival (may modify payload and world total)
                brain.OnArrival();
                // Immediately reflect post-arrival state in logs and UI
                GD.Print($"[DirectorDbg] Robot {rname} postArrival payload={brain.GetPayload():F3}, world_total={World.TotalDirtExtracted:F3}");
                if (_worldTotalLabel != null) _worldTotalLabel.Text = $"Total Dirt Removed: {World.TotalDirtExtracted:F2}";
                // after arrival handling, plan and send a new path
                brain.PlanAndGoOnce();
            }

            // Store the planned path for visualization
            var ctrlField = typeof(VehicleBrain).GetField("_ctrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ctrl = ctrlField?.GetValue(brain) as VehicleAgent3D;
            if (ctrl != null)
            {
                var pathField = typeof(VehicleAgent3D).GetField("_path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pathField != null)
                {
                    var path = pathField.GetValue(ctrl) as Vector3[];
                    if (path != null && path.Length > 1)
                        _robotPaths[brain] = path;
                }
            }
        }

        // Draw dig site gizmos (markers disabled to reduce visual clutter)
        // foreach (var site in World.DigSites)
        // {
        //     float scale = Mathf.Clamp(site.RemainingVolume, 0.1f, 2.0f); // scale by volume
        //     var sphere = new MeshInstance3D {
        //         Mesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.2f * scale },
        //     };
        //     var y = SampleSurfaceY(site.Center);
        //     sphere.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(site.Center.X, y + 0.2f * scale, site.Center.Z));
        //     var mat = new StandardMaterial3D { AlbedoColor = new Color(1, 0.7f, 0.2f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        //     mat.NoDepthTest = true;
        //     sphere.SetSurfaceOverrideMaterial(0, mat);
        //     AddChild(sphere);
        // }
        // Draw dig slices as colored sectors
        int N = Math.Max(1, VehicleCount);
        float digRadius = 7.0f;
        for (int i = 0; i < N; i++)
        {
            Color col = Color.FromHsv(i / (float)N, 0.7f, 0.8f);
            float theta0 = i * (Mathf.Tau / N);
            float theta1 = (i + 1) * (Mathf.Tau / N);
            var pts = new List<Vector3>();
            pts.Add(Vector3.Zero);
            for (int s = 0; s <= 20; s++)
            {
                float t = (float)s / 20f;
                float theta = Mathf.Lerp(theta0, theta1, t);
                pts.Add(new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * digRadius);
            }
            DrawPathProjectedToTerrain(pts.ToArray(), col);
        }
        // Draw robot paths to dig sites
        foreach (var kvp in _robotPaths)
        {
            DrawPathProjectedToTerrain(kvp.Value, new Color(0.9f, 0.2f, 0.7f));
        }
        // Clear stored paths after drawing so they don't persist next frame
        _robotPaths.Clear();

        // keep labels persistent; create/update below
         // Draw robot capacity label above each robot
         foreach (var brain in _brains)
         {
             var ctrlField = typeof(VehicleBrain).GetField("_ctrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
             var ctrl = ctrlField?.GetValue(brain) as VehicleAgent3D;
             if (ctrl != null)
             {
                 var pos = ctrl.GlobalTransform.Origin;
                 float y = pos.Y + 1.6f;
                 if (!_robotLabels.ContainsKey(brain)) {
                     var label = new Label3D();
                     label.Modulate = new Color(0.1f, 0.9f, 0.3f);
                     label.FontSize = 20;
                     label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                     AddChild(label);
                     _robotLabels[brain] = label;
                 }
                 var lbl = _robotLabels[brain];
                 float payload = brain.GetPayload();
                 lbl.Text = $"{payload:F2} / {RobotCapacity:F2} ({Mathf.Clamp(payload/RobotCapacity*100f,0f,100f):F0}%)";
                 lbl.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(pos.X, y, pos.Z));
             }
         }
         // update world label
         if (_worldTotalLabel != null) {
             _worldTotalLabel.Text = $"Total Dirt Removed: {World.TotalDirtExtracted:F2}";
             _worldTotalLabel.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(0, 5f, 0));
         }
        // update highest terrain label
        if (_highestLabel != null && _terrain != null) {
            float maxh = _terrain.GetMaxLocalHeight();
            _highestLabel.Text = $"Highest terrain (local): {maxh:F3}";
            _highestLabel.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(-6f, 5f, 0f));
        }

        // Recompute dig-site centers periodically so robots retarget current highs
        _siteRecomputeTimer += (float)delta;
        if (_siteRecomputeTimer >= SiteRecomputeInterval) {
            _siteRecomputeTimer = 0f;
            RecomputeSiteCenters();
        }
    }

    // Recompute the center of each dig site by finding the highest sample in its angular slice.
    private void RecomputeSiteCenters()
    {
        if (_terrain == null) return;
        int N = Math.Max(1, VehicleCount);
        float digRadius = 7.0f;
        int count = Math.Min(N, World.DigSites.Count);
        for (int i = 0; i < count; i++)
        {
            var site = World.DigSites[i];
            float theta0 = i * (Mathf.Tau / N);
            float theta1 = (i + 1) * (Mathf.Tau / N);
            // Search the full sector (radial + angular) for the highest sampled point
            Vector3 best = site.Center;
            float bestY = SampleSurfaceY(best);
            int angSamples = 24;
            int radSamples = 8;
            for (int a = 0; a < angSamples; a++)
            {
                float ta = (float)a / (angSamples - 1);
                float theta = Mathf.Lerp(theta0, theta1, ta);
                for (int r = 0; r < radSamples; r++)
                {
                    float tr = (float)(r + 1) / radSamples; // avoid exact center for r=0
                    Vector3 pt = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * (digRadius * tr);
                    float y = SampleSurfaceY(pt);
                    if (y > bestY)
                    {
                        bestY = y;
                        best = pt;
                    }
                }
            }
             if (best != site.Center)
             {
                 var updated = site with { Center = best };
                 World.DigSites[i] = updated;
                GD.Print($"[Director] Recomputed site[{i}] center -> {best} (y={bestY:F3})");
             }
         }
     }

    // Helper to check if robot is idle (path finished)
    private bool IsRobotIdle(VehicleBrain brain)
    {
        // VehicleAgent3D exposes _done flag, so check it
        var ctrlField = typeof(VehicleBrain).GetField("_ctrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ctrl = ctrlField?.GetValue(brain) as VehicleAgent3D;
        if (ctrl == null) return false;
        var doneField = typeof(VehicleAgent3D).GetField("_done", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (doneField == null) return false;
        return (bool)doneField.GetValue(ctrl);
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

        var mi = new MeshInstance3D();
        var im = new ImmediateMesh();
        mi.Mesh = im;
        if (_pathVizRoot != null) _pathVizRoot.AddChild(mi); else AddChild(mi);

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

    // Clear and free previous frame path visuals
    private void ClearPathVisuals()
    {
        if (_pathVizRoot == null) return;
        for (int i = _pathVizRoot.GetChildCount() - 1; i >= 0; --i)
        {
            var c = _pathVizRoot.GetChild(i) as Node;
            if (c != null) c.QueueFree();
        }
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
        if (_pathVizRoot != null) _pathVizRoot.AddChild(m); else AddChild(m);
    }

    // Add stub path planner class
    private class RSPathPlannerStub : IPathPlanner {
        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world) {
            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos  = new Vector3((float)goal.X, 0, (float)goal.Z);
            GD.Print($"[RSPathPlannerStub] startPos={startPos} yaw={start.Yaw:F2} goalPos={goalPos} yaw={goal.Yaw:F2}");
            var (pts, gears) = RSAdapter.ComputePath3D(startPos, start.Yaw, goalPos, goal.Yaw, spec.TurnRadius, 0.25);
            GD.Print($"[RSPathPlannerStub] path pts={pts.Length} gears={gears.Length}");
            var path = new PlannedPath();
            path.Points.AddRange(pts);
            path.Gears.AddRange(gears);
            return path;
        }
    }

    // Planner that clamps goal to an angular sector (slice) before planning
    private class SectorAwarePlanner : IPathPlanner {
        private readonly float _thetaMin, _thetaMax, _maxRadius;
        public SectorAwarePlanner(float thetaMin, float thetaMax, float maxRadius) { _thetaMin = thetaMin; _thetaMax = thetaMax; _maxRadius = maxRadius; }
        private static float NormalizeAngle(float a) { a %= Mathf.Tau; if (a < 0) a += Mathf.Tau; return a; }
        private static float AngularDistance(float a, float b) { float d = a - b; while (d > Mathf.Pi) d -= Mathf.Tau; while (d < -Mathf.Pi) d += Mathf.Tau; return d; }
        private static bool AngleBetween(float t, float a, float b) { if (a <= b) return t >= a && t <= b; return t >= a || t <= b; }

        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world) {
            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos  = new Vector3((float)goal.X, 0, (float)goal.Z);

            // clamp goalPos into sector
            float ang = Mathf.Atan2(goalPos.Z, goalPos.X); if (ang < 0) ang += Mathf.Tau;
            float r = goalPos.Length();
            float tmin = NormalizeAngle(_thetaMin); float tmax = NormalizeAngle(_thetaMax);
            if (!AngleBetween(ang, tmin, tmax)) {
                float dMin = AngularDistance(ang, tmin);
                float dMax = AngularDistance(ang, tmax);
                float newAng = (MathF.Abs(dMin) < MathF.Abs(dMax)) ? tmin : tmax;
                goalPos = new Vector3(MathF.Cos(newAng) * r, 0, MathF.Sin(newAng) * r);
            } else {
                goalPos = goalPos.Normalized() * r;
            }

            GD.Print($"[SectorPlanner] start={startPos} goalClamped={goalPos} (origGoal={(float)goal.X,0:F3},{(float)goal.Z,0:F3})");
            var (pts, gears) = RSAdapter.ComputePath3D(startPos, start.Yaw, goalPos, goal.Yaw, spec.TurnRadius, 0.25);

            // Post-process path points: clamp every waypoint into the sector so driver never receives points outside slice
            for (int i = 0; i < pts.Length; ++i)
            {
                var p = pts[i];
                float pang = Mathf.Atan2(p.Z, p.X); if (pang < 0) pang += Mathf.Tau;
                float pr = new Vector2(p.X, p.Z).Length();
                // clamp radius
                pr = MathF.Min(pr, _maxRadius);
                if (!AngleBetween(pang, tmin, tmax))
                {
                    float dMin = AngularDistance(pang, tmin);
                    float dMax = AngularDistance(pang, tmax);
                    float chosen = (MathF.Abs(dMin) < MathF.Abs(dMax)) ? tmin : tmax;
                    pts[i] = new Vector3(MathF.Cos(chosen) * pr, p.Y, MathF.Sin(chosen) * pr);
                }
                else
                {
                    // inside angle, enforce clamped radius
                    var norm = new Vector2(p.X, p.Z).Normalized();
                    pts[i] = new Vector3(norm.X * pr, p.Y, norm.Y * pr);
                }
            }

            var path = new PlannedPath(); path.Points.AddRange(pts); path.Gears.AddRange(gears);
            return path;
        }
    }

    // Scheduler that always returns the assigned dig site
    private class FixedSiteScheduler : IScheduler {
        private readonly DigSite _site;
        public FixedSiteScheduler(DigSite site) { _site = site; }
        public ITask NextTask(VehicleSpec _, WorldState world, bool payloadFull)
        {
            // Resolve the current site instance from world by selecting the nearest site to the original center
            DigSite worldSite = null;
            float bestD = float.MaxValue;
            foreach (var s in world.DigSites) {
                float d = s.Center.DistanceTo(_site.Center);
                if (d < bestD) { bestD = d; worldSite = s; }
            }
             if (payloadFull) return new DumpTask(world.DumpCenter);
            if (worldSite == null || worldSite.RemainingVolume <= 0) {
                GD.Print($"[SchedDbg-Fixed] site not found or depleted for center={_site.Center}");
                return new IdleTask();
            }
            return new DigTask(worldSite, worldSite.ToolRadius, worldSite.Depth);
        }
    }

    // Scheduler with dirt capacity logic
    private class CapacityScheduler : IScheduler {
        private readonly DigSite _site; // assigned slice (original sample)
        private readonly Vector3 _origin;
        private readonly float _thetaMin, _thetaMax;
        private bool _returningToDump = false;
        public CapacityScheduler(DigSite site, Vector3 origin, float thetaMin, float thetaMax) { _site = site; _origin = origin; _thetaMin = thetaMin; _thetaMax = thetaMax; }
        private static float NormalizeAngle(float a) { a %= Mathf.Tau; if (a < 0) a += Mathf.Tau; return a; }
        private static bool AngleBetween(float t, float a, float b) { if (a <= b) return t >= a && t <= b; return t >= a || t <= b; }
        public ITask NextTask(VehicleSpec spec, WorldState world, bool payloadFull)
        {
            // Find nearest site that lies within this scheduler's angular slice
            DigSite worldSite = null;
            float bestD = float.MaxValue;
            float tmin = NormalizeAngle(_thetaMin);
            float tmax = NormalizeAngle(_thetaMax);
            foreach (var s in world.DigSites) {
                float ang = Mathf.Atan2(s.Center.Z, s.Center.X); if (ang < 0) ang += Mathf.Tau;
                if (!AngleBetween(ang, tmin, tmax)) continue; // skip sites outside slice
                float d = s.Center.DistanceTo(_origin);
                if (d < bestD) { bestD = d; worldSite = s; }
            }
            if (worldSite == null) {
                GD.Print($"[SchedDbg] no site in slice for origin={_origin} -> Idle");
                return new IdleTask();
            }

            GD.Print($"[SchedDbg] CapacityScheduler for site={worldSite.Center} remaining={worldSite.RemainingVolume:F3} payloadFull={payloadFull}");
            if (worldSite.RemainingVolume <= 0) {
                GD.Print($"[SchedDbg] returning Idle (site depleted)");
                return new IdleTask(); // don't reassign, just idle
            }
            if (_returningToDump) {
                _returningToDump = false;
                GD.Print($"[SchedDbg] returning DumpTask to origin {_origin}");
                return new DumpTask(_origin);
            }
            if (payloadFull) {
                _returningToDump = true;
                GD.Print($"[SchedDbg] payloadFull => returning TransitTask to origin {_origin}");
                return new TransitTask(new Pose(_origin.X, _origin.Z, 0));
            }
            GD.Print($"[SchedDbg] returning DigTask(site={worldSite.Center} toolR={worldSite.ToolRadius:F3} depth={worldSite.Depth:F3})");
            return new DigTask(worldSite, worldSite.ToolRadius, worldSite.Depth);
         }
     }
}