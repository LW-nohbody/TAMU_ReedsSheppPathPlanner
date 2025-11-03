using Godot;
using System;
using System.Collections.Generic;
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
        [Export] public NodePath CameraOrbitPath { get; set; } = null!;
        [Export] public NodePath TerrainPath = null!;

        // Spawn / geometry
        [Export] public int VehicleCount = 8;
        [Export] public float SpawnRadius = 2.0f;
        [Export] public float VehicleLength = 2.0f;
        [Export] public float VehicleWidth = 1.2f;
        [Export] public float RideHeight = 0.25f;
        [Export] public float NormalBlend = 0.2f;

        // RS params
        [Export] public float GoalAdvance = 5.0f;
        [Export] public float TurnRadiusMeters = 2.0f;
        [Export] public float SampleStepMeters = 0.25f;

        // Cameras
        [Export] public float MouseSensitivity = 0.005f;
        [Export] public float TranslateSensitivity = 0.01f;
        [Export] public float ZoomSensitivity = 0.5f;
        [Export] public float ChaseLerp = 8.0f;
        [Export] public Vector3 ChaseOffset = new(0, 2.5f, 5.5f);
        [Export] public float FreeMoveSpeed = 12.0f;       // m/s
        [Export] public float FreeSprintMultiplier = 1.5f;

        // Debug
        [Export] public bool DebugPathOnTop = true;
        [Export] public bool DebugShowGrid = true;
        [Export] public float DebugGridY = 1.0f;                 // ~1m above ground like 3D
        [Export] public Color DebugGridColor = new Color(1, 0, 0, 0.35f);
        [Export] public float DebugGridCell = 0.25f;                // MUST match BuildGrid gridSize
        [Export] public int DebugGridExtent = 60;                   // MUST match BuildGrid extent

        // Obstacles (decoupled)
        [Export] public NodePath ObstacleRootPath = null!;

        // Shared extra safety beyond body half-extent
        [Export] public float ExtraObstacleBuffer = 0.10f; // <- you referenced this; now exported

        private TerrainDisk _terrain = null!;
        private Node3D _vehiclesRoot = null!;
        private readonly List<VehicleVisualizer> _vehicles = new();

        private Camera3D _camTop = null!, _camChase = null!, _camFree = null!, _camOrbit = null!;
        private bool _usingTop = true;
        private bool _movingFreeCam = false, _rotatingFreeCam = false, _rotatingOrbitCam = false;
        private float _freePitch = 0f, _freeYaw = 0f, _orbitPitch = 0, _orbitYaw = 0, Distance = 15.0f;
        private float MinPitchDeg = -5, MaxPitchDeg = 89, MinDist = 0.5f, MaxDist = 18f;

        public override void _Ready()
        {
            // Nodes
            _vehiclesRoot = GetNode<Node3D>(VehiclesRootPath);
            _camTop = GetNode<Camera3D>(CameraTopPath);
            _camChase = GetNode<Camera3D>(CameraChasePath);
            _camFree = GetNode<Camera3D>(CameraFreePath);
            _camOrbit = GetNode<Camera3D>(CameraOrbitPath);

            // Terrain
            _terrain = GetNodeOrNull<TerrainDisk>(TerrainPath);
            if (_terrain == null) { GD.PushError("TerrainPath must point to a TerrainDisk."); return; }

            // Spawn ring
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

                _vehicles.Add(car);
            }

            // --- Unified clearance buffer (uses the larger body dimension) ---
            float vehicleHalfExtent = Mathf.Max(VehicleWidth, VehicleLength) * 0.5f;
            float inflation = vehicleHalfExtent + ExtraObstacleBuffer;

            // Obstacles → data (load BEFORE scheduling)
            var obstacles = new List<Obstacle3D>();
            if (ObstacleRootPath != null && !ObstacleRootPath.IsEmpty)
            {
                var root = GetNodeOrNull<Node>(ObstacleRootPath);
                if (root != null) obstacles = ObstacleAdapter.ReadFromScene(root);
            }

            GD.Print($"[Obstacles] count = {obstacles.Count}");
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                GD.Print($"  #{i} {o.Shape} @ {o.Center}  R={o.Radius}  Ext={o.Extents}");
            }

            // Build grid (matches 3D numbers) with the SAME inflation
            if (obstacles.Count > 0)
            {
                GridPlannerPersistent.BuildGrid(
                    obstacles,
                    gridSize: 0.25f,
                    gridExtent: 60,
                    obstacleBufferMeters: inflation);
            }

            if (DebugShowGrid)
                DrawBlockedGridLike3D(GridPlannerPersistent.LastBlockedCenters);

            // Plan first dig targets (NOW pass obstacles + inflation)
            var scheduler = new RadialScheduler();
            var brains = new List<VehicleBrain>(_vehicles.Count);
            foreach (var v in _vehicles) { var b = new VehicleBrain(); v.AddChild(b); brains.Add(b); }

            var center = _terrain.GlobalTransform.Origin;

            var digTargets = scheduler.PlanFirstDigTargets(
                brains, _terrain, center, DigScoring.Default,
                keepoutR: 2.0f,              // your existing value
                randomizeOrder: false,       // your existing value
                obstacles: obstacles,        // NEW
                inflation: inflation         // NEW (matches grid + planner)
            );

            if (ObstacleRootPath != null && !ObstacleRootPath.IsEmpty)
            {
                var root = GetNodeOrNull<Node>(ObstacleRootPath);
                if (root != null) obstacles = ObstacleAdapter.ReadFromScene(root);
            }

            // Build grid with EXACT 3D numbers but using our computed inflation
            if (obstacles.Count > 0)
            {
                GridPlannerPersistent.BuildGrid(
                    obstacles,
                    gridSize: 0.25f,
                    gridExtent: 60,
                    obstacleBufferMeters: inflation);
            }

            // Build RS paths (hybrid)
            for (int k = 0; k < _vehicles.Count; k++)
            {
                var car = _vehicles[k];
                var (digPos, approachYaw) = digTargets[k];

                var fwd = (-car.GlobalTransform.Basis.Z);
                double startYaw = MathF.Atan2(fwd.Z, fwd.X);
                var start = car.GlobalTransform.Origin;

                var (ptsL, gearsL) = HybridPlanner.Plan(
                    start, startYaw,
                    digPos, approachYaw,
                    TurnRadiusMeters, SampleStepMeters,
                    obstacles,
                    obstacleBufferMeters: inflation); // <<< use the SAME inflation

                var pts = (ptsL != null && ptsL.Count > 0) ? ptsL.ToArray() : Array.Empty<Vector3>();
                var gears = (gearsL != null && gearsL.Count > 0) ? gearsL.ToArray() : Array.Empty<int>();

                DrawMarkerProjected(start, new Color(0, 1, 0));
                DrawMarkerProjected(digPos, new Color(0, 0, 1));
                DrawPathProjectedToTerrain(pts, new Color(0.15f, 0.9f, 1.0f));
                _vehicles[k].SetPath(pts, gears);
            }
        }

        // ---------- Input / camera ----------//
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

            // <-- Add this line
            MoveFreeCamera(delta);
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

        private void MoveFreeCamera(double delta)
        {
            if (!_camFree.Current) return;

            // Inputs
            float f = Input.GetActionStrength("free_forward") - Input.GetActionStrength("free_back");
            float r = Input.GetActionStrength("free_right") - Input.GetActionStrength("free_left");
            float u = Input.GetActionStrength("free_up") - Input.GetActionStrength("free_down");

            if (Mathf.IsZeroApprox(f) && Mathf.IsZeroApprox(r) && Mathf.IsZeroApprox(u)) return;

            // --- Build axes that ignore camera pitch ---
            // Project camera's forward/right onto the XZ plane so WASD doesn't inherit pitch.
            Basis B = _camFree.GlobalTransform.Basis;

            Vector3 forward = -B.Z;         // Godot forward = -Z
            forward.Y = 0;                   // drop pitch
            if (forward.LengthSquared() < 1e-8f) forward = new Vector3(0, 0, -1);
            forward = forward.Normalized();

            Vector3 right = B.X;
            right.Y = 0;                     // drop roll
            if (right.LengthSquared() < 1e-8f) right = new Vector3(1, 0, 0);
            right = right.Normalized();

            // Up is world-up so Space always goes +Y regardless of where you're looking
            Vector3 up = Vector3.Up;

            // Compose movement
            Vector3 v = forward * f + right * r + up * u;
            if (v.LengthSquared() > 1e-8f) v = v.Normalized();

            float speed = FreeMoveSpeed;
            if (Input.IsActionPressed("free_sprint")) speed *= FreeSprintMultiplier;

            _camFree.GlobalTranslate(v * speed * (float)delta);
        }

        // -------- Terrain projection helpers --------
        private float SampleSurfaceY(Vector3 xz)
        {
            if (_terrain != null && _terrain.SampleHeightNormal(xz, out var hit, out var _))
                return hit.Y;

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

        // --- Classic "red squares" like the 3D project ---
        private void DrawBlockedGridLike3D(IReadOnlyList<Vector2> centers)
        {
            if (centers == null || centers.Count == 0) return;

            // Fill quads
            var fillMI = new MeshInstance3D { Name = "DebugGridFill" };
            var fillIM = new ImmediateMesh();
            fillMI.Mesh = fillIM;
            AddChild(fillMI);

            // Thin inner grid lines
            var lineMI = new MeshInstance3D { Name = "DebugGridLines" };
            var lineIM = new ImmediateMesh();
            lineMI.Mesh = lineIM;
            AddChild(lineMI);

            float half = DebugGridCell * 0.45f; // same visual as 3D
            float y = DebugGridY;

            // Fill
            fillIM.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            foreach (var c in centers)
            {
                var a = new Vector3(c.X - half, y, c.Y - half);
                var b = new Vector3(c.X + half, y, c.Y - half);
                var d = new Vector3(c.X - half, y, c.Y + half);
                var e = new Vector3(c.X + half, y, c.Y + half);

                fillIM.SurfaceAddVertex(a); fillIM.SurfaceAddVertex(b); fillIM.SurfaceAddVertex(e);
                fillIM.SurfaceAddVertex(a); fillIM.SurfaceAddVertex(e); fillIM.SurfaceAddVertex(d);
            }
            fillIM.SurfaceEnd();

            var fillMat = new StandardMaterial3D
            {
                AlbedoColor = DebugGridColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            fillMI.SetSurfaceOverrideMaterial(0, fillMat);

            // Lines (simple 3x3 grid inside each square)
            lineIM.SurfaceBegin(Mesh.PrimitiveType.Lines);
            foreach (var c in centers)
            {
                float step = (half * 2f) / 3f;

                for (int i = 1; i <= 2; i++)
                {
                    float x = c.X - half + i * step;
                    lineIM.SurfaceAddVertex(new Vector3(x, y, c.Y - half));
                    lineIM.SurfaceAddVertex(new Vector3(x, y, c.Y + half));

                    float z = c.Y - half + i * step;
                    lineIM.SurfaceAddVertex(new Vector3(c.X - half, y, z));
                    lineIM.SurfaceAddVertex(new Vector3(c.X + half, y, z));
                }
            }
            lineIM.SurfaceEnd();

            var lineMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 0, 0, 0.8f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            lineMI.SetSurfaceOverrideMaterial(0, lineMat);
        }

        // --- placement on terrain ---
        private void PlaceOnTerrain(VehicleVisualizer car, Vector3 outward)
        {
            var yawBasis = Basis.LookingAt(outward, Vector3.Up);
            float halfL = VehicleLength * 0.5f;
            float halfW = VehicleWidth * 0.5f;

            Vector3 f = -yawBasis.Z;   // forward (-Z)
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
            Vector3 fProj = (yawFwd - n * yawFwd.Dot(n));
            if (fProj.LengthSquared() < 1e-6f) fProj = yawFwd; fProj = fProj.Normalized();

            Vector3 right = n.Cross(fProj).Normalized();
            Vector3 zAxis = -fProj;
            var basis = new Basis(right, n, zAxis).Orthonormalized();

            float ride = Mathf.Clamp(RideHeight, 0.02f, 0.12f);
            Vector3 pos = hC + n * ride;

            car.GlobalTransform = new Transform3D(basis, pos);
        }
    }
}