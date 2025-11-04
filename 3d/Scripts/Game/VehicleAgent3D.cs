using Godot;
using System;
using SimCore.Core;


public partial class VehicleAgent3D : CharacterBody3D
{
    // Global speed multiplier (configurable from UI)
    public static float GlobalSpeedMultiplier = 1.0f;

    [Export] public float SpeedMps = 0.6f;
    [Export] public float ArenaRadius = 15.0f;
    [Export] public float TurnSmoothing = 8.0f;   // yaw smoothing
    [Export] public float TiltSmoothing = 8.0f;   // pitch/roll smoothing
    [Export] public float YawStopEpsDeg = 3.0f;

    // Ground follow
    [Export] public float RideHeightFollow = 0.25f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float NormalBlendFollow = 0.2f;
    [Export] public float Wheelbase = 2.0f;
    [Export] public float TrackWidth = 1.2f;
    [Export] public bool EnableTilt = true;

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float TurnRadiusMeters = 1.0f;
    public VehicleSpec Spec { get; private set; }


    // Path
    private Vector3[] _path = Array.Empty<Vector3>();
    private int[] _gears = Array.Empty<int>();
    private int _i = 0;
    private bool _done = true;

    // Slice limits: optional angular sector (radians) and max radius to constrain vehicle
    private bool _hasSliceLimits = false;
    private float _sliceThetaMin = 0f;
    private float _sliceThetaMax = 0f;
    private float _sliceMaxRadius = 100f;

    public void SetSliceLimits(float thetaMin, float thetaMax, float maxRadius)
    {
        _hasSliceLimits = true;
        _sliceThetaMin = NormalizeAngle(thetaMin);
        _sliceThetaMax = NormalizeAngle(thetaMax);
        _sliceMaxRadius = MathF.Max(0f, maxRadius);
    }

    // Allow external callers to query slice limits
    public bool TryGetSliceLimits(out float thetaMin, out float thetaMax, out float maxRadius)
    {
        thetaMin = _sliceThetaMin; thetaMax = _sliceThetaMax; maxRadius = _sliceMaxRadius;
        return _hasSliceLimits;
    }
    private static float NormalizeAngle(float a)
    {
        float twoPi = Mathf.Tau;
        a %= twoPi; if (a < 0) a += twoPi; return a;
    }
    // Check if angle t is between [a,b] circularly (inclusive)
    private static bool AngleBetween(float t, float a, float b)
    {
        if (a <= b) return t >= a && t <= b;
        // wrapped interval
        return t >= a || t <= b;
    }

    // End yaw align
    private bool _aligning = false;
    private Vector3 _finalAim = Vector3.Zero; // unit XZ
    private bool _holdYaw = false;          // lock azimuth when done
    private Vector3 _holdAimXZ = Vector3.Zero;
    // debug counter for align prints
    private int _alignPrintCounter = 0;

    // External terrain sampler
    private TerrainDisk _terrain;
    public void SetTerrain(TerrainDisk t) => _terrain = t;
    // Allow external callers to request lowering terrain at a world XZ
    public void LowerTerrainAt(Vector3 worldXZ, float radius, float deltaHeight)
    {
        if (_terrain == null) return;
        _terrain.LowerArea(worldXZ, radius, deltaHeight);
    }

    // Allow external callers to sample terrain height & normal at a world XZ
    public bool SampleTerrainHeight(Vector3 worldXZ, out Vector3 hitPos, out Vector3 normal)
    {
        if (_terrain == null) { hitPos = default; normal = Vector3.Up; return false; }
        return _terrain.SampleHeightNormal(worldXZ, out hitPos, out normal);
    }

