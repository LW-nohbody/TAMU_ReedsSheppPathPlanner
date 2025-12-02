using Godot;
using System;
using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    public partial class VehicleVisualizer : CharacterBody3D
    {
        // === Movement / path-follow parameters ===

        [Export] public float SpeedMps = 0.6f;
        [Export] public float ArenaRadius = 15.0f;

        [Export] public float TurnSmoothing = 8.0f;   // yaw smoothing
        [Export] public float TiltSmoothing = 8.0f;   // pitch/roll smoothing

        // Ground follow
        [Export] public float RideHeightFollow = 0.25f;
        [Export] public float Wheelbase = 2.0f;
        [Export] public float TrackWidth = 1.2f;
        [Export] public bool EnableTilt = true;

        [Export(PropertyHint.Range, "0.1,10,0.1")]
        public float TurnRadiusMeters = 1.0f;

        public VehicleSpec Spec { get; private set; }

        // === Path state ===

        private Vector3[] _path = Array.Empty<Vector3>();
        private int[] _gears = Array.Empty<int>();
        private int _i = 0;
        private bool _done = true;

        // Final pose cache (XZ + facing) for smooth landing
        private Vector3 _finalPosXZ = Vector3.Zero;  // last waypoint XZ
        private Vector3 _finalAimXZ = Vector3.Zero;  // unit XZ forward to settle to

        // Landing smoothing
        [Export] public float EndPosSmoothing = 12.0f;   // higher = faster settle to final point
        [Export] public float EndYawSmoothing = 10.0f;   // higher = faster settle to final heading

        // External terrain sampler
        private TerrainDisk _terrain = null!;
        public void SetTerrain(TerrainDisk t) => _terrain = t;

        // Name for nameplate & UI
        public string _vehicleID = null!;

        // Sim pause flag (controlled by SimulationDirector)
        private bool _simPaused = false;
        public bool SimPaused
        {
            get => _simPaused;
            set => _simPaused = value;
        }

        // =======================================================================
        // Public API
        // =======================================================================

        public void SetPath(Vector3[] pts, int[] gears)
        {
            _path = pts ?? Array.Empty<Vector3>();
            _gears = gears ?? Array.Empty<int>();
            _i = 0;
            _done = _path.Length == 0;

            // Smooth landing setup: cache last XZ position and reset final aim
            _finalPosXZ = (_path.Length > 0) ? _path[^1].WithY(0) : Vector3.Zero;
            _finalAimXZ = Vector3.Zero;

            // If needed later, keep these around for debugging:
            // GD.Print($"[{Name}] SetPath: len={_path.Length}, gears={_gears.Length}, done={_done}");
            // for (int k = 0; k < _path.Length; ++k)
            //     GD.Print($"   pt[{k}] = {_path[k]}  gear={(k < _gears.Length ? _gears[k] : +1)}");
        }

        public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());

        public override void _Ready()
        {
            // Flatten initial pose to XZ plane (Y comes from terrain)
            var p = GlobalTransform.Origin;
            p.Y = 0f;
            GlobalTransform = new Transform3D(GlobalTransform.Basis.Orthonormalized(), p);

            Spec = new VehicleSpec
            {
                TurnRadius = TurnRadiusMeters,  // uses editor-exposed value
                MaxSpeed = SpeedMps
            };

            // GD.Print($"[{Name}] Ready. movement=ON");
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_simPaused)
                return;

            float dt = (float)delta;

            // When path is done, just smooth toward final pose
            if (_done)
            {
                // Smooth landing to final XZ + heading
                var curXZDone = GlobalTransform.Origin;
                curXZDone.Y = 0f;

                // Exponential blend for position toward the exact final XZ
                float ap = 1f - Mathf.Exp(-EndPosSmoothing * dt);
                var landXZ = curXZDone.Lerp(_finalPosXZ, ap);

                // Exponential blend for facing toward cached final tangent (if known)
                var fwdNow = (-GlobalTransform.Basis.Z).WithY(0).Normalized();
                Vector3 aimTarget = (_finalAimXZ.LengthSquared() > 1e-9f) ? _finalAimXZ : fwdNow;
                float ay = 1f - Mathf.Exp(-EndYawSmoothing * dt);
                var fwdBlend = (fwdNow * (1f - ay) + aimTarget * ay).Normalized();

                // Pose at blended XZ and blended facing (GroundFollowAt sets height / tilt)
                GroundFollowAt(landXZ, fwdBlend, dt);

                // Currently we do not auto-switch out of this state; vehicle just rests there.
                return;
            }

            // === Active path-follow ===

            var curXZ = GlobalTransform.Origin;
            curXZ.Y = 0f;

            var tgt = _path[_i];
            tgt.Y = 0f;

            if (curXZ.DistanceTo(tgt) < 0.12f)
            {
                // GD.Print($"[{Name}] Reached wp[{_i}] @ {_path[_i]}");
                _i++;

                if (_i >= _path.Length)
                {
                    _i = _path.Length - 1;

                    // Determine final tangent from last segment
                    Vector3 finalTan = Vector3.Zero;
                    if (_path.Length >= 2)
                    {
                        finalTan = (_path[^1] - _path[^2]).WithY(0);
                        if (finalTan.LengthSquared() > 1e-9f)
                            finalTan = finalTan.Normalized();
                    }

                    if (finalTan.LengthSquared() < 1e-9f)
                        finalTan = (-GlobalTransform.Basis.Z).WithY(0).Normalized();

                    // Respect final gear (forward vs reverse)
                    int lastGear = (_gears.Length > 0) ? _gears[^1] : +1;
                    _finalAimXZ = (lastGear >= 0) ? finalTan : -finalTan;

                    // Ensure final XZ is the true last point (defensive)
                    _finalPosXZ = _path[^1].WithY(0);

                    // Enter smooth landing on next frame
                    _done = true;
                    return;
                }

                tgt = _path[_i];
            }

            // Direction to current waypoint in XZ
            var dir = (tgt - curXZ).WithY(0);
            dir = (dir.LengthSquared() < 1e-6f)
                ? (-GlobalTransform.Basis.Z).WithY(0)
                : dir.Normalized();

            // Manual integration (no physics)
            var nextXZ = curXZ + dir * SpeedMps * dt;

            // Soft arena clamp
            float maxR = Mathf.Max(0.1f, ArenaRadius - 0.25f);
            if (nextXZ.Length() > maxR)
                nextXZ = nextXZ.Normalized() * maxR;

            // Smooth yaw toward tangent (respect gear)
            Vector3 aimDir = dir;
            if (_i >= _path.Length - 1 && _path.Length >= 2)
            {
                var finalTan = (_path[^1] - _path[^2]).WithY(0);
                if (finalTan.LengthSquared() > 1e-9f)
                    aimDir = finalTan.Normalized();
            }

            int gear = (_i < _gears.Length) ? _gears[_i] : +1;
            var facingDir = (gear >= 0) ? aimDir : -aimDir;

            var desiredYawMove = Basis.LookingAt(facingDir, Vector3.Up).Orthonormalized();
            float ayawMove = 1f - Mathf.Exp(-TurnSmoothing * dt);
            var yawBlendedMove = SafeSlerp(GlobalTransform.Basis, desiredYawMove, ayawMove);

            GlobalTransform = new Transform3D(
                yawBlendedMove,
                new Vector3(nextXZ.X, 0f, nextXZ.Z));

            // Stick to ground at the NEW XZ, blending pitch/roll
            GroundFollowAt(nextXZ, facingDir, dt);
        }

        // =======================================================================
        // Ground follow
        // =======================================================================

        // GroundFollowAt: sample terrain at center XZ and apply height / tilt
        private void GroundFollowAt(Vector3 centerXZ, Vector3 facingDir, float dt)
        {
            if (_terrain == null)
                return;
            if (!_terrain.SampleHeightNormal(centerXZ, out var hC, out var nC))
                return;

            // Always pin height
            Vector3 pos = hC + Vector3.Up * RideHeightFollow;

            if (!EnableTilt)
            {
                // Yaw already blended this frame; keep basis, set only Y
                GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
                return;
            }

            // Tilt enabled: build stable frame using ground normal + projected facing
            Vector3 fwd = facingDir - nC * facingDir.Dot(nC);
            if (fwd.LengthSquared() < 1e-6f)
                fwd = facingDir.WithY(0);
            if (fwd.LengthSquared() < 1e-6f)
                fwd = Vector3.Forward;

            fwd = fwd.Normalized();

            Vector3 right = fwd.Cross(nC).Normalized();
            Vector3 zAxis = -fwd;                    // Godot forward is -Z

            var desiredBasis = new Basis(right, nC, zAxis).Orthonormalized();

            float a = 1f - Mathf.Exp(-TiltSmoothing * dt);
            var blended = SafeSlerp(GlobalTransform.Basis, desiredBasis, a);

            GlobalTransform = new Transform3D(blended, pos);
        }

        // =======================================================================
        // Basis / quaternion helpers
        // =======================================================================

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

            if (!q.IsNormalized() || float.IsNaN(q.W))
                q = q0;

            return new Basis(q).Orthonormalized();
        }
    }

    static class Vec3Ext
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.X, y, v.Z);
    }
}