using Godot;
using System;
using System.Collections.Generic; // List<T>
using RSCore; // CylinderObstacle etc. (if not already present)

public partial class VehicleAgent3D : CharacterBody3D
{
    [Export] public float SpeedMps = 1.5f;
    [Export] public float ArenaRadius = 10.0f;   // meters
    [Export] public float TurnSmoothing = 8.0f;  // lerp gain

    [Export] public float PosStopEps = 0.05f;   // meters
    [Export] public float YawStopEpsDeg = 3.0f; // degrees
    [Export(PropertyHint.Range, "0.0,5.0,0.1")]
    private float obstacleBuffer = .5f;

    private Vector3[] _path = Array.Empty<Vector3>();
    private int _i = 0;
    private bool _done = true;
    


    public event Action<Vector3[]> PathUpdated;

    public void SetPath(Vector3[] pts)
    {
        _path = pts ?? Array.Empty<Vector3>();
        _i = 0;
        _done = _path.Length == 0;

        CheckPathForIntersections();

        // notify listeners (Main3D)
        PathUpdated?.Invoke(_path);
    }

    public override void _Ready()
    {
        var p = GlobalTransform.Origin;
        p.Y = 0f;
        GlobalTransform = new Transform3D(GlobalTransform.Basis, p);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done)
        {
            // keep slerping toward final tangent a few frames so we end nicely
            if (_path.Length >= 2)
            {
                var finalTan = (_path[^1] - _path[^2]).Normalized();
                var desiredEnd = Basis.LookingAt(new Vector3(finalTan.X, 0, finalTan.Z), Vector3.Up);
                var slerpedEnd = GlobalTransform.Basis.Slerp(desiredEnd, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));
                GlobalTransform = new Transform3D(slerpedEnd, GlobalTransform.Origin);
            }
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        var cur = GlobalTransform.Origin;
        var tgt = _path[_i]; tgt.Y = 0f;

        // tighten threshold a bit for your new scale
        if (cur.DistanceTo(tgt) < 0.12f)
        {
            _i++;
            if (_i >= _path.Length)
            {
                // clamp to the last point; stop translating, finish yaw alignment
                _i = _path.Length - 1;
                tgt = _path[_i];

                var finalPos = tgt;
                var finalTan = (_path[^1] - _path[^2]); finalTan.Y = 0f;
                finalTan = finalTan.LengthSquared() > 1e-9f ? finalTan.Normalized() : -GlobalTransform.Basis.Z;

                var posErr = new Vector2(finalPos.X - cur.X, finalPos.Z - cur.Z).Length();
                var forward = -GlobalTransform.Basis.Z; forward.Y = 0f; forward = forward.Normalized();
                var dot = Mathf.Clamp(forward.Dot(finalTan), -1f, 1f);
                var yawErrDeg = Mathf.RadToDeg(Mathf.Acos(dot));

                // freeze translation, keep rotating toward finalTan below
                Velocity = Vector3.Zero;
                MoveAndSlide();

                if (posErr <= PosStopEps && yawErrDeg <= YawStopEpsDeg)
                    _done = true;

                // we handled this tick
                return;
            }
            tgt = _path[_i];
        }

        var dir = (tgt - cur); dir.Y = 0f;
        if (dir.LengthSquared() < 1e-6f)
            dir = -GlobalTransform.Basis.Z; // keep heading
        else
            dir = dir.Normalized();

        Velocity = dir * SpeedMps;
        MoveAndSlide();

        // Smoothly face along the path tangent (even if not moving)
        Vector3 aimDir = dir;
        if (_i >= _path.Length - 1 && _path.Length >= 2)
        {
            var finalTan = (_path[^1] - _path[^2]); finalTan.Y = 0f;
            if (finalTan.LengthSquared() > 1e-9f) aimDir = finalTan.Normalized();
        }
        var desiredYaw = Basis.LookingAt(aimDir, Vector3.Up);
        var slerpedYaw = GlobalTransform.Basis.Slerp(desiredYaw, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));
        GlobalTransform = new Transform3D(slerpedYaw, GlobalTransform.Origin);

        // Plane lock + soft boundary
        var pos = GlobalTransform.Origin;
        pos.Y = 0f;
        if (pos.Length() > ArenaRadius - 0.5f)
            pos = pos.Normalized() * (ArenaRadius - 0.5f);
        GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
    }

    // Small plain-data obstacle record to avoid holding Godot objects
private struct Obstacle2D
{
    public Vector2 Center;
    public float Radius;
    public Obstacle2D(Vector2 c, float r) { Center = c; Radius = r; }
}

