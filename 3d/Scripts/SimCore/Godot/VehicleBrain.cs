using System;
using Godot;
using SimCore.Core;
using SimCore.Services;

public sealed class VehicleBrain
{
  private readonly VehicleAgent3D _ctrl;
  private readonly VehicleSpec     _spec;
  private readonly IPathPlanner    _planner;
  private readonly IScheduler      _sched;
  private readonly WorldState      _world;

  private bool _payloadFull;

  public VehicleBrain(VehicleAgent3D ctrl, VehicleSpec spec, IPathPlanner planner,
                      IScheduler sched, WorldState world)
  {
    _ctrl = ctrl; _spec = spec; _planner = planner; _sched = sched; _world = world;
  }

  public void PlanAndGoOnce()  // single-cycle until you add events/callbacks
  {
    // Derive current pose from the controller
    var xf  = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z; // Godot forward
    double yaw = Math.Atan2(fwd.Z, fwd.X); // 0 along +X, CCW to +Z
    var cur = new Pose(xf.Origin.X, xf.Origin.Z, yaw);

    // Get task
    var task = _sched.NextTask(_spec, _world, _payloadFull);

    // Convert task to a goal pose (simple facing = look at target)
    Pose goal = task switch {
      DigTask d  => ToPoseFacing(cur, new Vector3((float)cur.X, 0, (float)cur.Z), d.SiteCenter),
      DumpTask d => ToPoseFacing(cur, new Vector3((float)cur.X, 0, (float)cur.Z), d.DumpPoint),
      TransitTask t => t.Goal,
      _ => cur
    };

    // Plan & send to controller
    var planned = _planner.Plan(cur, goal, _spec, _world);
    _ctrl.SetPath(planned.Points.ToArray(), planned.Gears.ToArray());

    // For now, “execute” immediately after path (you can hook PathFinished later)
    if (task is DigTask dig)  _payloadFull = true;
    if (task is DumpTask)     _payloadFull = false;
  }

  private static Pose ToPoseFacing(Pose start, Vector3 from, Vector3 target)
  {
    var dir = (target - from); dir.Y = 0;
    if (dir.LengthSquared() < 1e-6f) return start;
    dir = dir.Normalized();
    double yaw = Math.Atan2(dir.Z, dir.X);
    return new Pose(target.X, target.Z, yaw);
  }
}