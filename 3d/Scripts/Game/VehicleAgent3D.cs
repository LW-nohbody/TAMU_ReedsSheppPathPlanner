using Godot;
using System;

public partial class VehicleAgent3D : CharacterBody3D
{
    [Export] public float SpeedMps = 1.5f;
    [Export] public float ArenaRadius = 10.0f;   // meters
    [Export] public float TurnSmoothing = 8.0f;  // lerp gain

    [Export] public float PosStopEps = 0.05f;   // meters
    [Export] public float YawStopEpsDeg = 3.0f; // degrees

    private Vector3[] _path = Array.Empty<Vector3>();
    private int _i = 0;
    private bool _done = true;

    public void SetPath(Vector3[] pts)
    {
        _path = pts ?? Array.Empty<Vector3>();
        _i = 0;
        _done = _path.Length == 0;
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
}