private void CheckPathForIntersections()
{
    if (_path == null || _path.Length < 2)
        return;

    // Collect obstacle data once (avoid storing Node references)
    var nodes = GetTree().GetNodesInGroup("Obstacles");
    var obsData = new List<Obstacle2D>(nodes.Count);
    foreach (var node in nodes)
    {
        if (node is RSCore.CylinderObstacle c && GodotObject.IsInstanceValid(c))
        {
            // Safely copy the XZ center and radius into a pure-data struct
            obsData.Add(new Obstacle2D(new Vector2(c.GlobalPosition.X, c.GlobalPosition.Z), c.Radius));
        }
    }

    if (obsData.Count == 0)
        return;

    // Quick check: does current path intersect any obstacle?
    if (!PathIntersectsObstacleData(_path, obsData))
        return;

    GD.Print("⚠️ RS path intersects obstacle — attempting replanning (safe-copy mode).");

    Vector3 start = _path[0];
    Vector3 goal = _path[^1];

    // A* / grid params (tweak as needed)
    float gridSize = 0.25f;
    int gridExtent = 60;
    float buffer = obstacleBuffer;


    var obstaclesRaw = GetTree().GetNodesInGroup("Obstacles");
var obsList = new List<RSCore.CylinderObstacle>();

for (int i = 0; i < obstaclesRaw.Count; i++)
{
    var node = obstaclesRaw[i];
    if (node is RSCore.CylinderObstacle c && GodotObject.IsInstanceValid(c))
        obsList.Add(c);
}

var astarPath = GridPlanner.Plan2DPath(start, goal, obsList, gridSize, gridExtent, buffer);


    if (astarPath == null || astarPath.Count < 3)
    {
        GD.PrintErr("❌ A* failed to produce a usable path.");
        return;
    }

    // Search attempts bounded to avoid hangs
    int maxAttempts = 200;
    int attempts = 0;
    double turnRadiusMeters = 1.0;
    double sampleStepMeters = 0.25;

    List<Vector3> mergedResult = null;

    // Try A* nodes as midpoints (center-first ordering)
    int n = astarPath.Count;
    int center = n / 2;
    var tried = new HashSet<int>();

    for (int offset = 0; offset <= n && attempts < maxAttempts; offset++)
    {
        int[] candidates = new int[] { center - offset, center + offset };
        foreach (var idx in candidates)
        {
            if (idx <= 0 || idx >= n - 1) continue;
            if (tried.Contains(idx)) continue;
            tried.Add(idx);

            var mid = astarPath[idx];

// Keep the same start and final yaw as the original RS path
double startYaw = 0.0;
double goalYaw = 0.0;

// Estimate start yaw from the original path direction
if (_path.Length >= 2)
{
    Vector3 startDir = _path[1] - _path[0];
    startDir.Y = 0f;
    if (startDir.LengthSquared() > 1e-9)
        startYaw = Math.Atan2(startDir.Z, startDir.X);
}

// Estimate goal yaw from the original final path direction
if (_path.Length >= 2)
{
    Vector3 goalDir = _path[_path.Length - 1] - _path[_path.Length - 2];
    goalDir.Y = 0f;
    if (goalDir.LengthSquared() > 1e-9)
        goalYaw = Math.Atan2(goalDir.Z, goalDir.X);
}

// Compute mid yaw based on direction from start to midpoint
Vector3 toMidV = mid - start;
toMidV.Y = 0f;
double midYaw = toMidV.LengthSquared() > 1e-9 ? Math.Atan2(toMidV.Z, toMidV.X) : startYaw;


            // compute RS segments in try/catch to avoid unexpected exceptions propagating
            Vector3[] rs1 = Array.Empty<Vector3>(), rs2 = Array.Empty<Vector3>();
            try
            {
                rs1 = RSAdapter.ComputePath3D(start, startYaw, mid, midYaw, turnRadiusMeters, sampleStepMeters);
                rs2 = RSAdapter.ComputePath3D(mid, midYaw, goal, goalYaw, turnRadiusMeters, sampleStepMeters);
            }
            catch (Exception e)
            {
                GD.PrintErr($"RSAdapter exception: {e.Message}");
                attempts += 2;
                continue;
            }

            attempts += 2;
            if (rs1 == null || rs2 == null || rs1.Length == 0 || rs2.Length == 0)
                continue;

            // Test collisions using pure-data obsData (no Godot calls)
            bool coll1 = PathIntersectsObstacleData(rs1, obsData);
            bool coll2 = PathIntersectsObstacleData(rs2, obsData);

            GD.Print($"  try idx={idx} attempts={attempts} coll1={coll1} coll2={coll2}");

            if (!coll1 && !coll2)
            {
                // merge rs1 and rs2 without duplicating mid
                mergedResult = new List<Vector3>(rs1);
                if (rs2.Length > 0)
                {
                    if (mergedResult.Count > 0 && mergedResult[mergedResult.Count - 1].DistanceTo(rs2[0]) < 1e-6f)
                    {
                        for (int k = 1; k < rs2.Length; k++) mergedResult.Add(rs2[k]);
                    }
                    else mergedResult.AddRange(rs2);
                }
                break;
            }

            if (attempts >= maxAttempts) break;
        }
        if (mergedResult != null || attempts >= maxAttempts) break;
    }

    if (mergedResult == null)
    {
        GD.PrintErr("❌ Could not find collision-free RS path after bounded attempts; leaving path unchanged.");
        return;
    }

    // Commit new path (fast, plain-array assignment)
    _path = mergedResult.ToArray();
    _i = 0;
    _done = _path.Length == 0;

    GD.Print($"✅ Replanning succeeded — new RS path has {_path.Length} points (attempts={attempts}).");
}

// Helper: uses only plain data (Vector2/float) — safe, no Godot calls inside loops
private bool PathIntersectsObstacleData(IList<Vector3> pathPoints, List<Obstacle2D> obstacles)
{
    if (pathPoints == null || pathPoints.Count == 0 || obstacles == null || obstacles.Count == 0)
        return false;

    for (int pi = 0; pi < pathPoints.Count; pi++)
    {
        var p = pathPoints[pi];
        float px = p.X;
        float pz = p.Z;

        // iterate obstacles
        for (int oi = 0; oi < obstacles.Count; oi++)
        {
            var o = obstacles[oi];
            var dx = px - o.Center.X;
            var dz = pz - o.Center.Y;
            // squared distance avoid sqrt
            var dist2 = dx * dx + dz * dz;
            var r = o.Radius + 0.5f; // safety buffer
            if (dist2 < r * r) return true;
        }
    }
    return false;
}


}