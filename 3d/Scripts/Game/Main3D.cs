using Godot;
using System;

public partial class Main3D : Node3D
{
    // 1) vehicle prefab + counts
    [Export] public PackedScene VehicleScene { get; set; }
    [Export] public int VehicleCount = 5;

    // 2) ring layout + simple goal
    [Export] public float SpawnRadius = 2.0f;   // meters, radius of the ring around (0,0)
    [Export] public float GoalAdvance = 5.0f;   // meters forward from each spawn position

    [Export] public NodePath CameraTopPath;
    [Export] public NodePath CameraChasePath;
    [Export] public NodePath CameraFreePath;
    [Export] public NodePath PathLineMeshPath; // MeshInstance3D

    [Export] public float ArenaRadius = 10f;
    [Export] public float TurnRadiusMeters = 2.0f;
    [Export] public float SampleStepMeters = 0.25f;
    [Export] public float MouseSensitivity = 0.005f;
    [Export] public float TranslateSensitivity = 0.01f;
   


    // Path drawing over ground
    [Export] public float PathLift = 0.05f;   // lift above hit point
    [Export] public uint  GroundMask = 0;     // 0 = collide with all; or set to 1 if Arena is on layer 1
    [Export] public bool  DebugPathOnTop = true;

    // Holds per-car path meshes & markers
    private Node3D _pathsParent;
    private System.Collections.Generic.List<VehicleAgent3D> _vehicles = new();
    private Camera3D _camTop, _camChase, _camFree;
    private MeshInstance3D _pathLine;
    private bool _usingTop = true;
    private bool _movingFreeCam = false;
    private bool _rotatingFreeCam = false;
    private float _pitch = 0f;
    private float _yaw = 0f;
    private Vector2 _lastMousePos;

    public override void _Ready()
    {
        // Grab cameras & path host
        _camTop = GetNode<Camera3D>(CameraTopPath);
        _camChase = GetNode<Camera3D>(CameraChasePath);
        _camFree = GetNode<Camera3D>(CameraFreePath);
        _pathLine = GetNode<MeshInstance3D>(PathLineMeshPath);
        _pathsParent = new Node3D { Name = "Paths" };
        AddChild(_pathsParent);

        // Sanity: need a vehicle prefab
        if (VehicleScene == null)
        {
            GD.PushError("Main3D: VehicleScene not assigned.");
            return;
        }

        // Spawn N vehicles on a ring
        int N = Math.Max(1, VehicleCount);
        float step = Mathf.Tau / N;         // 2π / N
        float r = SpawnRadius;

        for (int i = 0; i < N; i++)
        {
            // angle, outward direction, spawn pos
            float theta = i * step;
            var outward = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)); // unit
            var spawnPos = outward * r;

            // car should face OUTWARD (Godot's forward is -Z)
            var basis = Basis.LookingAt(outward, Vector3.Up);
            var xform = new Transform3D(basis, spawnPos);

            // instantiate car
            var car = VehicleScene.Instantiate<VehicleAgent3D>();
            AddChild(car);
            car.GlobalTransform = xform;             // put car exactly at path start
            car.ArenaRadius = ArenaRadius;       // keep your arena clamp

            // plan THIS car's path:
            //   start = (spawnPos, yaw=theta)
            //   goal  = a few meters forward, with final yaw turned 90° right
            double startYaw = theta;                 // 0=+X, CCW toward +Z
            var goalPos = spawnPos + outward * GoalAdvance;
            double goalYaw = startYaw + Mathf.Pi / 2.0;  // +90° = right in our math → 3D mapping

            var pts = RSAdapter.ComputePath3D(spawnPos, startYaw, goalPos, goalYaw,
                                            TurnRadiusMeters, SampleStepMeters);

            // pick a color per car
            Color[] pal = { new Color(1, 0, 0), new Color(1, 0.5f, 0), new Color(1, 1, 0), new Color(0, 1, 0), new Color(0, 1, 1) };
            var col = pal[i % pal.Length];

            // draw its path + start/goal markers
            DrawPathForCar(pts, col);
            DrawMarker(spawnPos, new Color(0, 1, 0)); // green = start
            DrawMarker(goalPos, new Color(0, 0, 1)); // blue  = goal

            // feed path
            car.SetPath(pts);

