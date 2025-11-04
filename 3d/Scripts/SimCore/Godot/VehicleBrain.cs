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
    
    // INCREASED threshold - 120 frames (2 seconds at 60fps) to allow more time for path planning
    if (_stuckCycleCount > 120)
    {
      // Only log once per recovery, not every frame
      if (_stuckCycleCount == 121)
      {
        GD.PrintErr($"[{_spec.Name}] STUCK for {_stuckCycleCount} cycles at {currentPos}. Recovering...");
      }
      
      // Recovery: release claim and reset
      _coordinator.ReleaseClaim(_robotId);
      _stuckCycleCount = 0;
      _lastKnownGoodPos = currentPos;
      return true;
    }
    
    return false;
  }

  public void PlanAndGoOnce()
  {
    try
    {
      // Validate controller is still valid before attempting to use it
      if (_ctrl == null || !GodotObject.IsInstanceValid(_ctrl))
      {
        _currentStatus = "ERROR: Controller invalid";
        GD.PrintErr($"[{_spec.Name}] Controller is no longer valid!");
        return;
      }

      // Get current pose
      var xf = _ctrl.GlobalTransform;
      var fwd = -xf.Basis.Z;
      double yaw = Math.Atan2(fwd.Z, fwd.X);
      var curPose = new Pose(xf.Origin.X, xf.Origin.Z, yaw);
      var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);

      // Check if robot is stuck
      bool isStuck = IsStuck(curPos);
      if (isStuck)
      {
        _currentStatus = "STUCK - Recovering";
        // Clear current target to force new path planning
        _currentTarget = Vector3.Zero;
        _returningHome = false;  // Reset state
      }

      // Decide what to do
      Vector3 targetPos = Vector3.Zero;
      
      if (_returningHome)
      {
        // Go home to dump
        targetPos = _homePosition;
        float distToHome = curPos.DistanceTo(_homePosition);
        _currentStatus = $"Returning Home ({distToHome:F1}m)";
        
        // Try to dump if close
        if (distToHome < 5.0f && _payload > 0.001f)
        {
          _world.TotalDirtExtracted += _payload;
          GD.Print($"[{_spec.Name}] *** DUMPED {_payload:F3}m³ *** Total: {_world.TotalDirtExtracted:F2}m³");
          _payload = 0f;
          _returningHome = false;
          _currentStatus = "Dumped - Ready";
          _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
          return;
        }
      }
      else if (_payload >= SimpleDigLogic.ROBOT_CAPACITY)
      {
        // Full! Go home
        _returningHome = true;
        targetPos = _homePosition;
        _currentStatus = "▌▌▌ FULL - Going Home ▌▌▌";
        _coordinator.ReleaseClaim(_robotId);
        GD.Print($"[{_spec.Name}] ▌▌▌ FULL ({_payload:F3}m³ / {SimpleDigLogic.ROBOT_CAPACITY}m³), heading to home at ({_homePosition.X:F1}, {_homePosition.Z:F1})");
      }
      else
      {
        // Find dig target
        targetPos = _coordinator.GetBestDigPoint(_robotId, _terrain, _thetaMin, _thetaMax, _maxRadius);
        
        float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
        if (_coordinator.ClaimDigSite(_robotId, targetPos, digRadius))
        {
          _currentStatus = "Digging";
        }
        else
        {
          _currentStatus = "Waiting (collision)";
          if (_currentTarget != Vector3.Zero)
            targetPos = _currentTarget;
        }
        
        // Check if sector is done
        if (!SimpleDigLogic.HasWorkRemaining(_terrain, _thetaMin, _thetaMax, _maxRadius))
        {
          if (!_sectorCompleted)
          {
            _sectorCompleted = true;
            try
            {
              _onSectorComplete?.Invoke(_robotId);
            }
            catch { }
            GD.Print($"[{_spec.Name}] Sector complete!");
          }
          
          targetPos = _homePosition;
          _currentStatus = "Sector Done - Idling";
          _coordinator.ReleaseClaim(_robotId);
        }
      }

      _currentTarget = targetPos;

      // Plan path
      var goalPose = new Pose(targetPos.X, targetPos.Z, yaw);
      var planned = _planner.Plan(curPose, goalPose, _spec, _world);
      var pts = planned.Points.ToArray();
      var gears = planned.Gears.ToArray();

      if (pts.Length == 0 || (pts.Length == 1 && curPos.DistanceTo(pts[0]) < 0.5f))
      {
        // Already at target or path planning failed
        _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
        // Only call OnArrival once when we're truly at the target
        OnArrival();
      }
      else
      {
        _ctrl.SetPath(pts, gears);
      }
    }
    catch (System.Exception ex)
    {
      GD.PrintErr($"[{_spec.Name}] Exception in PlanAndGoOnce: {ex}");
    }
  }

  public void OnArrival()
  {
    try
    {
      // FIRST: Validate all critical objects are not null AND valid
      if (_ctrl == null || !GodotObject.IsInstanceValid(_ctrl))
      {
        GD.PrintErr("OnArrival: _ctrl is null or invalid!");
        return;
      }

      if (_world == null)
      {
        GD.PrintErr("OnArrival: _world is null!");
        return;
      }

      if (_coordinator == null)
      {
        GD.PrintErr("OnArrival: _coordinator is null!");
        return;
      }

      if (_terrain == null)
      {
        GD.PrintErr("OnArrival: _terrain is null!");
        return;
      }

      Vector3 curPos = Vector3.Zero;
      try
      {
        curPos = new Vector3(_ctrl.GlobalTransform.Origin.X, 0, _ctrl.GlobalTransform.Origin.Z);
      }
      catch
      {
        GD.PrintErr("OnArrival: Failed to get current position");
        return;
      }

      // DUMP LOGIC
      if (_returningHome)
      {
        float distToHome = 0f;
        try
        {
          distToHome = curPos.DistanceTo(_homePosition);
        }
        catch
        {
          distToHome = 999f;
        }

        // Dump if close enough AND have payload
        if (distToHome < 5.0f && _payload > 0.001f)
        {
          _world.TotalDirtExtracted += _payload;
          GD.Print($"[{_spec.Name}] ✓✓✓ DUMPED {_payload:F3}m³ at ({curPos.X:F1}, {curPos.Z:F1}) - Distance to home: {distToHome:F2}m - Total extracted: {_world.TotalDirtExtracted:F2}m³");
          
          _payload = 0f;
          _returningHome = false;
          _currentStatus = "Dumped - Ready";
          return;
        }

        // Not home yet
        _currentStatus = $"Going Home ({distToHome:F1}m away)";
        return;
      }

      // DIG LOGIC (only if not returning home)
      try
      {
        if (_terrain == null)
        {
          _currentStatus = "Terrain is null!";
          return;
        }

        if (_coordinator == null)
        {
          _currentStatus = "Coordinator is null!";
          return;
        }

        Vector3 digTarget = Vector3.Zero;
        try
        {
          digTarget = _coordinator.GetBestDigPoint(_robotId, _terrain, _thetaMin, _thetaMax, _maxRadius);
        }
        catch (System.Exception ex)
        {
          _currentStatus = $"Failed to get dig point: {ex.Message}";
          return;
        }

        if (digTarget == Vector3.Zero)
        {
          _currentStatus = "No valid dig target";
          return;
        }

        float distToDig = curPos.DistanceTo(digTarget);

        if (distToDig < 2.5f)  // Consistent tolerance with dump (5.0f is ~2x this for longer distance)
        {
          float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
          float dug = 0f;
          
          try
          {
            dug = SimpleDigLogic.PerformDig(_terrain, digTarget, _payload, SimpleDigLogic.ROBOT_CAPACITY, digRadius);
          }
          catch
          {
            _currentStatus = "Dig failed";
            return;
          }

          if (dug > 0.00001f)
          {
            _payload += dug;
            _totalDug += dug;
            _digsCompleted++;
            _currentStatus = $"Dug {dug:F4}m³";
            GD.Print($"[{_spec.Name}] Dug {dug:F4}m³ -> Payload: {_payload:F3}m³");
          }
        }
        else
        {
          _currentStatus = $"Moving to dig ({distToDig:F1}m)";
        }
      }
      catch (System.Exception ex)
      {
        GD.PrintErr($"[{_spec.Name}] Error in dig logic: {ex.Message}");
        _currentStatus = "Dig error";
      }
    }
    catch (System.Exception ex)
    {
      GD.PrintErr($"[{_spec.Name}] CRITICAL Exception in OnArrival: {ex}");
    }
  }

  public float GetPayload() => _payload;

  /// <summary>
  /// Get the current planned path for visualization
  /// </summary>
  public List<Vector3> GetCurrentPath()
  {
    var path = new List<Vector3>();
    
    // Use public accessor instead of reflection
    try
    {
      var pathArray = _ctrl.GetCurrentPath();
      if (pathArray != null && pathArray.Length > 0)
      {
        path.AddRange(pathArray);
      }
    }
    catch
    {
      // Silently fail if controller is no longer valid
    }
    
    return path;
  }

  /// <summary>
  /// Check if the robot has finished its current path
  /// </summary>
  public bool IsPathComplete()
  {
    try
    {
      if (_ctrl == null) return false;
      // Check if the handle is still valid
      return GodotObject.IsInstanceValid(_ctrl) && _ctrl.IsDone;
    }
    catch
    {
      return false;
    }
  }
}
