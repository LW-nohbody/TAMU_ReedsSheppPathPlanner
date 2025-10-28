using Godot;
using System;

namespace DigSim3D.App
{
    public partial class VehicleVisualizer : CharacterBody3D
    {
        [Export] public float SpeedMps = 1.0f;
        [Export] public float ArenaRadius = 15.0f;
        [Export] public float TurnSmoothing = 8.0f;      // yaw smoothing while moving
        [Export] public float TiltSmoothing = 8.0f;      // pitch/roll smoothing
        [Export] public float YawStopEpsDeg = 3.0f;

        // Landing (smooth instead of snap)
        [Export] public float EndPosSmoothing = 12.0f;   // higher = faster settle to final point
        [Export] public float EndYawSmoothing = 10.0f;   // higher = faster settle to final heading
        [Export] public float EndStopPosEps = 0.01f;     // meters to consider “arrived”

        // Ground follow
        [Export] public float RideHeightFollow = 0.25f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float NormalBlendFollow = 0.2f;
        [Export] public float Wheelbase = 2.0f;
        [Export] public float TrackWidth = 1.2f;
        [Export] public bool EnableTilt = true;

        // Path
        private Vector3[] _path = Array.Empty<Vector3>();
        private int[] _gears = Array.Empty<int>();
        private int _i = 0;
        private bool _done = true;

        // Final pose cache (XZ + facing)
        private Vector3 _finalPosXZ = Vector3.Zero;  // last waypoint XZ
        private Vector3 _finalAimXZ = Vector3.Zero;  // unit XZ forward to settle to

        // External terrain sampler
        private TerrainDisk _terrain = null!;
        public void SetTerrain(TerrainDisk t) => _terrain = t;

        public void SetPath(Vector3[] pts, int[] gears)
        {
            _path = pts ?? Array.Empty<Vector3>();
            _gears = gears ?? Array.Empty<int>();

            _i = 0;
            _done = _path.Length == 0;

            _finalPosXZ = (_path.Length > 0) ? _path[^1].WithY(0) : Vector3.Zero;
            _finalAimXZ = Vector3.Zero; // will be computed when we actually finish

            GD.Print($"[{Name}] SetPath: len={_path.Length}, gears={_gears.Length}, done={_done}");
        }
        public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());

        public override void _Ready()
        {
            var p = GlobalTransform.Origin; p.Y = 0f;
            GlobalTransform = new Transform3D(GlobalTransform.Basis.Orthonormalized(), p);
            GD.Print($"[{Name}] Ready. movement=ON");
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_done)
            {
                // --- Smooth “landing” to final pose ---
                var curXZ = GlobalTransform.Origin; curXZ.Y = 0f;

                // Exponential blend for position
                float ap = 1f - Mathf.Exp(-EndPosSmoothing * dt);
                var landXZ = curXZ.Lerp(_finalPosXZ, ap);

                // Exponential blend for facing toward final tangent (if known)
                var fwdNow = (-GlobalTransform.Basis.Z).WithY(0).Normalized();
                Vector3 aimTarget = (_finalAimXZ.LengthSquared() > 1e-9f) ? _finalAimXZ : fwdNow;
                float ay = 1f - Mathf.Exp(-EndYawSmoothing * dt);
                var fwdBlend = (fwdNow * (1f - ay) + aimTarget * ay).Normalized();

                // Build pose at blended XZ and blended facing (GroundFollowAt handles tilt & height)
                GroundFollowAt(landXZ, fwdBlend, dt);

                // Stop conditions: close enough in position and yaw
                var posErr = landXZ.DistanceTo(_finalPosXZ);
                float yawErrDeg = 0f;
                {
                    var fNow = (-GlobalTransform.Basis.Z).WithY(0).Normalized();
                    var fAim = (_finalAimXZ.LengthSquared() > 1e-9f) ? _finalAimXZ : fNow;
                    float d = Mathf.Clamp(fNow.Dot(fAim), -1f, 1f);
                    yawErrDeg = Mathf.RadToDeg(Mathf.Acos(d));
                }

                if (posErr <= EndStopPosEps && yawErrDeg <= YawStopEpsDeg)
                {
                    // Pin exactly and finish
                    var gt = GlobalTransform;
                    gt.Origin = new Vector3(_finalPosXZ.X, gt.Origin.Y, _finalPosXZ.Z);
                    GlobalTransform = gt;
                }
                return;
            }

