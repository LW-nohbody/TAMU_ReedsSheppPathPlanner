using Godot;
using System;

public partial class VehicleAgent3D : CharacterBody3D
{
    [Export] public float ArenaRadius = 15.0f;
    [Export] public float SpeedMps = 1.0f;
    [Export] public float TurnSmoothing = 8.0f;  // yaw slerp gain
    [Export] public float YawStopEpsDeg = 3.0f;  // end alignment tolerance (deg)
    [Export] public bool MovementEnabled = true;

    [Signal] public delegate void PathFinishedEventHandler();

    private Vector3[] _path = Array.Empty<Vector3>();
    private int[] _gears = Array.Empty<int>();

    private int _i = 0;
    private bool _done = true;

    // Keep the prefab’s authored Y so nothing can push us down.
    private float _baseY = 0f;

    // End-alignment (rotate-only)
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

    public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());

    public override void _Ready()
    {
        // Preserve the scene-authored height; don’t zero it.
        _baseY = GlobalTransform.Origin.Y;

        // Ensure no auto floor snapping interferes.
        FloorSnapLength = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!MovementEnabled)
        {
            Velocity = Vector3.Zero;
            return;
        }

        // --- Rotate-only end alignment ---------------------------------------
        if (_aligning)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();

            // Slerp yaw toward cached final facing (XZ)
            var desired = Basis.LookingAt(new Vector3(_finalAim.X, 0f, _finalAim.Z), Vector3.Up);
            var slerped = GlobalTransform.Basis.Slerp(desired, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));
            var pos = GlobalTransform.Origin; pos.Y = _baseY;          // keep height pinned
            GlobalTransform = new Transform3D(slerped, pos);

            // Check yaw error
            var fwd = -GlobalTransform.Basis.Z; fwd.Y = 0f; fwd = fwd.Normalized();
            float dot = Mathf.Clamp(fwd.Dot(_finalAim), -1f, 1f);
            float yawErrDeg = Mathf.RadToDeg(Mathf.Acos(dot));

            if (yawErrDeg <= YawStopEpsDeg)
            {
                // Final snap to exact facing, still at prefab height
                GlobalTransform = new Transform3D(desired, pos);
                _aligning = false;
                _done = true;

                EmitSignal(SignalName.PathFinished);   // tell the brain we’re done
            }
            return;
        }
        // ---------------------------------------------------------------------

        // Fully done: just stay put at prefab height.
        if (_done)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            var stopPos = GlobalTransform.Origin; stopPos.Y = _baseY;
            GlobalTransform = new Transform3D(GlobalTransform.Basis, stopPos);
            return;
        }

        // Current pos & target (we only care about XZ for navigation)
        var cur = GlobalTransform.Origin;
        var tgt = _path[_i];

        // Advance on XZ distance only
        float distXZ = new Vector2(cur.X - tgt.X, cur.Z - tgt.Z).Length();
        if (distXZ < 0.12f)
        {
            _i++;
            if (_i >= _path.Length)
            {
                // Enter rotate-only end alignment
                _i = _path.Length - 1;

                // Final tangent from last two points
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

                // Respect final gear for visual facing
                int lastGear = (_gears.Length > 0) ? _gears[^1] : +1;
                _finalAim = (lastGear >= 0) ? finalTan : -finalTan;

                Velocity = Vector3.Zero;
                MoveAndSlide();

                // Pin height and switch to alignment mode
                var pos = GlobalTransform.Origin; pos.Y = _baseY;
                GlobalTransform = new Transform3D(GlobalTransform.Basis, pos);
                _aligning = true;
                return;
            }
            tgt = _path[_i];
        }

        // Move toward current waypoint (horizontal only)
        var moveDir = new Vector3(tgt.X - cur.X, 0f, tgt.Z - cur.Z);
        if (moveDir.LengthSquared() < 1e-6f)
            moveDir = -GlobalTransform.Basis.Z;
        else
            moveDir = moveDir.Normalized();

        Velocity = moveDir * SpeedMps;
        MoveAndSlide();

        // Smoothly face along the path tangent during traversal
        Vector3 aimDir = moveDir;
        if (_i >= _path.Length - 1 && _path.Length >= 2)
        {
            var finalTan = _path[^1] - _path[^2];
            finalTan.Y = 0f;
            if (finalTan.LengthSquared() > 1e-9f)
                aimDir = finalTan.Normalized();
        }

        int gear = (_i < _gears.Length) ? _gears[_i] : +1;
        var facingDir = (gear >= 0) ? aimDir : -aimDir;

        var desiredYaw = Basis.LookingAt(facingDir, Vector3.Up);
        var slerpedYaw = GlobalTransform.Basis.Slerp(desiredYaw, Mathf.Clamp((float)(TurnSmoothing * delta), 0f, 1f));

        // Apply yaw and re-pin Y so nothing drags us down
        var pinned = GlobalTransform.Origin; pinned.Y = _baseY;
        GlobalTransform = new Transform3D(slerpedYaw, pinned);
    }
}