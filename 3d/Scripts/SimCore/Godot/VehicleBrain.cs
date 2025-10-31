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
    
    // Longer threshold to avoid false positives (60 frames = 1 second at 60fps)
    if (_stuckCycleCount > 60)
    {
      GD.PrintErr($"[{_spec.Name}] STUCK for {_stuckCycleCount} cycles at {currentPos}. Recovering...");
      
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
    // Get current pose
    var xf = _ctrl.GlobalTransform;
    var fwd = -xf.Basis.Z;
    double yaw = Math.Atan2(fwd.Z, fwd.X);
    var curPose = new Pose(xf.Origin.X, xf.Origin.Z, yaw);
    var curPos = new Vector3(xf.Origin.X, 0, xf.Origin.Z);

    // Check if robot is stuck - but continue planning anyway
    bool isStuck = IsStuck(curPos);
    if (isStuck)
    {
      _currentStatus = "STUCK - Recovering";
      // Don't return - let it replan below
    }

    // Decide what to do
    Vector3 targetPos;
    
    if (_returningHome)
    {
      // Go home to dump
      targetPos = _homePosition;
      
      // If very close to home, try to dump NOW before planning another path
      float distToHome = curPos.DistanceTo(_homePosition);
      _currentStatus = $"Returning Home ({distToHome:F1}m)";
      
      if (distToHome < 5.0f)  // Very lenient - try to dump when reasonably close
      {
        GD.Print($"[{_spec.Name}] Close to home ({distToHome:F2}m), attempting dump in PlanAndGoOnce...");
        OnArrival();  // Try to dump
        
        // If we dumped, mark it
        if (!_returningHome)  // If _returningHome was set to false, we dumped
        {
          GD.Print($"[{_spec.Name}] Successfully dumped! Replanning for dig.");
          _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
          return;
        }
      }
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
        _currentTarget = targetPos;  // Update current target when claim succeeds
      }
      else
      {
        _currentStatus = "Waiting (too close to others)";
        // If we have a previous target, try to move closer to it
        // Otherwise, move to a different part of the sector
        if (_currentTarget == Vector3.Zero)
        {
          // First time or no previous target - pick sector center
          float midTheta = (_thetaMin + _thetaMax) / 2f;
          targetPos = new Vector3(
            Mathf.Cos(midTheta) * _maxRadius * 0.3f,
            0,
            Mathf.Sin(midTheta) * _maxRadius * 0.3f
          );
        }
        else
        {
          targetPos = _currentTarget;  // Keep trying previous target
        }
      }
      
      // Check if sector is already flat enough
      if (!SimpleDigLogic.HasWorkRemaining(_terrain, _thetaMin, _thetaMax, _maxRadius))
      {
        // Sector complete! Notify director and idle
        if (!_sectorCompleted)
        {
          _sectorCompleted = true;
          try
          {
            _onSectorComplete?.Invoke(_robotId);
          }
          catch (System.Exception ex)
          {
            GD.PrintErr($"[{_spec.Name}] Error in sector complete callback: {ex.Message}");
          }
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

    // Debug logging for stuck situations
    if (pts.Length == 0)
    {
      GD.PrintErr($"[{_spec.Name}] FAILED TO PLAN PATH from ({curPos.X:F1}, {curPos.Z:F1}) to ({targetPos.X:F1}, {targetPos.Z:F1}). Status: {_currentStatus}");
    }

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
    try
    {
      var curPos = new Vector3(_ctrl.GlobalTransform.Origin.X, 0, _ctrl.GlobalTransform.Origin.Z);
      
      if (_returningHome)
      {
        // At home - dump payload
        // Be VERY lenient with distance since robots might be stuck at origin
        float distToHome = curPos.DistanceTo(_homePosition);
        
        GD.Print($"[{_spec.Name}] OnArrival (returning home): distToHome={distToHome:F2}m, payload={_payload:F3}m³");
        
        // If close to home OR if we haven't moved and have full payload, dump it
        if (distToHome < 5.0f || (distToHome < 1.0f && _payload > 0.1f))
        {
          if (_payload > 0.001f)
          {
            GD.Print($"[{_spec.Name}] *** DUMPING {_payload:F3}m³ ***");
            _world.TotalDirtExtracted += _payload;
            
            // Try to log remaining dirt
            float remainingDirt = -1f;
            if (_terrain != null)
            {
              try
              {
                remainingDirt = _terrain.GetRemainingDirtVolume();
              }
              catch (System.Exception ex)
              {
                GD.PrintErr($"Error getting remaining dirt: {ex.Message}");
              }
            }
            
            if (remainingDirt >= 0f)
              GD.Print($"[{_spec.Name}] Dumped {_payload:F3}m³ at home. World total: {_world.TotalDirtExtracted:F2}m³. Remaining: {remainingDirt:F2}m³");
            else
              GD.Print($"[{_spec.Name}] Dumped {_payload:F3}m³ at home. World total: {_world.TotalDirtExtracted:F2}m³");
            
            _payload = 0f;
            _returningHome = false;
            _currentStatus = "Dumped - Ready";
            return;  // Exit early after dumping
          }
        }
        
        // Still trying to get home
        _currentStatus = $"Going Home ({distToHome:F1}m away)";
        return;
      }
      
      // NOT returning home - try to dig
      if (_terrain != null)
      {
        Vector3 digTarget = _coordinator.GetBestDigPoint(
          _robotId, _terrain, _thetaMin, _thetaMax, _maxRadius);
        
        float dist = curPos.DistanceTo(digTarget);
        
        // Very lenient dig distance
        if (dist < 3.0f)
        {
          float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
          float dug = SimpleDigLogic.PerformDig(_terrain, digTarget, _payload, SimpleDigLogic.ROBOT_CAPACITY, digRadius);
          
          if (dug > 0.00001f)
          {
            _payload += dug;
            _totalDug += dug;
            _digsCompleted++;
            _currentStatus = $"Dug! ({_digsCompleted} digs)";
            
            float remainingDirt = -1f;
            try
            {
              remainingDirt = _terrain.GetRemainingDirtVolume();
            }
            catch { }
            
            if (remainingDirt >= 0f)
              GD.Print($"[{_spec.Name}] Dug {dug:F4}m³. Payload: {_payload:F3}/{SimpleDigLogic.ROBOT_CAPACITY:F3}m³. Remaining: {remainingDirt:F2}m³");
          }
        }
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