            _vehicles.Add(car);
        }

        // Draw just the first car's path for now (to keep your single PathLine node)
        if (_vehicles.Count > 0)
        {
            // Recompute the first path exactly like above to get the points for drawing
            float theta0 = 0f;
            var outward0 = new Vector3(Mathf.Cos(theta0), 0f, Mathf.Sin(theta0));
            var spawn0 = outward0 * SpawnRadius;
            double yaw0 = theta0;
            var goal0 = spawn0 + outward0 * GoalAdvance;
            double yaw0g = yaw0 - Mathf.Pi / 2.0;

            var pts0 = RSAdapter.ComputePath3D(spawn0, yaw0, goal0, yaw0g,
                                            TurnRadiusMeters, SampleStepMeters);
            DrawPath(pts0);
        }

        // cameras
        _camTop.Current = true;
        _camChase.Current = false;
        _camFree.Current = false;

        // If you want the chase cam to follow car 0 when you press Tab:
        // nothing else to do; FollowChaseCamera() already uses the first car if we point it there.
    }

    public override void _Input(InputEvent @event)
    {
        if ((@event is InputEventMouseMotion mouseMotion) && _rotatingFreeCam)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            RotateX(-mouseMotion.Relative.Y * MouseSensitivity);

            //Vertical Rotation
            _pitch += -mouseMotion.Relative.Y * MouseSensitivity;
            // _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-90), Mathf.DegToRad(90)); // Clamp pitch

            //Horizontal Rotation
            _yaw += -mouseMotion.Relative.X * MouseSensitivity;
            // _yaw = Mathf.Clamp(_yaw, Mathf.DegToRad(-90), Mathf.DegToRad(90)); //Clamp yaw

            _camFree.Rotation = new Vector3(_pitch, _yaw, 0);
        }

        else if ((@event is InputEventMouseMotion MouseMotion) && _movingFreeCam)
        {
            Vector2 delta = MouseMotion.Relative;

            // Get camera's axis
            Vector3 right = _camFree.GlobalTransform.Basis.X;
            Vector3 up = _camFree.GlobalTransform.Basis.Y;

            // Move the camera along its local right and up axes
            Vector3 motion = (-right * delta.X + up * delta.Y) * TranslateSensitivity;

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
            _movingFreeCam = false;
        }
        if (!_usingTop) FollowChaseCamera(delta);

        if (Input.IsActionJustPressed("select_free_camera"))
        {
            _camFree.Current = true;
            _camTop.Current = false;
            _camChase.Current = false;
            _usingTop = !_usingTop; //Toggle so when return it resumes from the previous view
        }

        // move free camera        
        if (Input.IsActionPressed("translate_free_camera"))
        {
            // get movement of mouse
            _rotatingFreeCam = false;
            _movingFreeCam = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _lastMousePos = GetViewport().GetMousePosition();
        }
        else if (Input.IsActionPressed("rotate_free_camera"))
        {
            // get movement of mouse
            _movingFreeCam = false;
            _rotatingFreeCam = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            _movingFreeCam = false;
            _rotatingFreeCam = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

    }

    private void DrawPath(Vector3[] points)
    {
        var im = new ImmediateMesh();
        im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var p in points)
            im.SurfaceAddVertex(p);
        im.SurfaceEnd();
        _pathLine.Mesh = im;

        var mat = new StandardMaterial3D { AlbedoColor = new Color(1, 0, 0) };
        _pathLine.SetSurfaceOverrideMaterial(0, mat);
    }

    private void SnapChaseCamera()
    {
        if (_vehicles.Count == 0) return;
        var targetCar = _vehicles[0];

        var back = new Vector3(0, 2.5f, 5.5f);
        var basis = targetCar.GlobalTransform.Basis;
        var pos = targetCar.GlobalTransform.Origin;
        var chase = pos + basis.Z * back.Z + Vector3.Up * back.Y;
        _camChase.GlobalTransform = new Transform3D(_camChase.GlobalTransform.Basis, chase);
        _camChase.LookAt(pos, Vector3.Up);
    }

    private void FollowChaseCamera(double delta)
    {
        if (_vehicles.Count == 0) return;
        var targetCar = _vehicles[0];

        var back = new Vector3(0, 2.5f, 5.5f);
        var basis = targetCar.GlobalTransform.Basis;
        var target = targetCar.GlobalTransform.Origin + basis.Z * back.Z + Vector3.Up * back.Y;
        var cur = _camChase.GlobalTransform.Origin;
        var next = cur.Lerp(target, (float)(8.0 * delta));
        _camChase.GlobalTransform = new Transform3D(_camChase.GlobalTransform.Basis, next);
        _camChase.LookAt(targetCar.GlobalTransform.Origin, Vector3.Up);
    }

    private float SampleSurfaceY(Vector3 xz)
    {
        // Raycast down to find the ground at this XZ
        var space = GetWorld3D().DirectSpaceState;
        var from = xz + new Vector3(0, 100f, 0);
        var to   = xz + new Vector3(0,-1000f, 0);

        var q = PhysicsRayQueryParameters3D.Create(from, to);
        if (GroundMask != 0) q.CollisionMask = GroundMask;

        var hit = space.IntersectRay(q);
        if (hit.Count > 0)
            return ((Vector3)hit["position"]).Y + PathLift;

        // Fallback if nothing hit (e.g., no collider yet)
        return PathLift;
    }

    private void DrawPathForCar(Vector3[] points, Color col)
    {
        if (points == null || points.Length < 2) return;

        var mi = new MeshInstance3D();
        var im = new ImmediateMesh();

        im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            var y = SampleSurfaceY(p);
            im.SurfaceAddVertex(new Vector3(p.X, y, p.Z));
        }
        im.SurfaceEnd();

        mi.Mesh = im;

        var mat = new StandardMaterial3D { AlbedoColor = col };
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (DebugPathOnTop) mat.NoDepthTest = true;   // draw above the ground while testing
        mi.SetSurfaceOverrideMaterial(0, mat);

        _pathsParent.AddChild(mi);
    }

    private void DrawMarker(Vector3 pos, Color col)
    {
        var m = new MeshInstance3D();
        m.Mesh = new CylinderMesh
        {
            TopRadius = 0.07f,
            BottomRadius = 0.07f,
            Height = 0.01f,
            RadialSegments = 16
        };

        var y = SampleSurfaceY(pos);
        m.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(pos.X, y + 0.005f, pos.Z));

        var mat = new StandardMaterial3D { AlbedoColor = col };
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (DebugPathOnTop) mat.NoDepthTest = true;
        m.SetSurfaceOverrideMaterial(0, mat);

        _pathsParent.AddChild(m);
    }
}