using Godot;
using System;
using System.Collections.Generic;
using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    /// <summary>
    /// Enhanced SimulationDirector for "Highest-First Cooperative Digging"
    /// - Spawns robots in radial sectors
    /// - Initializes RobotCoordinator for collision avoidance
    /// - Creates TargetIndicator system for visual feedback
    /// - Each robot continuously digs highest points in its sector
    /// - Terrain uses vertex coloring (Yellow/Green/Blue/Purple height bands)
    /// </summary>
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
        [Export] public float TurnRadiusMeters = 2.0f;
        [Export] public float SampleStepMeters = 0.25f;

        // Dig params
        [Export] public float MaxDigRadius = 15.0f;
        [Export] public Vector3 DumpLocation = Vector3.Zero;
        [Export] public float MinRobotSeparation = 3.0f;

        // Cameras
        [Export] public float MouseSensitivity = 0.005f;
        [Export] public float TranslateSensitivity = 0.01f;
        [Export] public float ZoomSensitivity = 0.5f;
        [Export] public float ChaseLerp = 8.0f;
        [Export] public Vector3 ChaseOffset = new(0, 2.5f, 5.5f);
        [Export] public float FreeMoveSpeed = 12.0f;
        [Export] public float FreeSprintMultiplier = 1.5f;

        // Debug
        [Export] public bool DebugPathOnTop = true;
        [Export] public bool DebugShowGrid = true;
        [Export] public float DebugGridY = 1.0f;
        [Export] public Color DebugGridColor = new Color(1, 0, 0, 0.35f);
        [Export] public float DebugGridCell = 0.25f;
        [Export] public int DebugGridExtent = 60;
        [Export] public bool DrawSectorLines = true;

        // Obstacles
        [Export] public NodePath ObstacleRootPath = null!;
        [Export] public float ExtraObstacleBuffer = 0.10f;

        private TerrainDisk _terrain = null!;
        private Node3D _vehiclesRoot = null!;
        private readonly List<VehicleVisualizer> _vehicles = new();
        private readonly List<VehicleBrain> _brains = new();

        private Camera3D _camTop = null!, _camChase = null!, _camFree = null!, _camOrbit = null!;
        private bool _usingTop = true;
        private bool _movingFreeCam = false, _rotatingFreeCam = false, _rotatingOrbitCam = false;
        private float _freePitch = 0f, _freeYaw = 0f, _orbitPitch = 0, _orbitYaw = 0, Distance = 15.0f;
        private float MinPitchDeg = -5, MaxPitchDeg = 89, MinDist = 0.5f, MaxDist = 18f;

        // Services
        private RobotCoordinator _coordinator = null!;
        private TargetIndicator _targetIndicator = null!;
        private PathVisualizer _pathVisualizer = null!;
        private PlannedPathVisualizer _plannedPathVisualizer = null!;
        private SimulationHUD _hud = null!;
        private HeatMapLegend _heatMapLegend = null!;
        private List<Obstacle3D> _obstacles = new();
        
        // UI toggles
        private bool _heatMapVisible = true;
        private bool _pathsVisible = true;
        private bool _plannedPathsVisible = true;
        private bool _heatMapToggled = false;
        private bool _pathToggled = false;
        private bool _plannedPathToggled = false;
        private bool _clearToggled = false;

        // Sector colors (for visual feedback)
        private readonly Color[] _sectorColors = new[]
        {
            new Color(1.0f, 0.2f, 0.2f),   // Red
            new Color(0.2f, 1.0f, 0.2f),   // Green
            new Color(0.2f, 0.5f, 1.0f),   // Blue
            new Color(1.0f, 0.8f, 0.2f),   // Yellow
            new Color(1.0f, 0.4f, 0.8f),   // Pink
            new Color(0.4f, 1.0f, 0.8f),   // Cyan
            new Color(1.0f, 0.6f, 0.2f),   // Orange
            new Color(0.6f, 0.4f, 1.0f),   // Purple
        };

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

            // Initialize services
            _coordinator = new RobotCoordinator(MinRobotSeparation);
            
            _targetIndicator = new TargetIndicator();
            AddChild(_targetIndicator);
            _targetIndicator.Initialize(_terrain);
            
            // Initialize path visualizer
            _pathVisualizer = new PathVisualizer();
            AddChild(_pathVisualizer);
            _pathVisualizer.Visible = _pathsVisible;
            
            // Initialize planned path visualizer
            _plannedPathVisualizer = new PlannedPathVisualizer();
            AddChild(_plannedPathVisualizer);
            _plannedPathVisualizer.Visible = _plannedPathsVisible;
            
            // Initialize HUD
            _hud = new SimulationHUD();
            AddChild(_hud);
            
            // Initialize heat map legend
            _heatMapLegend = new HeatMapLegend();
            AddChild(_heatMapLegend);
            _heatMapLegend.Visible = _heatMapVisible;

            // Load obstacles
            _obstacles = new List<Obstacle3D>();
            if (ObstacleRootPath != null && !ObstacleRootPath.IsEmpty)
            {
                var root = GetNodeOrNull<Node>(ObstacleRootPath);
                if (root != null) _obstacles = ObstacleAdapter.ReadFromScene(root);
            }

            GD.Print($"[SimulationDirector] Obstacles: {_obstacles.Count}");

            // Build obstacle grid
            float vehicleHalfExtent = Mathf.Max(VehicleWidth, VehicleLength) * 0.5f;
            float inflation = vehicleHalfExtent + ExtraObstacleBuffer;

            if (_obstacles.Count > 0)
            {
                GridPlannerPersistent.BuildGrid(
                    _obstacles,
                    gridSize: DebugGridCell,
                    gridExtent: DebugGridExtent,
                    obstacleBufferMeters: inflation);
            }

            if (DebugShowGrid)
                DrawBlockedGrid(GridPlannerPersistent.LastBlockedCenters);

            // Spawn robots in sectors
            int N = Math.Max(1, VehicleCount);
            float sectorSize = Mathf.Tau / N;

            for (int i = 0; i < N; i++)
            {
                float theta = i * sectorSize;
                float thetaMin = theta;
                float thetaMax = theta + sectorSize;
                
                var outward = new Vector3(Mathf.Cos(theta + sectorSize / 2), 0, Mathf.Sin(theta + sectorSize / 2)).Normalized();
                var spawnXZ = outward * SpawnRadius;

                var car = VehicleScene.Instantiate<VehicleVisualizer>();
                _vehiclesRoot.AddChild(car);
                car.SetTerrain(_terrain);
                car.GlobalTransform = new Transform3D(Basis.Identity, spawnXZ);

                PlaceOnTerrain(car, outward);

                car.Wheelbase = VehicleLength;
                car.TrackWidth = VehicleWidth;

                _vehicles.Add(car);

                // Create brain
                var brain = new VehicleBrain
                {
                    RobotId = i,
                    ThetaMin = thetaMin,
                    ThetaMax = thetaMax,
                    MaxRadius = MaxDigRadius,
                    DumpLocation = DumpLocation,
                    SectorColor = _sectorColors[i % _sectorColors.Length],
                    Terrain = _terrain,
                    Coordinator = _coordinator,
                    TargetIndicator = _targetIndicator,
                    PathVisualizer = _pathVisualizer,
                    PlannedPathVisualizer = _plannedPathVisualizer,
                    TurnRadius = TurnRadiusMeters,
                    SampleStep = SampleStepMeters,
                    Obstacles = _obstacles,
                    ObstacleBuffer = inflation
                };

                car.AddChild(brain);
                _brains.Add(brain);
                
                // Register vehicle for path tracking with sector color
                _pathVisualizer.RegisterVehicle(i, _sectorColors[i % _sectorColors.Length]);
                _plannedPathVisualizer.RegisterVehicle(i, _sectorColors[i % _sectorColors.Length]);

                GD.Print($"[Robot {i}] Sector: {Mathf.RadToDeg(thetaMin):F1}° to {Mathf.RadToDeg(thetaMax):F1}°");
            }

            // Draw sector boundary lines (optional)
            if (DrawSectorLines)
            {
                DrawSectorBoundaries(N, MaxDigRadius);
            }

            GD.Print("[SimulationDirector] New dig logic initialized!");
            GD.Print($"  - {N} robots in radial sectors");
            GD.Print($"  - Collision avoidance: {MinRobotSeparation}m separation");
            GD.Print($"  - Terrain vertex coloring enabled");
            GD.Print($"  - Target indicators active");
        }

        // ---------- Input / camera ----------
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
            // Toggle heat map with 'H' key
            if (Input.IsKeyPressed(Key.H))
            {
                if (!_heatMapToggled)
                {
                    _heatMapToggled = true;
                    _heatMapVisible = !_heatMapVisible;
                    _terrain.HeatMapEnabled = _heatMapVisible;
                    _heatMapLegend.Visible = _heatMapVisible; // Sync legend with heat map
                    GD.Print($"[SimulationDirector] Heat map: {(_heatMapVisible ? "ON" : "OFF")}");
                }
            }
            else
            {
                _heatMapToggled = false;
            }
            
            // Toggle TRAVELED paths with 'P' key
            if (Input.IsKeyPressed(Key.P))
            {
                if (!_pathToggled)
                {
                    _pathToggled = true;
                    _pathsVisible = !_pathsVisible;
                    _pathVisualizer.Visible = _pathsVisible;
                    GD.Print($"[SimulationDirector] Traveled paths: {(_pathsVisible ? "ON" : "OFF")}");
                }
            }
            else
            {
                _pathToggled = false;
            }
            
            // Toggle PLANNED paths with 'L' key
            if (Input.IsKeyPressed(Key.L))
            {
                if (!_plannedPathToggled)
                {
                    _plannedPathToggled = true;
                    _plannedPathsVisible = !_plannedPathsVisible;
                    _plannedPathVisualizer.Visible = _plannedPathsVisible;
                    GD.Print($"[SimulationDirector] Planned paths: {(_plannedPathsVisible ? "ON" : "OFF")}");
                }
            }
            else
            {
                _plannedPathToggled = false;
            }
            
            // Clear paths with 'C' key
            if (Input.IsKeyPressed(Key.C))
            {
                if (!_clearToggled)
                {
                    _clearToggled = true;
                    _pathVisualizer.ClearAllPaths();
                    GD.Print("[SimulationDirector] Traveled paths cleared");
                }
            }
            else
            {
                _clearToggled = false;
            }
            
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

            MoveFreeCamera(delta);
            
            // Update HUD stats
            float totalDirt = CalculateTotalDirtExtracted();
            _hud.UpdateStats(_vehicles.Count, totalDirt, _heatMapVisible, _pathsVisible, _plannedPathsVisible);
        }
        
        private float CalculateTotalDirtExtracted()
        {
            // Sum up all dirt from all brains
            float total = 0f;
            foreach (var brain in _brains)
            {
                total += brain.TotalPayloadDelivered;
            }
            return total;
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

            float f = Input.GetActionStrength("free_forward") - Input.GetActionStrength("free_back");
            float r = Input.GetActionStrength("free_right") - Input.GetActionStrength("free_left");
            float u = Input.GetActionStrength("free_up") - Input.GetActionStrength("free_down");

            if (Mathf.IsZeroApprox(f) && Mathf.IsZeroApprox(r) && Mathf.IsZeroApprox(u)) return;

            Basis B = _camFree.GlobalTransform.Basis;

            Vector3 forward = -B.Z;
            forward.Y = 0;
            if (forward.LengthSquared() < 1e-8f) forward = new Vector3(0, 0, -1);
            forward = forward.Normalized();

            Vector3 right = B.X;
            right.Y = 0;
            if (right.LengthSquared() < 1e-8f) right = new Vector3(1, 0, 0);
            right = right.Normalized();

            Vector3 up = Vector3.Up;

            Vector3 v = forward * f + right * r + up * u;
            if (v.LengthSquared() > 1e-8f) v = v.Normalized();

            float speed = FreeMoveSpeed;
            if (Input.IsActionPressed("free_sprint")) speed *= FreeSprintMultiplier;

            _camFree.GlobalTranslate(v * speed * (float)delta);
        }

        // -------- Visualization helpers --------
        private void DrawBlockedGrid(IReadOnlyList<Vector2> centers)
        {
            if (centers == null || centers.Count == 0) return;

            var fillMI = new MeshInstance3D { Name = "DebugGridFill" };
            var fillIM = new ImmediateMesh();
            fillMI.Mesh = fillIM;
            AddChild(fillMI);

            var lineMI = new MeshInstance3D { Name = "DebugGridLines" };
            var lineIM = new ImmediateMesh();
            lineMI.Mesh = lineIM;
            AddChild(lineMI);

            float half = DebugGridCell * 0.45f;
            float y = DebugGridY;

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

        private void DrawSectorBoundaries(int sectorCount, float radius)
        {
            var mi = new MeshInstance3D { Name = "SectorBoundaries" };
            var im = new ImmediateMesh();
            mi.Mesh = im;
            AddChild(mi);

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);

            float y = 0.05f; // Slightly above ground
            float sectorSize = Mathf.Tau / sectorCount;

            for (int i = 0; i < sectorCount; i++)
            {
                float theta = i * sectorSize;
                Vector3 inner = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * 0.5f;
                Vector3 outer = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * radius;

                if (_terrain.SampleHeightNormal(inner, out var hitInner, out var _))
                    inner.Y = hitInner.Y + y;
                if (_terrain.SampleHeightNormal(outer, out var hitOuter, out var _))
                    outer.Y = hitOuter.Y + y;

                im.SurfaceAddVertex(inner);
                im.SurfaceAddVertex(outer);
            }

            im.SurfaceEnd();

            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, 0.6f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            mi.SetSurfaceOverrideMaterial(0, mat);
        }

        private void PlaceOnTerrain(VehicleVisualizer car, Vector3 outward)
        {
            var yawBasis = Basis.LookingAt(outward, Vector3.Up);
            float halfL = VehicleLength * 0.5f;
            float halfW = VehicleWidth * 0.5f;

            Vector3 f = -yawBasis.Z;
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
