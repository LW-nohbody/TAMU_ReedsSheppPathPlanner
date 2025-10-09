using Godot;
using System;

public partial class VehicleAgent3D : CharacterBody3D
{
    [Export] public float SpeedMps = 1.0f;
    [Export] public float ArenaRadius = 10.0f;   // meters
    [Export] public float TurnSmoothing = 8.0f;  // lerp gain

    [Export] public float PosStopEps = 0.05f;   // meters
    [Export] public float YawStopEpsDeg = 3.0f; // degrees

    private Vector3[] _path = Array.Empty<Vector3>();
    private int[] _gears = Array.Empty<int>();

    private int _i = 0;
    private bool _done = true;

    // NEW: end-alignment state
    private bool _aligning = false;
    private Vector3 _finalAim = Vector3.Zero; // unit XZ direction to face at the end

    public void SetPath(Vector3[] pts, int[] gears)
    {
        _path = pts ?? Array.Empty<Vector3>();
        _gears = gears ?? Array.Empty<int>();
        _i = 0;
        _done = _path.Length == 0;

        _aligning = false;
        _finalAim = Vector3.Zero;
    }

    public void SetPath(Vector3[] pts)
    {
        SetPath(pts, Array.Empty<int>());
    }

    public override void _Ready()
    {
        var p = GlobalTransform.Origin;
        p.Y = 0f;
        GlobalTransform = new Transform3D(GlobalTransform.Basis, p);
    }

    public override void _PhysicsProcess(double delta)
    {
        // 1) If we're in the end-alignment phase, only rotate smoothly until done.
        if (_aligning)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();

            // Slerp toward the cached final facing
            var desired = Basis.LookingAt(new Vector3(_finalAim.X, 0f, _finalAim.Z), Vector3.Up);
            var slerped = GlobalTransform.Basis.Slerp(
                desired, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));
            GlobalTransform = new Transform3D(slerped, GlobalTransform.Origin);

            // Check yaw error
            var fwd = -GlobalTransform.Basis.Z; fwd.Y = 0f; fwd = fwd.Normalized();
            float dot = Mathf.Clamp(fwd.Dot(_finalAim), -1f, 1f);
            float yawErrDeg = Mathf.RadToDeg(Mathf.Acos(dot));

            if (yawErrDeg <= YawStopEpsDeg)
            {
                // Final snap to exact facing, then done.
                GlobalTransform = new Transform3D(desired, GlobalTransform.Origin);
                _aligning = false;
                _done = true;
            }
            return;
        }

        // 2) If we're fully done (no aligning), just stop.
        if (_done)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        var cur = GlobalTransform.Origin;
        var tgt = _path[_i]; tgt.Y = 0f;

        // Advance waypoint when close
        if (cur.DistanceTo(tgt) < 0.12f)
        {
            _i++;
            if (_i >= _path.Length)
            {
                // We just reached/passed the last point -> start end-alignment (no more translation).
                _i = _path.Length - 1;

                // Compute final tangent from last two points
                Vector3 finalTan = Vector3.Zero;
                if (_path.Length >= 2)
                {
                    finalTan = _path[^1] - _path[^2];
                    finalTan.Y = 0f;
                }
                if (finalTan.LengthSquared() < 1e-9f)
                    finalTan = -GlobalTransform.Basis.Z; // fallback to current facing
                else
                    finalTan = finalTan.Normalized();

                // Respect the final gear for visual facing
                int lastGear = (_gears.Length > 0) ? _gears[^1] : +1;
                _finalAim = (lastGear >= 0) ? finalTan : -finalTan;

                Velocity = Vector3.Zero;
                MoveAndSlide();

                _aligning = true;   // hand control to the alignment block above
                return;
            }
            tgt = _path[_i];
        }

        // Move toward current waypoint
        var dir = (tgt - cur); dir.Y = 0f;
        if (dir.LengthSquared() < 1e-6f)
            dir = -GlobalTransform.Basis.Z; // keep heading if degenerate
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

        // Face forward for gear>0, backward for gear<0 (visual only)
        int gear = (_i < _gears.Length) ? _gears[_i] : +1;
        var facingDir = (gear >= 0) ? aimDir : -aimDir;

        var desiredYaw = Basis.LookingAt(facingDir, Vector3.Up);
        var slerpedYaw = GlobalTransform.Basis.Slerp(
            desiredYaw, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));
        GlobalTransform = new Transform3D(slerpedYaw, GlobalTransform.Origin);

        // Plane lock + soft boundary
        var pos = GlobalTransform.Origin;
        pos.Y = 0f;
        if (pos.Length() > ArenaRadius - 0.5f)
            pos = pos.Normalized() * (ArenaRadius - 0.5f);
        GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
    }
}