    public void SetPath(Vector3[] pts, int[] gears)
    {
        _path = pts ?? Array.Empty<Vector3>();
        _gears = gears ?? Array.Empty<int>();
        _i = 0;
        _done = _path.Length == 0;
        _aligning = false;
        _finalAim = Vector3.Zero;

        // Debug prints commented out to reduce noise (as per main branch)
        //GD.Print($"[{Name}] SetPath: len={_path.Length}, gears={_gears.Length}, done={_done}");
        //for (int k = 0; k < _path.Length; ++k)
        //    GD.Print($"   pt[{k}] = {_path[k]}  gear={(k < _gears.Length ? _gears[k] : +1)}");

        // If controller was given an empty path, attempt a small local recovery if we're in a depression.
        if (_path.Length == 0)
        {
            // Sample local terrain heights to detect a pit (neighbors higher than current)
            var cur = GlobalTransform.Origin; cur.Y = 0f;
            if (SampleTerrainHeight(cur, out var hpCur, out var _))
            {
                float hCur = hpCur.Y;
                
                // Check 8 directions around current position
                float maxN = hCur;
                Vector3 bestEscapeDir = Vector3.Zero;
                var directions = new[]
                {
                    Vector3.Right, -Vector3.Right, 
                    Vector3.Forward, -Vector3.Forward,
                    (Vector3.Right + Vector3.Forward).Normalized(),
                    (Vector3.Right - Vector3.Forward).Normalized(),
                    (-Vector3.Right + Vector3.Forward).Normalized(),
                    (-Vector3.Right - Vector3.Forward).Normalized()
                };
                
                foreach (var dir in directions)
                {
                    var testPos = cur + dir * 0.7f;
                    if (SampleTerrainHeight(testPos, out var hp, out var _))
                    {
                        if (hp.Y > maxN)
                        {
                            maxN = hp.Y;
                            bestEscapeDir = dir;
                        }
                    }
                }
                
                // If neighbors are significantly higher, we're in a pit - escape toward highest neighbor
                float depthDiff = maxN - hCur;
                if (depthDiff > 0.15f)
                {
                    // Move toward the highest neighboring point
                    var escape1 = cur + bestEscapeDir * 0.8f;
                    var escape2 = cur + bestEscapeDir * 1.6f;
                    var recovery = new Vector3[] { escape1, escape2, cur };
                    _path = recovery;
                    _gears = new int[] { +1, +1, +1 };
                    _i = 0;
                    _done = false;
                    GD.Print($"[{Name}] LocalRecovery: climbing {depthDiff:F2}m toward {bestEscapeDir}, path={escape1},{escape2}");
                }
            }
        }
    }
    public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());

    public override void _Ready()
    {
        var p = GlobalTransform.Origin; p.Y = 0f;
        GlobalTransform = new Transform3D(GlobalTransform.Basis.Orthonormalized(), p);

        Spec = new VehicleSpec
        {
            TurnRadius = TurnRadiusMeters,  // now uses the editor-exposed value
            MaxSpeed = SpeedMps
        };


        //GD.Print($"[{Name}] Ready. movement=ON");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_aligning)
        {
            var xz = GlobalTransform.Origin; xz.Y = 0f;

            // Let the tilt/ground-follow logic build the target basis using the FINAL AIM.
            GroundFollowAt(xz, _finalAim, dt);

            // Check yaw error in XZ vs the final aim
            var fwdNow = (-GlobalTransform.Basis.Z).WithY(0).Normalized();
            float yawErrDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(fwdNow.Dot(_finalAim), -1f, 1f)));

            // Periodic debug print to help diagnose stuck alignment
            _alignPrintCounter++;
            if ((_alignPrintCounter % 10) == 0)
            {
                GD.Print($"[{Name}] Aligning... yawErrDeg={yawErrDeg:F2}deg finalAim={_finalAim}");
            }

            if (yawErrDeg <= YawStopEpsDeg)
            {
                _aligning = false;
                _done = true;
                _holdYaw = true;       // lock the azimuth going forward
                _holdAimXZ = _finalAim;
                //GD.Print($"[{Name}] Final align complete.");
            }
            return;
        }

        if (_done)
        {
            var xz = GlobalTransform.Origin; xz.Y = 0f;

            // If we’ve finished, preserve the cached azimuth and SNAP each frame.
            var fwd = _holdYaw ? _holdAimXZ : (-GlobalTransform.Basis.Z).WithY(0).Normalized();
            GroundFollowAt(xz, fwd, dt);
            return;
        }

        // == Old stable follow: step toward current waypoint in XZ ==
        var curXZ = GlobalTransform.Origin; curXZ.Y = 0f;
        var tgt = _path[_i]; tgt.Y = 0f;

        if (curXZ.DistanceTo(tgt) < 0.12f)
        {
            // GD.Print($"[{Name}] Reached wp[{_i}] @ {_path[_i]}");
            //GD.Print($"[{Name}] Reached wp[{_i}] @ {_path[_i]}");
            _i++;
            if (_i >= _path.Length)
            {
                _i = _path.Length - 1;

                // Determine final facing from last segment, respect final gear
                Vector3 finalTan = Vector3.Zero;
                if (_path.Length >= 2)
                {
                    finalTan = (_path[^1] - _path[^2]).WithY(0);
                    if (finalTan.LengthSquared() > 1e-9f) finalTan = finalTan.Normalized();
                }
                if (finalTan.LengthSquared() < 1e-9f) finalTan = (-GlobalTransform.Basis.Z).WithY(0).Normalized();

                int lastGear = (_gears.Length > 0) ? _gears[^1] : +1;
                _finalAim = (lastGear >= 0) ? finalTan : -finalTan;

                // --- START OF CHANGE: Force completion on final waypoint ---
                // To avoid vehicles getting stuck in final alignment and never setting _done=true,
                // we skip the align phase here and mark the agent as done so the director can
                // handle arrival/digging immediately. This was added as a pragmatic fix.
                _aligning = false;
                _done = true;
                _holdYaw = true;       // lock the azimuth going forward
                _holdAimXZ = _finalAim;
                GD.Print($"[{Name}] Final waypoint reached — forcing done (skipping align). finalAim={_finalAim} lastGear={lastGear}");
                // --- END OF CHANGE ---

                return;
            }
            tgt = _path[_i];
        }

        // Direction to current waypoint
        var dir = (tgt - curXZ).WithY(0);
        dir = (dir.LengthSquared() < 1e-6f) ? (-GlobalTransform.Basis.Z).WithY(0) : dir.Normalized();

        // Adaptive speed: slow down when close to waypoint to avoid overshooting
        float distToWaypoint = curXZ.DistanceTo(tgt);
        float speedMult = Mathf.Clamp(distToWaypoint / 0.5f, 0.3f, 1.0f); // Slow to 30% within 0.5m
        float effectiveSpeed = SpeedMps * speedMult * GlobalSpeedMultiplier;

        // Manual integration (no physics!)
        var nextXZ = curXZ + dir * effectiveSpeed * dt;

        // Soft arena clamp
        float maxR = Mathf.Max(0.1f, ArenaRadius - 0.25f);
        if (nextXZ.Length() > maxR) nextXZ = nextXZ.Normalized() * maxR;

        // Enforce optional slice limits (angular sector and radius)
        if (_hasSliceLimits)
        {
            // convert to polar
            float rx = nextXZ.Length();
            float ang = Mathf.Atan2(nextXZ.Z, nextXZ.X); if (ang < 0) ang += Mathf.Tau;
            // clamp radius
            rx = MathF.Min(rx, _sliceMaxRadius);
            // clamp angle within sector
            if (!AngleBetween(ang, _sliceThetaMin, _sliceThetaMax))
            {
                // project angle to nearest boundary
                // compute angular distance to both ends
                float dMin = AngularDistance(ang, _sliceThetaMin);
                float dMax = AngularDistance(ang, _sliceThetaMax);
                float newAng = (MathF.Abs(dMin) < MathF.Abs(dMax)) ? _sliceThetaMin : _sliceThetaMax;
                nextXZ = new Vector3(MathF.Cos(newAng) * rx, 0, MathF.Sin(newAng) * rx);
            }
            else
            {
                // within angular sector, apply clamped radius
                nextXZ = nextXZ.Normalized() * rx;
            }
        }

        // Smooth yaw toward tangent (respect gear)
        Vector3 aimDir = dir;
        if (_i >= _path.Length - 1 && _path.Length >= 2)
        {
            var finalTan = (_path[^1] - _path[^2]).WithY(0);
            if (finalTan.LengthSquared() > 1e-9f) aimDir = finalTan.Normalized();
        }
        int gear = (_i < _gears.Length) ? _gears[_i] : +1;
        var facingDir = (gear >= 0) ? aimDir : -aimDir;

        var desiredYawMove = Basis.LookingAt(facingDir, Vector3.Up).Orthonormalized();
        float ayawMove = 1f - Mathf.Exp(-TurnSmoothing * dt);
        var yawBlendedMove = SafeSlerp(GlobalTransform.Basis, desiredYawMove, ayawMove);
        GlobalTransform = new Transform3D(yawBlendedMove, new Vector3(nextXZ.X, 0f, nextXZ.Z));

        // Stick to ground at the NEW XZ, blending pitch/roll
        GroundFollowAt(nextXZ, facingDir, dt);
    }

    // --- Ground follow at a given XZ (blends pitch/roll, sets Y) ---------------
    private void GroundFollowAt(Vector3 centerXZ, Vector3 facingDir, float dt)
    {
        if (_terrain == null) return;
        if (!_terrain.SampleHeightNormal(centerXZ, out var hC, out var nC)) return;

        // Always pin height
        Vector3 pos = hC + Vector3.Up * RideHeightFollow;

        if (!EnableTilt)
        {
            // Yaw already blended this frame; keep basis, set only Y
            GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
            return;
        }

        // --- EnableTilt path (can turn on later) ---
        // Project facing onto ground to build a stable frame
        Vector3 fwd = facingDir - nC * facingDir.Dot(nC);
        if (fwd.LengthSquared() < 1e-6f) fwd = facingDir.WithY(0);
        if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.Forward;
        fwd = fwd.Normalized();

        Vector3 right = fwd.Cross(nC);
        right = right.Normalized();
        Vector3 zAxis = -fwd;                    // Godot forward is -Z
        var desiredBasis = new Basis(right, nC, zAxis).Orthonormalized();

        float a = 1f - Mathf.Exp(-TiltSmoothing * dt);
        var blended = SafeSlerp(GlobalTransform.Basis, desiredBasis, a);
        GlobalTransform = new Transform3D(blended, pos);
    }

    private static Vector3 PerpTo(Vector3 n)
    {
        return (Mathf.Abs(n.Y) < 0.99f ? n.Cross(Vector3.Up) : n.Cross(Vector3.Right)).Normalized();
    }

    private static Basis SafeSlerp(in Basis fromB, in Basis toB, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        var q0 = fromB.Orthonormalized().GetRotationQuaternion().Normalized();
        var q1 = toB.Orthonormalized().GetRotationQuaternion().Normalized();
        var q = q0.Slerp(q1, t).Normalized();
        if (!q.IsNormalized() || float.IsNaN(q.W)) q = q0;
        return new Basis(q).Orthonormalized();
    }

    private static float AngularDistance(float a, float b)
    {
        float d = a - b;
        while (d > Mathf.Pi) d -= Mathf.Tau;
        while (d < -Mathf.Pi) d += Mathf.Tau;
        return d;
    }

    // Public accessors to avoid reflection (for SimulationDirector)
    public bool IsDone 
    { 
        get 
        { 
            try 
            { 
                return GodotObject.IsInstanceValid(this) && _done; 
            } 
            catch 
            { 
                return false; 
            } 
        } 
    }
    
    public Vector3[] GetCurrentPath() 
    { 
        try 
        { 
            if (!GodotObject.IsInstanceValid(this)) return Array.Empty<Vector3>();
            return _path ?? Array.Empty<Vector3>(); 
        } 
        catch 
        { 
            return Array.Empty<Vector3>(); 
        } 
    }
    
    public int GetCurrentPathIndex() 
    { 
        try 
        { 
            if (!GodotObject.IsInstanceValid(this)) return 0;
            return _i; 
        } 
        catch 
        { 
            return 0; 
        } 
    }
    
    public int[] GetCurrentGears() 
    { 
        try 
        { 
            if (!GodotObject.IsInstanceValid(this)) return Array.Empty<int>();
            return _gears ?? Array.Empty<int>(); 
        } 
        catch 
        { 
            return Array.Empty<int>(); 
        } 
    }
}

static class Vec3Ext
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.X, y, v.Z);
}