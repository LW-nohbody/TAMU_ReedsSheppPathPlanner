using System;
using Godot;
using SimCore.Core;
using SimCore.Services;

/// <summary>
/// Clean, simple robot brain:
/// 1. Find highest point in your sector
/// 2. Use Reeds-Shepp to drive there
/// 3. Dig a little (flatten the peak)
/// 4. When full, drive back to origin and dump
/// 5. Repeat until sector is flat
/// </summary>
public sealed class VehicleBrain
{
  private readonly VehicleAgent3D _ctrl;
  private readonly VehicleSpec _spec;
  private readonly IPathPlanner _planner;
  private readonly WorldState _world;
  private readonly TerrainDisk _terrain;
  
  // Robot's assigned sector
  private readonly float _thetaMin, _thetaMax, _maxRadius;
  private readonly Vector3 _homePosition;
  
  // Robot state
  private float _payload = 0f;
  private bool _returningHome = false;

  public VehicleBrain(
    VehicleAgent3D ctrl, 
    VehicleSpec spec, 
    IPathPlanner planner,
    WorldState world,
    TerrainDisk terrain,
    float thetaMin,
    float thetaMax,
    float maxRadius,
    Vector3 homePosition)
  {
    _ctrl = ctrl;
    _spec = spec;
    _planner = planner;
    _world = world;
    _terrain = terrain;
    _thetaMin = thetaMin;
    _thetaMax = thetaMax;
    _maxRadius = maxRadius;
    _homePosition = homePosition;
  }

  public void PlanAndGoOnce()
  {
    // Get current pose
    var xf = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z;
    double yaw = Math.Atan2(fwd.Z, fwd.X);
    var curPose = new Pose(xf.Origin.X, xf.Origin.Z, yaw);
    var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);

    // Decide what to do
    Vector3 targetPos;
    
    if (_returningHome)
    {
      // Go home to dump
      targetPos = _homePosition;
    }
    else if (_payload >= SimpleDigLogic.ROBOT_CAPACITY)
    {
      // Full! Go home
      _returningHome = true;
      targetPos = _homePosition;
      GD.Print($"[{_spec.Name}] Full ({_payload:F3}m³), returning home");
    }
    else
    {
      // Find highest point in sector and go dig there
      targetPos = SimpleDigLogic.FindHighestInSector(
        _terrain, _thetaMin, _thetaMax, _maxRadius);
      
      // Check if sector is already flat enough
      if (!SimpleDigLogic.HasWorkRemaining(_terrain, _thetaMin, _thetaMax, _maxRadius))
      {
        // Done! Just idle at home
        targetPos = _homePosition;
        GD.Print($"[{_spec.Name}] Sector flat! Idling at home.");
      }
    }

    // Plan Reeds-Shepp path to target
    var goalPose = new Pose(targetPos.X, targetPos.Z, yaw);
    var planned = _planner.Plan(curPose, goalPose, _spec, _world);
    var pts = planned.Points.ToArray();
    var gears = planned.Gears.ToArray();

    // Send path to controller
    if (pts.Length == 0 || (pts.Length == 1 && curPos.DistanceTo(pts[0]) < 0.3f))
    {
      // Already at target
      _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
      OnArrival();
    }
    else
    {
      _ctrl.SetPath(pts, gears);
    }
  }

  public void OnArrival()
  {
    var curPos = new Vector3(_ctrl.GlobalTransform.Origin.X, 0, _ctrl.GlobalTransform.Origin.Z);
    
    if (_returningHome)
    {
      // At home - dump payload
      if (curPos.DistanceTo(_homePosition) < 1.0f)
      {
        _world.TotalDirtExtracted += _payload;
        GD.Print($"[{_spec.Name}] Dumped {_payload:F3}m³ at home. World total: {_world.TotalDirtExtracted:F2}m³");
        _payload = 0f;
        _returningHome = false;
      }
    }
    else
    {
      // At dig site - perform dig
      Vector3 digTarget = SimpleDigLogic.FindHighestInSector(
        _terrain, _thetaMin, _thetaMax, _maxRadius);
      
      if (curPos.DistanceTo(digTarget) < 2.0f)
      {
        // Dig radius based on robot width (covers robot footprint)
        float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
        float dug = SimpleDigLogic.PerformDig(_terrain, digTarget, _payload, SimpleDigLogic.ROBOT_CAPACITY, digRadius);
        _payload += dug;
        GD.Print($"[{_spec.Name}] Dug {dug:F4}m³ at {digTarget} (radius={digRadius:F2}m). Payload: {_payload:F3}m³");
      }
    }
  }

  public float GetPayload() => _payload;
}