            // == Follow: step toward current waypoint in XZ ==
            var curXZ2 = GlobalTransform.Origin; curXZ2.Y = 0f;
            var tgt = _path[_i]; tgt.Y = 0f;

            if (curXZ2.DistanceTo(tgt) < 0.12f)
            {
                _i++;
                if (_i >= _path.Length)
                {
                    _i = _path.Length - 1;

                    // Compute final forward from last segment (respect last gear) for landing
                    Vector3 finalTan = Vector3.Zero;
                    if (_path.Length >= 2)
                    {
                        finalTan = (_path[^1] - _path[^2]).WithY(0);
                        if (finalTan.LengthSquared() > 1e-9f) finalTan = finalTan.Normalized();
                    }
                    if (finalTan.LengthSquared() < 1e-9f)
                        finalTan = (-GlobalTransform.Basis.Z).WithY(0).Normalized();

                    int lastGear = (_gears.Length > 0) ? _gears[^1] : +1;
                    _finalAimXZ = (lastGear >= 0) ? finalTan : -finalTan;

                    _done = true;   // enter landing next frame
                    return;
                }
                tgt = _path[_i];
            }

            // Direction to current waypoint
            var dir = (tgt - curXZ2).WithY(0);
            dir = (dir.LengthSquared() < 1e-6f) ? (-GlobalTransform.Basis.Z).WithY(0) : dir.Normalized();

            // Manual integration (no physics)
            var nextXZ = curXZ2 + dir * SpeedMps * dt;

            // Soft arena clamp
            float maxR = Mathf.Max(0.1f, ArenaRadius - 0.25f);
            if (nextXZ.Length() > maxR) nextXZ = nextXZ.Normalized() * maxR;

            // Smooth yaw toward motion dir (respect gear)
            Vector3 aimDir = dir;
            if (_i >= _path.Length - 1 && _path.Length >= 2)
            {
                var finalTan = (_path[^1] - _path[^2]).WithY(0);
                if (finalTan.LengthSquared() > 1e-9f) aimDir = finalTan.Normalized();
            }
            int gear = (_i < _gears.Length) ? _gears[_i] : +1;
            var facingDir = (gear >= 0) ? aimDir : -aimDir;

            var desiredYaw = Basis.LookingAt(facingDir, Vector3.Up).Orthonormalized();
            float ayaw = 1f - Mathf.Exp(-TurnSmoothing * dt);
            var yawBlended = SafeSlerp(GlobalTransform.Basis, desiredYaw, ayaw);
            GlobalTransform = new Transform3D(yawBlended, new Vector3(nextXZ.X, 0f, nextXZ.Z));

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
                GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
                return;
            }

            // Project facing onto ground to build a stable frame
            Vector3 fwd = facingDir - nC * facingDir.Dot(nC);
            if (fwd.LengthSquared() < 1e-6f) fwd = facingDir.WithY(0);
            if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.Forward;
            fwd = fwd.Normalized();

            Vector3 right = fwd.Cross(nC).Normalized();
            Vector3 zAxis = -fwd;  // Godot forward is -Z
            var desiredBasis = new Basis(right, nC, zAxis).Orthonormalized();

            float a = 1f - Mathf.Exp(-TiltSmoothing * dt);
            var blended = SafeSlerp(GlobalTransform.Basis, desiredBasis, a);
            GlobalTransform = new Transform3D(blended, pos);
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
    }

    static class Vec3Ext
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.X, y, v.Z);
    }
}