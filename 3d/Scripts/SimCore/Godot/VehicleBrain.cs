using System;
using System.Collections.Generic;
using Godot;
using SimCore.Core;
using SimCore.Services;

/// <summary>
/// Enhanced robot brain with collision avoidance and stats tracking:
/// 1. Find highest point in your sector (avoiding other robots)
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
  private readonly RobotCoordinator _coordinator;
  private readonly int _robotId;
  
  // Robot's assigned sector
  private readonly float _thetaMin, _thetaMax, _maxRadius;
  private readonly Vector3 _homePosition;
  
  // Callback when sector is complete
  private System.Action<int> _onSectorComplete = null;
  
  // Robot state
  private float _payload = 0f;
  private bool _returningHome = false;
  private int _digsCompleted = 0;
  private float _totalDug = 0f;
  private Vector3 _currentTarget = Vector3.Zero;
  private string _currentStatus = "Initializing";
  private bool _sectorCompleted = false;
  
  // Stuck detection
  private Vector3 _lastKnownGoodPos = Vector3.Zero;
  private int _stuckCycleCount = 0;
  private const int STUCK_THRESHOLD = 30;  // Frames without significant movement

  // Public properties for UI/stats
  public int RobotId => _robotId;
  public float Payload => _payload;
  public int DigsCompleted => _digsCompleted;
  public float TotalDug => _totalDug;
  public Vector3 CurrentTarget => _currentTarget;
  public string Status => _currentStatus;
  public Vector3 CurrentPosition => new Vector3(_ctrl.GlobalTransform.Origin.X, 0, _ctrl.GlobalTransform.Origin.Z);

  public VehicleBrain(
    VehicleAgent3D ctrl, 
    VehicleSpec spec, 
    IPathPlanner planner,
    WorldState world,
    TerrainDisk terrain,
    RobotCoordinator coordinator,
    int robotId,
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
    _coordinator = coordinator;
    _robotId = robotId;
    _thetaMin = thetaMin;
    _thetaMax = thetaMax;
    _maxRadius = maxRadius;
    _homePosition = homePosition;
  }

  /// <summary>
  /// Set callback for when this sector is complete
  /// </summary>
  public void SetSectorCompleteCallback(System.Action<int> callback)
  {
    _onSectorComplete = callback;
  }

  /// <summary>
  /// Check if robot is stuck and attempt recovery
  /// </summary>
  private bool IsStuck(Vector3 currentPos)
  {
    float distMoved = currentPos.DistanceTo(_lastKnownGoodPos);
    
    // If we've moved a reasonable distance, we're good
    if (distMoved > 0.5f)
    {
      _lastKnownGoodPos = currentPos;
      _stuckCycleCount = 0;
      return false;
    }
    
    // Not moved much - increment counter
    _stuckCycleCount++;
    
    if (_stuckCycleCount > STUCK_THRESHOLD)
    {
      GD.PrintErr($"[{_spec.Name}] STUCK for {_stuckCycleCount} cycles at {currentPos}. Recovering...");
      
      // Recovery: release claim and go to new dig site
      _coordinator.ReleaseClaim(_robotId);
      _stuckCycleCount = 0;
      _lastKnownGoodPos = currentPos;
      return true;
    }
    
    return false;
  }

  public void PlanAndGoOnce()
  {
    // Get current pose
    var xf = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z;
    double yaw = Math.Atan2(fwd.Z, fwd.X);
    var curPose = new Pose(xf.Origin.X, xf.Origin.Z, yaw);
    var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);

    // Check if robot is stuck
    if (IsStuck(curPos))
    {
      _currentStatus = "STUCK - Recovering";
      // Force a new plan by clearing the current path
      _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
      // Will re-plan on next cycle
      return;
    }

    // Decide what to do
    Vector3 targetPos;
    
    if (_returningHome)
    {
      // Go home to dump
      targetPos = _homePosition;
      _currentStatus = "Returning Home";
    }
    else if (_payload >= SimpleDigLogic.ROBOT_CAPACITY)
    {
      // Full! Go home
      _returningHome = true;
      targetPos = _homePosition;
      _currentStatus = "Full - Going Home";
      _coordinator.ReleaseClaim(_robotId);
      GD.Print($"[{_spec.Name}] Full ({_payload:F3}m³), returning home");
    }
    else
    {
      // Find highest point in sector (avoiding other robots)
      targetPos = _coordinator.GetBestDigPoint(
        _robotId, _terrain, _thetaMin, _thetaMax, _maxRadius);
      
      // Try to claim this dig site
      float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
      if (_coordinator.ClaimDigSite(_robotId, targetPos, digRadius))
      {
        _currentStatus = "Digging";
      }
      else
      {
        _currentStatus = "Waiting (too close to others)";
        // Keep previous target, try again next cycle
        targetPos = _currentTarget;
      }
      
      // Check if sector is already flat enough
      if (!SimpleDigLogic.HasWorkRemaining(_terrain, _thetaMin, _thetaMax, _maxRadius))
      {
        // Sector complete! Notify director and idle
        if (!_sectorCompleted)
        {
          _sectorCompleted = true;
          _onSectorComplete?.Invoke(_robotId);
          GD.Print($"[{_spec.Name}] Sector {_robotId} COMPLETE - calling callback");
        }
        
        // Done! Just idle at home
        targetPos = _homePosition;
        _currentStatus = "Sector Complete - Idling";
        _coordinator.ReleaseClaim(_robotId);
        GD.Print($"[{_spec.Name}] Sector flat! Idling at home.");
      }
    }

    _currentTarget = targetPos;

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
      // At home - dump payload (increased tolerance to 2.0f to ensure robots dump)
      if (curPos.DistanceTo(_homePosition) < 2.0f)
      {
        if (_payload > 0.001f)  // Only dump if we have something to dump
        {
          _world.TotalDirtExtracted += _payload;
          GD.Print($"[{_spec.Name}] Dumped {_payload:F3}m³ at home. World total: {_world.TotalDirtExtracted:F2}m³. Remaining dirt: {_terrain.GetRemainingDirtVolume():F2}m³");
          _payload = 0f;
          _returningHome = false;
          _currentStatus = "Dumped - Ready";
        }
        else
        {
          // Empty, just go back to digging
          _returningHome = false;
          _currentStatus = "Dump Complete - Returning to Dig";
        }
      }
    }
    else
    {
      // At dig site - perform dig
      Vector3 digTarget = _coordinator.GetBestDigPoint(
        _robotId, _terrain, _thetaMin, _thetaMax, _maxRadius);
      
      if (curPos.DistanceTo(digTarget) < 2.5f)  // Slightly increased tolerance
      {
        // Dig radius based on robot width (covers robot footprint)
        float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
        float dug = SimpleDigLogic.PerformDig(_terrain, digTarget, _payload, SimpleDigLogic.ROBOT_CAPACITY, digRadius);
        
        if (dug > 0.0001f)  // Only count if we actually dug something
        {
          _payload += dug;
          _totalDug += dug;
          _digsCompleted++;
          _currentStatus = $"Dug! ({_digsCompleted} digs)";
          GD.Print($"[{_spec.Name}] Dug {dug:F4}m³ at {digTarget} (radius={digRadius:F2}m). Payload: {_payload:F3}m³ / Remaining: {_terrain.GetRemainingDirtVolume():F2}m³");
        }
      }
    }
  }

  public float GetPayload() => _payload;

  /// <summary>
  /// Get the current planned path for visualization
  /// </summary>
  public List<Vector3> GetCurrentPath()
  {
    var path = new List<Vector3>();
    
    // Get path from the controller using reflection
    var pathField = typeof(VehicleAgent3D).GetField("_path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (pathField != null)
    {
      var pathArray = pathField.GetValue(_ctrl) as Vector3[];
      if (pathArray != null)
      {
        path.AddRange(pathArray);
      }
    }
    
    return path;
  }
}
