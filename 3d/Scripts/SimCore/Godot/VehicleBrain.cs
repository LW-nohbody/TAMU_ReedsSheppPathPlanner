using System;
using System.Collections.Generic;
using Godot;
using SimCore.Core;
using SimCore.Services;

/// <summary>
/// Simplified robot brain:
/// 1. Find nearest highest point in entire terrain (no sector restriction)
/// 2. Use Reeds-Shepp to drive there
/// 3. Dig a little (flatten the peak)
/// 4. When full, drive back to origin and dump
/// 5. Repeat
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
  
  // Home position (origin)
  private readonly Vector3 _homePosition;
  
  // Robot state
  private float _payload = 0f;
  private bool _returningHome = false;
  private int _digsCompleted = 0;
  private float _totalDug = 0f;
  private Vector3 _currentTarget = Vector3.Zero;
  private string _currentStatus = "Initializing";

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
    _homePosition = homePosition;
  }

  /// <summary>
  /// Main update loop - simple algorithm:
  /// If full -> go home and dump
  /// Otherwise -> find nearest highest point and dig
  /// </summary>
  public void PlanAndGoOnce()
  {
    try
    {
      if (_ctrl == null || !GodotObject.IsInstanceValid(_ctrl)) return;

      Vector3 curPos = new Vector3(_ctrl.GlobalTransform.Origin.X, 0, _ctrl.GlobalTransform.Origin.Z);

      // Decide what to do
      if (_returningHome)
      {
        // Go home to dump
        float distToHome = curPos.DistanceTo(_homePosition);
        _currentStatus = $"Dumping ({distToHome:F1}m)";
        
        // Dump if close enough
        if (distToHome < 5.0f && _payload > 0.001f)
        {
          _world.TotalDirtExtracted += _payload;
          GD.Print($"[Robot_{_robotId}] ✓✓✓ DUMPED {_payload:F2}m³ at ({curPos.X:F1}, {curPos.Z:F1}) - Total: {_world.TotalDirtExtracted:F2}m³");
          _payload = 0f;
          _returningHome = false;
          _currentStatus = "Ready";
          _ctrl.SetPath(Array.Empty<Vector3>(), Array.Empty<int>());
          return;
        }

        // Plan path to home
        PlanPath(curPos, _homePosition);
      }
      else if (_payload >= SimulationConfig.RobotLoadCapacity)
      {
        // Full! Go home
        _returningHome = true;
        _currentStatus = $"FULL ({_payload:F2}m³)";
        _coordinator.ReleaseClaim(_robotId);
        GD.Print($"[Robot_{_robotId}] ▌▌▌ FULL ({_payload:F2}m³ / {SimulationConfig.RobotLoadCapacity}m³), heading home");
        PlanPath(curPos, _homePosition);
      }
      else
      {
        // Find nearest highest point in entire terrain
        Vector3 digTarget = FindNearestHighestPoint(curPos);
        
        if (digTarget != Vector3.Zero)
        {
          float digRadius = SimpleDigLogic.GetDigRadius(_spec.Width);
          
          if (_coordinator.ClaimDigSite(_robotId, digTarget, digRadius))
          {
            _currentTarget = digTarget;
            float dist = curPos.DistanceTo(digTarget);
            _currentStatus = $"Digging ({dist:F1}m away)";
            PlanPath(curPos, digTarget);
          }
          else
          {
            _currentStatus = "Waiting (robot collision)";
          }
        }
        else
        {
          _currentStatus = "No targets found";
          _returningHome = true;
          PlanPath(curPos, _homePosition);
        }
      }
    }
    catch (System.Exception ex)
    {
      GD.PrintErr($"[Robot_{_robotId}] Error: {ex.Message}");
      _currentStatus = "Error";
    }
  }

  /// <summary>
  /// Find the nearest highest point in the entire terrain
  /// </summary>
  private Vector3 FindNearestHighestPoint(Vector3 currentPos)
  {
    var candidates = new List<(Vector3 pos, float height, float distance)>();
    
    // Sample terrain in concentric circles
    int anglesSamples = 12;  // 12 angles (30° apart)
    int radiusRings = 6;      // 6 distance rings
    
    for (int a = 0; a < anglesSamples; a++)
    {
      float theta = (float)a / anglesSamples * Mathf.Tau;
      
      for (int r = 1; r <= radiusRings; r++)
      {
        float sampleRadius = r * 2.5f;  // 2.5m to 15m
        Vector3 samplePos = currentPos + new Vector3(Mathf.Cos(theta) * sampleRadius, 0, Mathf.Sin(theta) * sampleRadius);
        
        if (_terrain.SampleHeightNormal(samplePos, out var hitPos, out var _))
        {
          float distance = currentPos.DistanceTo(hitPos);
          candidates.Add((new Vector3(hitPos.X, 0, hitPos.Z), hitPos.Y, distance));
        }
      }
    }

    if (candidates.Count == 0)
      return Vector3.Zero;

    // Sort by: height (descending) primary, distance (ascending) secondary
    // This prioritizes highest points, but prefers closer ones if heights are similar
    candidates.Sort((a, b) =>
    {
      // Compare heights (higher first)
      float heightDiff = b.height - a.height;
      if (Mathf.Abs(heightDiff) > 0.1f)  // Threshold to avoid floating point noise
        return heightDiff > 0 ? -1 : 1;
      
      // Heights are similar - closer is better
      return a.distance.CompareTo(b.distance);
    });

    return candidates[0].pos;
  }

  /// <summary>
  /// Plan and execute Reeds-Shepp path to target
  /// </summary>
  private void PlanPath(Vector3 startPos, Vector3 targetPos)
  {
    try
    {
      if (_ctrl == null) return;

      // Get current heading
      var fwd = -_ctrl.GlobalTransform.Basis.Z;
      double startYaw = Mathf.Atan2(fwd.Z, fwd.X);

      // Calculate target heading
      Vector3 toTarget = (targetPos - startPos).Normalized();
      double targetYaw = Mathf.Atan2(toTarget.Z, toTarget.X);

      // Plan using RSAdapter (Reeds-Shepp path planning)
      var result = RSAdapter.ComputePath3D(
        startPos, startYaw,
        targetPos, targetYaw,
        _ctrl.TurnRadiusMeters, 0.25f);

      var pts = result.Item1;
      var gears = result.Item2;

      if (pts != null && pts.Length > 0)
      {
        _ctrl.SetPath(pts, gears != null ? gears : Array.Empty<int>());
      }
    }
    catch (System.Exception ex)
    {
      GD.PrintErr($"[Robot_{_robotId}] Path planning failed: {ex.Message}");
    }
  }

  /// <summary>
  /// Called when robot arrives at target
  /// </summary>
  public void OnArrival()
  {
    try
    {
      if (_returningHome || _currentTarget == Vector3.Zero) return;

      Vector3 digPos = _currentTarget;
      
      // Dig at current location
      float digAmount = SimpleDigLogic.DIG_AMOUNT;
      _terrain.LowerArea(digPos, 2.0f, digAmount);
      
      // Add to payload
      _payload = Mathf.Min(_payload + digAmount * 0.5f, SimulationConfig.RobotLoadCapacity);
      
      _digsCompleted++;
      _totalDug += digAmount * 0.5f;

      GD.Print($"[Robot_{_robotId}] Dug {digAmount:F3}m → Payload: {_payload:F2}m³");

      // Release claim
      _coordinator.ReleaseClaim(_robotId);
      _currentTarget = Vector3.Zero;
    }
    catch (System.Exception ex)
    {
      GD.PrintErr($"[Robot_{_robotId}] Error in OnArrival: {ex.Message}");
    }
  }
  public float GetPayload() => _payload;

  /// <summary>
  /// Get the current planned path for visualization
  /// </summary>
  public List<Vector3> GetCurrentPath()
  {
    var path = new List<Vector3>();
    
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
      // Silently fail
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
      return GodotObject.IsInstanceValid(_ctrl) && _ctrl.IsDone;
    }
    catch
    {
      return false;
    }
  }
}

