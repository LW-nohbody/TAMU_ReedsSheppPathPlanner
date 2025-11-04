using Godot;
using System;
using System.Collections.Generic;
using System.Runtime;

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

        // Debug
        [Export] public bool DebugPathOnTop = true;

        private TerrainDisk _terrain = null!;
        private Node3D _vehiclesRoot = null!;
        private readonly List<VehicleVisualizer> _vehicles = new();

        private Camera3D _camTop = null!, _camChase = null!, _camFree = null!, _camOrbit = null!;
        private bool _usingTop = true;
        private bool _movingFreeCam = false, _rotatingFreeCam = false, _rotatingOrbitCam = false;
        private float _freePitch = 0f, _freeYaw = 0f, _orbitPitch = 0, _orbitYaw = 0, Distance = 15.0f;
        private float MinPitchDeg = -5, MaxPitchDeg = 89, MinDist = 0.5f, MaxDist = 18f;
        private float lift = 0.04f;

        [Export] public NodePath ObstacleManagerPath = null!;
        private ObstacleManager _obstacleManager = null!;
        private SimulationSettingsUI _settingsUI = null!;


        public override void _Ready()
        {
            // Initialize settings UI first
            _settingsUI = new SimulationSettingsUI();
            AddChild(_settingsUI);
            GD.Print("[Director] ✅ Simulation Settings UI initialized");

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
            GridPlannerPersistent.BuildGrid(obstacleList, gridSize: 0.25f, gridExtent: 60, obstacleBufferMeters: 1.0f);

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

                _vehicles.Add(car);                    // <-- put this back
            }

            // === PLAN FIRST DIG TARGETS (after all cars exist) ===
            var scheduler = new RadialScheduler();
            var brains = new List<VehicleBrain>(_vehicles.Count);
            foreach (var v in _vehicles)
            {
                var brain = new VehicleBrain();
                v.AddChild(brain);
                brains.Add(brain);
            }

            var digTargets = scheduler.PlanFirstDigTargets(
                brains, _terrain, Vector3.Zero, DigScoring.Default);

            // === Build paths to the assigned dig targets (scheduler-driven) ===
            for (int k = 0; k < _vehicles.Count; k++)
            {
                var car = _vehicles[k];
                var (digPos, approachYaw) = digTargets[k];

                // current forward yaw in XZ (Godot forward is -Z)
                var fwd = -car.GlobalTransform.Basis.Z;
                double startYaw = MathF.Atan2(fwd.Z, fwd.X);
                var start = car.GlobalTransform.Origin;

                // Planner poses use X/Z + yaw
                var startPose = new Pose(start.X, start.Z, startYaw);
                var goalPose = new Pose(digPos.X, digPos.Z, approachYaw);

                // Vehicle + world (note: Obstacles is List<Obstacle3D>)
                VehicleSpec spec = car.Spec;
                var world = new WorldState
                {
                    Obstacles = obstacleList,   // <— List<Obstacle3D>
                    Terrain = _terrain
                };

                var hybridRSPlanner = new HybridReedsSheppPlanner();
                PlannedPath path = hybridRSPlanner.Plan(startPose, goalPose, spec, world);

                DrawPathProjectedToTerrain(path.Points.ToArray(), new Color(0.15f, 0.9f, 1.0f));
                DrawMarkerProjected(start, new Color(0, 1, 0));
                DrawMarkerProjected(digPos, new Color(0, 0, 1));

                car.SetPath(path.Points.ToArray(), path.Gears.ToArray());

                GD.Print($"[Director] {car.Name} path: {path.Points.Count} samples");
            }

            _camTop.Current = true; _camChase.Current = false; _camFree.Current = false; _camOrbit.Current = false;
        }

        // ---------- Input / camera (unchanged) ----------
        public override void _Input(InputEvent e)
        {
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
    }
}