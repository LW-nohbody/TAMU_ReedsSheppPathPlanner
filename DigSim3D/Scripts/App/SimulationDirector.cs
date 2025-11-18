using Godot;
using System;
using System.Collections.Generic;
using System.Runtime;
using DigSim3D.UI;

using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    public partial class SimulationDirector : Node3D
    {
        [Export] public PackedScene VehicleScene = null!;
        [Export] public NodePath VehiclesRootPath = null!;
        [Export] public NodePath CameraTopPath = null!;
        [Export] public NodePath CameraChasePath = null!;
        [Export] public NodePath CameraFreePath = null!;
        [Export] public NodePath CameraOrbitPath = null!;

        [Export] public NodePath TerrainPath = null!;

        // Spawn / geometry
        [Export] public int VehicleCount = 8;
        [Export] public float SpawnRadius = 2.0f;
        [Export] public float VehicleLength = 2.0f;
        [Export] public float VehicleWidth = 1.2f;
        [Export] public float RideHeight = 0.25f;
        [Export] public float NormalBlend = 0.2f;

        // RS params and “go 5m forward then +90° right”
        [Export] public float GoalAdvance = 5.0f;
        [Export] public float TurnRadiusMeters = 2.0f;
        [Export] public float SampleStepMeters = 0.25f;

        // Cameras
        [Export] public float MouseSensitivity = 0.005f;
        [Export] public float TranslateSensitivity = 0.01f;
        [Export] public float ZoomSensitivity = 1.0f;
        [Export] public float ChaseLerp = 8.0f;
        [Export] public Vector3 ChaseOffset = new(0, 2.5f, 5.5f);
        [Export] public float FreeMoveSpeed = 12.0f;   // world-relative m/s
        [Export] public float FreeMoveSpeedFast = 18.0f;

        // Debug
        [Export] public bool DebugPathOnTop = true;

        private TerrainDisk _terrain = null!;
        private Node3D _vehiclesRoot = null!;
        private readonly List<VehicleVisualizer> _vehicles = new();

        private Camera3D _camTop = null!, _camChase = null!, _camFree = null!, _camOrbit = null!;
        private enum CameraMode { Free, Orbit, TopDown, VehicleFollow }
        private CameraMode _mode = CameraMode.TopDown;
        private int _followIndex = 0;

        private bool _usingTop = true;
        private bool _movingFreeCam = false, _rotatingFreeCam = false, _rotatingOrbitCam = false;
        private float _freePitch = 0f, _freeYaw = 0f, _orbitPitch = 0, _orbitYaw = 0, Distance = 15.0f;
        private float MinPitchDeg = -5, MaxPitchDeg = 89, MinDist = 0.5f, MaxDist = 18f;
        private float lift = 0.04f;

        [Export] public NodePath ObstacleManagerPath = null!;
        private ObstacleManager _obstacleManager = null!;

        // === Dig System ===
        private DigService _digService = null!;
        private DigConfig _digConfig = DigConfig.Default;
        private DigVisualizer _digVisualizer = null!;
        private SectorVisualizer _sectorVisualizer = null!;
        private BufferVisualizer _bufferVisualizer = null!;
        private List<VehicleBrain> _robotBrains = new();
        private float _initialTerrainVolume = 0f;  // Store initial volume at startup

        private bool _simPaused = false;

        // === UI ===
        private DigSim3D.UI.DigSimUI _digSimUI = null!;
        // private SimpleTestUI _testUI = null!;

        public override void _Ready()
        {
            // Nodes
            _vehiclesRoot = GetNode<Node3D>(VehiclesRootPath);
            _camTop = GetNode<Camera3D>(CameraTopPath);
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
            GridPlannerPersistent.BuildGrid(obstacleList, gridSize: 0.25f, gridExtent: 60, obstacleBufferMeters: 0.5f);  // 0.5m obstacle buffer

            // Spawn on ring
            int N = Math.Max(1, VehicleCount);
            for (int i = 0; i < N; i++)
            {
                float theta = i * (Mathf.Tau / N);
                var outward = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)).Normalized();
                var spawnXZ = outward * SpawnRadius;

                var car = VehicleScene.Instantiate<VehicleVisualizer>();
                _vehiclesRoot.AddChild(car);
                car.SetTerrain(_terrain);
                car.GlobalTransform = new Transform3D(Basis.Identity, spawnXZ);

                PlaceOnTerrain(car, outward);

                car.Wheelbase = VehicleLength;
                car.TrackWidth = VehicleWidth;
                car._vehicleID = $"RS-{(i + 1):00}";

                _vehicles.Add(car);

                // Add nameplate
                var id = car._vehicleID;
                var nameplate = new Nameplate3D
                {
                    Text = id,
                    HeightOffset = 0.5f,
                    FontColor = Colors.White,
                    YBillboardOnly = false,
                    FixedSize = true,
                    FontSize = 64,
                    PixelSize = 0.0005f,
                };
                car.AddChild(nameplate);
            }

            // === PLAN FIRST DIG TARGETS (after all cars exist) ===
            var scheduler = new RadialScheduler();
            _robotBrains = new List<VehicleBrain>(_vehicles.Count);
            foreach (var v in _vehicles)
            {
                var brain = new VehicleBrain();
                v.AddChild(brain);
                _robotBrains.Add(brain);
            }

            // === Initialize Dig Service ===
            _digService = new DigService(_terrain, _digConfig);

            // Create dig visualizer
            _digVisualizer = new DigVisualizer { Name = "DigVisualizer" };
            AddChild(_digVisualizer);

            // Create sector visualizer to show robot sectors
            _sectorVisualizer = new SectorVisualizer { Name = "SectorVisualizer" };
            AddChild(_sectorVisualizer);
            float arenaRadius = _terrain.GridResolution * _terrain.GridStep / 2f;
            _sectorVisualizer.Initialize(_vehicles.Count, arenaRadius);
            GD.Print($"[Director] SectorVisualizer created with {_vehicles.Count} sectors, radius {arenaRadius:F1}m");

            // Create buffer visualizer to show obstacle and wall buffer zones
            _bufferVisualizer = new BufferVisualizer { Name = "BufferVisualizer" };
            AddChild(_bufferVisualizer);
            const float obstacleBufferMeters = 0.5f; // 0.5m obstacle buffer (both dig target selection and path planning)
            const float wallBufferMeters = 0.1f;     // 0.1m wall buffer (dig target selection only)
            _bufferVisualizer.Initialize(obstacleList, obstacleBufferMeters, _terrain.Radius, wallBufferMeters);
            GD.Print($"[Director] BufferVisualizer created - obstacle buffer: {obstacleBufferMeters}m, wall buffer: {wallBufferMeters}m");

            // Create world state for path planning
            var worldState = new WorldState
            {
                Obstacles = obstacleList,
                Terrain = _terrain
            };

            // Create path planner (will be reused for all robots)
            var hybridPlanner = new HybridReedsSheppPlanner();

            // Initialize brain dig system with path drawing callback
            foreach (var brain in _robotBrains)
            {
                int robotIndex = _robotBrains.IndexOf(brain);
                int totalRobots = _robotBrains.Count;
                GD.Print($"🎮 [Director] Initializing brain {robotIndex} with DigConfig.DigDepth={_digConfig.DigDepth:F2}m (config hash: {_digConfig.GetHashCode()})");
                brain.InitializeDigBrain(_digService, _terrain, scheduler, _digConfig, hybridPlanner, worldState, _digVisualizer, DrawPathProjectedToTerrain, robotIndex, totalRobots);
            }

            var digTargets = scheduler.PlanFirstDigTargets(
                _robotBrains, _terrain, Vector3.Zero, DigScoring.Default,
                keepoutR: 2.0f, randomizeOrder: true,
                obstacles: obstacleList, inflation: 0.5f);  // Pass obstacles for manual checking

            // === Build paths to the assigned dig targets (scheduler-driven) ===
            for (int k = 0; k < _vehicles.Count; k++)
            {
                var car = _vehicles[k];
                var brain = _robotBrains[k];
                var (digPos, approachYaw) = digTargets[k];

                // Set dig target on brain
                brain.SetDigTarget(digPos, approachYaw);

                // current forward yaw in XZ (Godot forward is -Z)
                var fwd = -car.GlobalTransform.Basis.Z;
                double startYaw = MathF.Atan2(fwd.Z, fwd.X);
                var start = car.GlobalTransform.Origin;

                // Planner poses use X/Z + yaw
                var startPose = new Pose(start.X, start.Z, startYaw);
                var goalPose = new Pose(digPos.X, digPos.Z, approachYaw);

                // Vehicle spec
                VehicleSpec spec = car.Spec;

                PlannedPath path = hybridPlanner.Plan(startPose, goalPose, spec, worldState);

                DrawPathProjectedToTerrain(path.Points.ToArray(), new Color(0.15f, 0.9f, 1.0f));
                DrawMarkerProjected(start, new Color(0, 1, 0));
                DrawMarkerProjected(digPos, new Color(0, 0, 1));

                car.SetPath(path.Points.ToArray(), path.Gears.ToArray());

                GD.Print($"[Director] {car.Name} path: {path.Points.Count} samples");
            }

            // === Initialize UI ===
            // Add UI Control to a CanvasLayer to ensure it's drawn on top
            var uiLayer = new CanvasLayer { Layer = 100 };
            AddChild(uiLayer);
            GD.Print($"[Director] Created CanvasLayer for UI");

            _digSimUI = new DigSim3D.UI.DigSimUI();
            uiLayer.AddChild(_digSimUI);
            GD.Print($"[Director] Added DigSimUI to CanvasLayer");

            // Add robots to UI
            for (int i = 0; i < _robotBrains.Count; i++)
            {
                var car = _vehicles[i];
                _digSimUI.AddRobot(i, car._vehicleID, new Color((float)i / _robotBrains.Count, 0.6f, 1.0f));
            }

            // Calculate and store initial terrain volume
            _initialTerrainVolume = CalculateTerrainVolume();
            GD.Print($"[Director] Initial terrain volume: {_initialTerrainVolume:F2} m³");

            _digSimUI.SetDigConfig(_digConfig);
            GD.Print($"🎮 [Director] Passed DigConfig to UI: DigDepth={_digConfig.DigDepth:F2}m (config hash: {_digConfig.GetHashCode()})");
            // _digSimUI.SetHeatMapStatus(false);
            _digSimUI.SetInitialVolume(_initialTerrainVolume);
            _digSimUI.SetVehicles(_vehicles);
            // Removed SetTerrain call - no longer needed without terrain thumbnail

            // Initialize progress bars to 0% and 100%
            _digSimUI.UpdateTerrainProgress(_initialTerrainVolume, _initialTerrainVolume);

            GD.Print("[Director] DigSimUI initialized successfully");

            SetCameraMode(CameraMode.TopDown);
        }

        // ---------- Input / camera (unchanged) ----------
        public override void _Input(InputEvent e)
        {
            // ---- Camera mode hotkeys ----
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                switch (k.Keycode)
                {
                    case Key.Key1: SetCameraMode(CameraMode.TopDown); return;
                    case Key.Key2: SetCameraMode(CameraMode.Orbit); return;
                    case Key.Key3: SetCameraMode(CameraMode.Free); return;
                    case Key.Key4: SetCameraMode(CameraMode.VehicleFollow); return;

                    case Key.R:
                        GetTree().ReloadCurrentScene();
                        return;

                    case Key.P:
                        _simPaused = !_simPaused;
                        GD.Print($"Sim paused: {_simPaused}");
                        // Tell all vehicles to respect sim pause
                        foreach (var v in _vehicles)
                            v.SimPaused = _simPaused;
                        return;

                    case Key.Left:
                        if (_mode == CameraMode.VehicleFollow) { CycleFollowTarget(-1); return; }
                        break;
                    case Key.Right:
                        if (_mode == CameraMode.VehicleFollow) { CycleFollowTarget(+1); return; }
                        break;
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
                    Vector3 forward = -_camFree.GlobalTransform.Basis.Z.Normalized();
                    Vector3 newPosition = _camFree.GlobalTransform.Origin + forward * -ZoomSensitivity;
                    _camFree.GlobalTransform = new Transform3D(_camFree.GlobalTransform.Basis, newPosition);
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    Vector3 forward = -_camFree.GlobalTransform.Basis.Z.Normalized();
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
                if (mb2.ButtonIndex == MouseButton.WheelUp) Distance += ZoomSensitivity;
                else if (mb2.ButtonIndex == MouseButton.WheelDown) Distance -= ZoomSensitivity;

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

        private float CalculateTerrainVolume()
        {
            if (_terrain == null || _terrain.HeightGrid == null)
                return 0f;

            double totalVolume = 0.0;
            float gridStep = _terrain.GridStep;
            float cellArea = gridStep * gridStep;
            float baseLevel = _terrain.FloorY;

            for (int i = 0; i < _terrain.GridResolution; i++)
            {
                for (int j = 0; j < _terrain.GridResolution; j++)
                {
                    float height = _terrain.HeightGrid[i, j];
                    if (!float.IsNaN(height))
                    {
                        float adjustedHeight = height - baseLevel;
                        if (adjustedHeight > 0)
                            totalVolume += adjustedHeight * cellArea;
                    }
                }
            }

            return (float)totalVolume;
        }


        public override void _Process(double delta)
        {
            // Only advance digging + brains when NOT paused
            if (!_simPaused)
            {
                // Update terrain dig batching
                _digService?.Update((float)delta);

                // Update robot dig behaviours
                foreach (var brain in _robotBrains)
                {
                    brain.UpdateDigBehavior((float)delta);
                }
            }

            // Update UI with robot stats
            if (_digSimUI != null && _robotBrains.Count > 0)
            {
                for (int i = 0; i < _robotBrains.Count; i++)
                {
                    var brain = _robotBrains[i];
                    var state = brain.DigState;
                    var robotPos = brain.Agent.GlobalTransform.Origin;

                    float payloadPercent = state.MaxPayload > 0 ? (state.CurrentPayload / state.MaxPayload) : 0f;
                    _digSimUI.UpdateRobotPayload(i, payloadPercent, robotPos, state.State.ToString());
                }

                // Update terrain progress
                float remainingVolume = CalculateTerrainVolume();
                _digSimUI.UpdateTerrainProgress(remainingVolume, _initialTerrainVolume);
            }

            // Original camera code
            // (keep your existing toggles if you still want them)
            if (Input.IsActionJustPressed("toggle_camera"))
            {
                // Backward-compat: flip between top and follow
                if (_mode == CameraMode.TopDown) SetCameraMode(CameraMode.VehicleFollow);
                else if (_mode == CameraMode.VehicleFollow) SetCameraMode(CameraMode.TopDown);
            }

            if (_mode == CameraMode.VehicleFollow) FollowChaseCamera(delta);

            if (Input.IsActionJustPressed("select_free_camera")) SetCameraMode(CameraMode.Free);
            if (Input.IsActionJustPressed("select_orbit_camera")) SetCameraMode(CameraMode.Orbit);

            // Check if mouse is over UI before capturing
            bool mouseOverUI = IsMouseOverUI();

            if (Input.IsActionPressed("translate_free_camera") && _camFree.Current && !mouseOverUI)
            {
                _movingFreeCam = true; _rotatingFreeCam = _rotatingOrbitCam = false;
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            else if (Input.IsActionPressed("rotate_camera") && !mouseOverUI)
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

            if (_camFree.Current)
            {
                // Camera-forward/right projected to XZ (ignore pitch)
                Vector3 camForward = -_camFree.GlobalTransform.Basis.Z;
                Vector3 camRight = _camFree.GlobalTransform.Basis.X;

                Vector3 fwdXZ = new Vector3(camForward.X, 0, camForward.Z);
                Vector3 rightXZ = new Vector3(camRight.X, 0, camRight.Z);

                const float EPS = 1e-4f;
                if (fwdXZ.LengthSquared() < EPS) fwdXZ = new Vector3(0, 0, -1);
                if (rightXZ.LengthSquared() < EPS) rightXZ = new Vector3(1, 0, 0);

                fwdXZ = fwdXZ.Normalized();
                rightXZ = rightXZ.Normalized();

                // Build ground-plane intent via InputMap actions
                Vector3 move = Vector3.Zero;
                if (Input.IsActionPressed("free_forward")) move += fwdXZ;
                if (Input.IsActionPressed("free_back")) move -= fwdXZ;
                if (Input.IsActionPressed("free_right")) move += rightXZ;
                if (Input.IsActionPressed("free_left")) move -= rightXZ;

                // Add vertical movement in world-Y (Space/Ctrl)
                float upDown = 0f;
                if (Input.IsActionPressed("free_up")) upDown += 1f;
                if (Input.IsActionPressed("free_down")) upDown -= 1f;

                // Speed
                float speed = Input.IsActionPressed("free_sprint") ? FreeMoveSpeedFast : FreeMoveSpeed;

                // Apply movement
                if (move != Vector3.Zero || MathF.Abs(upDown) > 0f)
                {
                    move = move.Normalized();
                    Vector3 pos = _camFree.GlobalTransform.Origin;

                    // XZ move (camera-relative, flattened)
                    Vector3 deltaXZ = move * speed * (float)delta;

                    // Y move (world up/down)
                    Vector3 deltaY = new Vector3(0, upDown * speed * (float)delta, 0);

                    Vector3 newPos = pos + deltaXZ + deltaY;

                    _camFree.GlobalTransform = new Transform3D(_camFree.GlobalTransform.Basis, newPos);
                }
            }
        }

        private void FollowChaseCamera(double delta)
        {
            if (_vehicles.Count == 0) return;

            var car = _vehicles[Mathf.Clamp(_followIndex, 0, _vehicles.Count - 1)];
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

        private void SetCameraMode(CameraMode m)
        {
            _mode = m;

            // Reset "Current" flags
            _camTop.Current = false;
            _camChase.Current = false;
            _camFree.Current = false;
            _camOrbit.Current = false;

            // Reset mouse capture state on mode changes
            _movingFreeCam = _rotatingFreeCam = _rotatingOrbitCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;

            switch (_mode)
            {
                case CameraMode.TopDown:
                    _camTop.Current = true;
                    break;

                case CameraMode.VehicleFollow:
                    // clamp follow index if vehicles changed
                    if (_vehicles.Count > 0)
                        _followIndex = Mathf.PosMod(_followIndex, _vehicles.Count);
                    _camChase.Current = true;
                    break;

                case CameraMode.Free:
                    _camFree.Current = true;
                    break;

                case CameraMode.Orbit:
                    _camOrbit.Current = true;
                    break;
            }
        }

        // Wrap index cleanly when cycling vehicles
        private void CycleFollowTarget(int delta)
        {
            if (_vehicles.Count == 0) return;
            _followIndex = (_followIndex + delta) % _vehicles.Count;
            if (_followIndex < 0) _followIndex += _vehicles.Count;
        }

        // ------------------------------------------------

        // === Placement on terrain using FL/FR/RC (unchanged) =======================
        private void PlaceOnTerrain(VehicleVisualizer car, Vector3 outward)
        {
            var yawBasis = Basis.LookingAt(outward, Vector3.Up);
            float halfL = VehicleLength * 0.5f;
            float halfW = VehicleWidth * 0.5f;

            Vector3 f = -yawBasis.Z;      // Godot forward is -Z
            Vector3 r = yawBasis.X;

            Vector3 centerXZ = car.GlobalTransform.Origin; centerXZ.Y = 0;

            Vector3 pFL = centerXZ + f * halfL + r * halfW;
            Vector3 pFR = centerXZ + f * halfL - r * halfW;
            Vector3 pRC = centerXZ - f * halfL;

            _terrain.SampleHeightNormal(centerXZ, out var hC, out var nC);
            _terrain.SampleHeightNormal(pFL, out var hFL, out var _);
            _terrain.SampleHeightNormal(pFR, out var hFR, out var _);
            _terrain.SampleHeightNormal(pRC, out var hRC, out var _);

            Vector3 n = (hFR - hFL).Cross(hRC - hFL);
            if (n.LengthSquared() < 1e-6f) n = nC;
            n = n.Normalized();

            if (NormalBlend > 0f) n = n.Lerp(nC, Mathf.Clamp(NormalBlend, 0f, 1f)).Normalized();

            Vector3 yawFwd = new Vector3(outward.X, 0, outward.Z).Normalized();
            Vector3 fProj = (yawFwd - n * yawFwd.Dot(n)); if (fProj.LengthSquared() < 1e-6f) fProj = yawFwd; fProj = fProj.Normalized();

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
            var to = xz + new Vector3(0, -1000f, 0);
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
            AddChild(mi);

            im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                var y = SampleSurfaceY(p) + lift;
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
            var y = SampleSurfaceY(pos) + lift;
            m.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(pos.X, y + 0.01f, pos.Z));

            var mat = new StandardMaterial3D { AlbedoColor = col, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            mat.NoDepthTest = DebugPathOnTop;
            m.SetSurfaceOverrideMaterial(0, mat);
            AddChild(m);
        }

        private bool IsMouseOverUI()
        {
            if (_digSimUI == null || !_digSimUI.Visible) return false;

            var mousePos = GetViewport().GetMousePosition();

            // Check if mouse is over the UI panel or any of its children
            return IsPointInControl(_digSimUI, mousePos);
        }

        private bool IsPointInControl(Control control, Vector2 point)
        {
            if (!control.Visible) return false;

            // Check if point is in this control's rectangle
            var rect = control.GetGlobalRect();
            if (rect.HasPoint(point))
            {
                return true;
            }

            // Recursively check all Control children
            foreach (var child in control.GetChildren())
            {
                if (child is Control childControl && IsPointInControl(childControl, point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}