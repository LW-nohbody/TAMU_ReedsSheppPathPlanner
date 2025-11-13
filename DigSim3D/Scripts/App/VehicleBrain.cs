using Godot;
using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    public partial class VehicleBrain : Node
    {
        public VehicleVisualizer Agent { get; private set; } = null!;
        public DigState DigState { get; private set; } = new();

        /// <summary> Robot's assigned sector index (permanent) </summary>
        public int SectorIndex { get; private set; } = -1;

        /// <summary> Total number of sectors (equal to number of robots) </summary>
        private int _totalSectors = 1;

        /// <summary> Cached reference to dig service (set by SimulationDirector) </summary>
        private DigService _digService = null!;

        /// <summary> Cached reference to terrain (set by SimulationDirector) </summary>
        private TerrainDisk _terrain = null!;

        /// <summary> Cached reference to scheduler (set by SimulationDirector) </summary>
        private RadialScheduler _scheduler = null!;

        /// <summary> Cached reference to path planner (set by SimulationDirector) </summary>
        private HybridReedsSheppPlanner _pathPlanner = null!;

        /// <summary> Cached reference to world state (set by SimulationDirector) </summary>
        private WorldState _worldState = null!;

        /// <summary> Dig configuration </summary>
        private DigConfig _digConfig = null!;

        /// <summary> Dig visualizer </summary>
        private DigVisualizer _digVisualizer = null!;

        /// <summary> Path drawing callback (set by SimulationDirector) </summary>
        private System.Action<Vector3[], Color> _drawPathCallback = null!;

        /// <summary> Accumulated dig time at current site (seconds) </summary>
        private float _digTimeAccumulated = 0f;

        /// <summary> List of recently visited dig locations to prevent revisiting </summary>
        private System.Collections.Generic.List<Vector3> _recentDigLocations = new();
        
        /// <summary> Maximum number of recent dig locations to track </summary>
        private const int MaxRecentDigLocations = 10;

        /// <summary> Minimum distance from recent dig locations (to prevent clustering) </summary>
        private const float MinDistanceFromRecentDigs = 3.0f;

        private bool _hasReplannedFromFreeze = false;
                // Track frozen cars we've already replanned for
        private readonly HashSet<VehicleBrain> _frozenCarsAlreadyHandled = new();

        // Freeze/unfreeze radii
        private const float FreezeRadius = 2.0f;      // trigger freeze/replan
        private const float UnfreezeRadius = 3.0f;    // must exceed freeze radius


        private static bool IsInsideAvoidanceRadius(Vector3 a, Vector3 b, float radius)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return (dx * dx + dz * dz) < radius * radius;
        }

        
        public override void _Ready()
        {
            Agent = GetParent<VehicleVisualizer>();
        }

        /// <summary>
        /// Initialize dig brain with external services and sector assignment.
        /// </summary>
        public void InitializeDigBrain(DigService digService, TerrainDisk terrain, 
            RadialScheduler scheduler, DigConfig digConfig, HybridReedsSheppPlanner pathPlanner,
            WorldState worldState, DigVisualizer digVisualizer, System.Action<Vector3[], Color> drawPathCallback,
            int sectorIndex, int totalSectors)
        {
            _digService = digService;
            _terrain = terrain;
            _scheduler = scheduler;
            _digConfig = digConfig;
            _pathPlanner = pathPlanner;
            _worldState = worldState;
            _digVisualizer = digVisualizer;
            _drawPathCallback = drawPathCallback;
            
            // Assign permanent sector
            SectorIndex = sectorIndex;
            _totalSectors = totalSectors;
            
            GD.Print($"[VehicleBrain] {Agent.Name} assigned to sector {SectorIndex}/{_totalSectors}");
        }

        /// <summary>
        /// Update dig state each frame (called by SimulationDirector).
        /// </summary>
        public void UpdateDigBehavior(float deltaSeconds)
        {
            // Check if any nearby frozen robot is blocking us -> replan
            CheckFrozenCarReplan();

            if (_digService == null || _terrain == null || _scheduler == null)
                return;

            var robotPos = Agent.GlobalTransform.Origin;

            switch (DigState.State)
            {
                case DigState.TaskState.Idle:
                    // Request new dig target
                    RequestNewDigTarget();
                    break;

                case DigState.TaskState.TravelingToDigSite:
                    // Check if arrived at dig site
                    float distToDig = robotPos.DistanceTo(DigState.CurrentDigTarget);
                    if (distToDig < _digConfig.AtSiteThreshold)
                    {
                        DigState.State = DigState.TaskState.Digging;
                        _digTimeAccumulated = 0f;
                    }
                    break;

                case DigState.TaskState.Digging:
                    // Dig at current site
                    PerformDigging(deltaSeconds);

                    // Check if payload is full
                    if (DigState.IsPayloadFull)
                    {
                        DigState.State = DigState.TaskState.TravelingToDump;
                        PlanPathToDump();
                    }
                    break;

                case DigState.TaskState.TravelingToDump:
                    // Check if arrived at dump center (origin)
                    float distToDump = robotPos.DistanceTo(Vector3.Zero);
                    if (distToDump < _digConfig.AtDumpThreshold)
                    {
                        DigState.State = DigState.TaskState.Dumping;
                    }
                    break;

                case DigState.TaskState.Dumping:
                    // Unload and request next target
                    DigState.DumpCount++;
                    DigState.CurrentPayload = 0f;
                    
                    // Check if there's still dirt to dig
                    if (HasRemainingTerrain())
                    {
                        // Continue digging - request new target
                        DigState.State = DigState.TaskState.Idle;
                        RequestNewDigTarget();
                    }
                    else
                    {
                        // All dirt removed - mark as complete
                        DigState.State = DigState.TaskState.Complete;
                        GD.Print($"[VehicleBrain] {Agent.Name} completed - no more dirt to dig");
                    }
                    break;

                case DigState.TaskState.Complete:
                    // Do nothing - robot is finished
                    break;
            }
        }

        // ------------------------
        // FROZEN CAR OBSTACLE SYSTEM
        // ------------------------

        private static System.Collections.Generic.Dictionary<VehicleBrain, CylinderObstacle> FrozenCarObstacles
            = new System.Collections.Generic.Dictionary<VehicleBrain, CylinderObstacle>();

        private const float CarObstacleRadius = 0.9f;   // adjust to robot radius
        private const float CarObstacleHeight = 1.5f;

        // Call when THIS car becomes frozen
        public void RegisterFrozenCarObstacle()
        {
            if (FrozenCarObstacles.ContainsKey(this)) return;

            var pos = Agent.GlobalTransform.Origin;
            var obstacle = new CylinderObstacle()
            {
                GlobalPosition = pos,
                Radius = CarObstacleRadius,
                Height = CarObstacleHeight
            };

            FrozenCarObstacles[this] = obstacle;
            _worldState.Obstacles.Add(obstacle);

            GD.Print($"[VehicleBrain] Registered frozen obstacle for {Agent.Name}");
        }

        // Call when THIS car unfreezes
        public void RemoveFrozenCarObstacle()
        {
            if (!FrozenCarObstacles.ContainsKey(this)) return;

            var obstacle = FrozenCarObstacles[this];
            _worldState.Obstacles.Remove(obstacle);
            FrozenCarObstacles.Remove(this);

            GD.Print($"[VehicleBrain] Removed frozen obstacle for {Agent.Name}");
        }

        private double FrozenReplanCooldown = 0f;

        private void CheckFrozenCarReplan()
{
    var myPos = Agent.GlobalTransform.Origin;

    foreach (var kv in FrozenCarObstacles)
    {
        var other = kv.Key;
        if (other == this) continue;

        var otherPos = other.Agent.GlobalTransform.Origin;
        float dist = myPos.DistanceTo(otherPos);

        // ---- ENTERING FREEZE RADIUS ----
        if (dist < FreezeRadius)
        {
            // Only replan the FIRST time we see this frozen car
            if (!_frozenCarsAlreadyHandled.Contains(other))
            {
                _frozenCarsAlreadyHandled.Add(other);

                GD.Print($"[VehicleBrain] {Agent.Name} replanning (new frozen car: {other.Agent.Name})");

                if (DigState.State == DigState.TaskState.TravelingToDigSite)
                    PlanPathToDigSite(DigState.CurrentDigTarget, DigState.CurrentDigYaw);
                else if (DigState.State == DigState.TaskState.TravelingToDump)
                    PlanPathToDump();
            }
        }
        // ---- EXITING FREEZE / ENTERING UNFREEZE RADIUS ----
        else if (dist > UnfreezeRadius)
        {
            // Allow replanning again only after we fully leave the zone
            if (_frozenCarsAlreadyHandled.Contains(other))
            {
                _frozenCarsAlreadyHandled.Remove(other);
                GD.Print($"[VehicleBrain] {Agent.Name} untracked frozen car {other.Agent.Name}");
            }
        }
    }
}


        // ------------------------
// PRIORITY FREEZE / UNFREEZE HOOKS
// ------------------------

private bool _isPriorityFrozen = false;

public void FreezeForPriority()
{
    // already frozen → do nothing
    if (_isPriorityFrozen) return;

    _isPriorityFrozen = true;

    // Stop physics updates
    Agent.SetPhysicsProcess(false);

    // Register frozen obstacle in the world
    RegisterFrozenCarObstacle();

    // Replan only once during this frozen period
    if (!_hasReplannedFromFreeze)
    {
        _hasReplannedFromFreeze = true;

        GD.Print($"[VehicleBrain] {Agent.Name} frozen — triggering ONE replan.");

        // Trigger your replan function here
        CheckFrozenCarReplan();
        // or call whatever replanning method you prefer
    }
    else
    {
        GD.Print($"[VehicleBrain] {Agent.Name} frozen (already replanned, skipping).");
    }
}


public void UnfreezeFromPriority()
{
    if (!_isPriorityFrozen) return;

    _isPriorityFrozen = false;

    // Resume physics
    Agent.SetPhysicsProcess(true);

    // Remove frozen obstacle
    RemoveFrozenCarObstacle();

    // Allow replanning again on next freeze event
    _hasReplannedFromFreeze = false;

    GD.Print($"[VehicleBrain] {Agent.Name} unfrozen.");
}



        /// <summary>
        /// Request a new dig target from the scheduler using assigned sector.
        /// </summary>
        private void RequestNewDigTarget()
        {
            if (_scheduler == null || _terrain == null)
            {
                GD.PrintErr("[VehicleBrain] Cannot request new dig target - scheduler or terrain is null");
                return;
            }

            // Check if there's dirt to dig in our sector
            if (!HasDiggableTerrainInSector())
            {
                GD.Print($"[VehicleBrain] {Agent.Name} sector {SectorIndex} complete - no more dirt to dig");
                DigState.State = DigState.TaskState.Complete;
                return;
            }

            // Find best dig location in this robot's specific sector
            Vector3 digPos = FindBestDigInSector();

            // Check if we got a valid position (negative Y means failure from FindBestDigInSector)
            if (digPos.Y < 0f)
            {
                // No valid dig targets found in sector
                GD.Print($"[VehicleBrain] {Agent.Name} sector {SectorIndex} - no dig targets available. Marking complete.");
                DigState.State = DigState.TaskState.Complete;
                return;
            }

            // Calculate approach yaw (face towards center from dig site)
            Vector3 toCenter = (Vector3.Zero - digPos).WithY(0).Normalized();
            float approachYaw = Mathf.Atan2(toCenter.Z, toCenter.X);

            // Plan path to new dig target
            SetDigTarget(digPos, approachYaw);
            PlanPathToDigSite(digPos, approachYaw);

            GD.Print($"[VehicleBrain] {Agent.Name} (sector {SectorIndex}) assigned new dig target at ({digPos.X:F1}, {digPos.Z:F1}), height {digPos.Y:F2}m");
        }

        /// <summary>
        /// Find the best (highest) dig location within this robot's assigned sector.
        /// Avoids recently visited locations and prioritizes tallest points.
        /// </summary>
        private Vector3 FindBestDigInSector()
        {
            if (_terrain == null || _terrain.HeightGrid == null)
                return Vector3.Zero;

            float sectorStartAngle = SectorIndex * Mathf.Tau / _totalSectors;
            float sectorEndAngle = (SectorIndex + 1) * Mathf.Tau / _totalSectors;
            
            // Add small inward buffer to avoid sector boundary issues (shrink sector by 2 degrees on each side)
            float bufferAngle = Mathf.DegToRad(2f);
            sectorStartAngle += bufferAngle;
            sectorEndAngle -= bufferAngle;

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;
            
            Vector3 bestPos = Vector3.Zero;
            float bestScore = float.NegativeInfinity;
            int candidatesChecked = 0;
            int candidatesInSector = 0;

            // Calculate wall buffer zone
            float arenaRadius = _terrain.Radius;
            const float WallBufferMeters = 0.5f; // 0.5m wall buffer (user requested)
            float maxAllowedRadius = arenaRadius - WallBufferMeters;
            
            // Get obstacles from world state for manual checking
            const float ObstacleBufferMeters = 0.5f; // 0.5m obstacle buffer

            // Search terrain grid for best dig point in this sector
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float height = _terrain.HeightGrid[i, j];
                    
                    // Accept any height >= 0.2m
                    if (float.IsNaN(height) || height < 0.2f) continue;

                    // Convert grid indices to world position
                    float worldX = (i - resolution / 2f) * gridStep;
                    float worldZ = (j - resolution / 2f) * gridStep;
                    Vector3 candidate = new Vector3(worldX, height, worldZ);
                    
                    candidatesChecked++;

                    // Check if too close to wall (arena boundary)
                    float distFromCenter = candidate.WithY(0).Length();
                    if (distFromCenter > maxAllowedRadius)
                    {
                        // Too close to wall - skip this candidate
                        continue;
                    }

                    // Check if this point is in our sector using STRICT angle check
                    float angle = Mathf.Atan2(worldZ, worldX);
                    if (angle < 0) angle += Mathf.Tau; // Normalize to [0, 2π]
                    
                    // STRICT sector boundary check
                    bool inSector = IsAngleInSector(angle, sectorStartAngle, sectorEndAngle);
                    
                    if (!inSector) continue;
                    candidatesInSector++;

                    // MANUAL obstacle check - skip if inside obstacle buffer zone
                    bool tooCloseToObstacle = false;
                    if (_worldState?.Obstacles != null)
                    {
                        foreach (var obstacle in _worldState.Obstacles)
                        {
                            if (obstacle is CylinderObstacle cyl)
                            {
                                Vector2 candidateXZ = new Vector2(candidate.X, candidate.Z);
                                Vector2 obstacleXZ = new Vector2(cyl.GlobalPosition.X, cyl.GlobalPosition.Z);
                                float distToObstacle = candidateXZ.DistanceTo(obstacleXZ);
                                
                                // Check if inside obstacle + buffer
                                if (distToObstacle < (cyl.Radius + ObstacleBufferMeters))
                                {
                                    tooCloseToObstacle = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (tooCloseToObstacle) continue;

                    // SIMPLIFIED SCORING: Just prioritize height
                    Vector3 robotPos = Agent.GlobalTransform.Origin;
                    float distFromRobot = candidate.DistanceTo(robotPos);
                    
                    // Base score: height is most important (tallest points first)
                    float score = height * 10.0f;
                    
                    // Small penalty for being very far from robot (prefer closer targets when equal height)
                    score -= distFromRobot * 0.05f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidate;
                    }
                }
            }

            if (bestPos == Vector3.Zero || bestScore == float.NegativeInfinity)
            {
                GD.PrintErr($"[VehicleBrain] {Agent.Name} sector {SectorIndex}: NO VALID dig targets found! Checked {candidatesChecked} cells, {candidatesInSector} in sector");
                // Return a clearly invalid position so RequestNewDigTarget can handle it properly
                return new Vector3(0, -1f, 0); // Negative Y indicates failure
            }

            GD.Print($"[VehicleBrain] {Agent.Name} sector {SectorIndex}: checked {candidatesChecked} cells, {candidatesInSector} in sector, best height: {bestPos.Y:F2}m at ({bestPos.X:F1}, {bestPos.Z:F1})");

            // No longer track recent dig locations (removed per user request)

            return bestPos;
        }
        
        /// <summary>
        /// Check if an angle (in radians, [0, 2π]) is within a sector defined by start and end angles.
        /// Handles wraparound correctly.
        /// </summary>
        private bool IsAngleInSector(float angle, float sectorStart, float sectorEnd)
        {
            // Normalize all angles to [0, 2π]
            angle = NormalizeAngle(angle);
            sectorStart = NormalizeAngle(sectorStart);
            sectorEnd = NormalizeAngle(sectorEnd);
            
            if (sectorStart < sectorEnd)
            {
                // Normal case: sector doesn't wrap around
                return angle >= sectorStart && angle < sectorEnd;
            }
            else
            {
                // Wraparound case: sector crosses 0/2π boundary
                return angle >= sectorStart || angle < sectorEnd;
            }
        }
        
        /// <summary>
        /// Normalize angle to [0, 2π] range.
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            while (angle < 0) angle += Mathf.Tau;
            while (angle >= Mathf.Tau) angle -= Mathf.Tau;
            return angle;
        }

        /// <summary>
        /// Check if there's still diggable terrain in this robot's sector.
        /// </summary>
        private bool HasDiggableTerrainInSector()
        {
            if (_terrain == null || _terrain.HeightGrid == null) return false;

            float sectorStartAngle = SectorIndex * Mathf.Tau / _totalSectors;
            float sectorEndAngle = (SectorIndex + 1) * Mathf.Tau / _totalSectors;
            
            // Add small inward buffer (same as FindBestDigInSector)
            float bufferAngle = Mathf.DegToRad(2f);
            sectorStartAngle += bufferAngle;
            sectorEndAngle -= bufferAngle;

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;

            // Calculate wall buffer zone (0.5m)
            float arenaRadius = _terrain.Radius;
            const float WallBufferMeters = 0.5f; // 0.5m wall buffer
            float maxAllowedRadius = arenaRadius - WallBufferMeters;
            
            const float ObstacleBufferMeters = 0.5f; // 0.5m obstacle buffer

            // Check if any grid cell in our sector has significant height
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float height = _terrain.HeightGrid[i, j];
                    
                    // Accept any height >= 0.2m
                    if (float.IsNaN(height) || height < 0.2f) continue;

                    // Convert grid indices to world position
                    float worldX = (i - resolution / 2f) * gridStep;
                    float worldZ = (j - resolution / 2f) * gridStep;
                    
                    // Check if too close to wall
                    float distFromCenter = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
                    if (distFromCenter > maxAllowedRadius) continue;

                    // Check if this point is in our sector using STRICT angle check
                    float angle = Mathf.Atan2(worldZ, worldX);
                    if (angle < 0) angle += Mathf.Tau;
                    
                    bool inSector = IsAngleInSector(angle, sectorStartAngle, sectorEndAngle);
                    
                    if (!inSector) continue;
                    
                    // Manual obstacle check
                    bool tooCloseToObstacle = false;
                    if (_worldState?.Obstacles != null)
                    {
                        Vector3 candidate = new Vector3(worldX, height, worldZ);
                        foreach (var obstacle in _worldState.Obstacles)
                        {
                            if (obstacle is CylinderObstacle cyl)
                            {
                                Vector2 candidateXZ = new Vector2(candidate.X, candidate.Z);
                                Vector2 obstacleXZ = new Vector2(cyl.GlobalPosition.X, cyl.GlobalPosition.Z);
                                float distToObstacle = candidateXZ.DistanceTo(obstacleXZ);
                                
                                if (distToObstacle < (cyl.Radius + ObstacleBufferMeters))
                                {
                                    tooCloseToObstacle = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (tooCloseToObstacle) continue;
                    
                    return true; // Found diggable terrain in sector
                }
            }

            return false;
        }

        /// <summary>
        /// Plan path to dig site using the same path planner.
        /// </summary>
        private void PlanPathToDigSite(Vector3 digPos, float approachYaw)
        {
            if (_pathPlanner == null || Agent == null)
                return;

            var robotPos = Agent.GlobalTransform.Origin;
            var fwd = -Agent.GlobalTransform.Basis.Z;
            float startYaw = Mathf.Atan2(fwd.X, fwd.Z);
            
            // Create start pose
            var startPose = new Pose(robotPos.X, robotPos.Z, startYaw);
            
            // Create goal pose at dig site
            var goalPose = new Pose(digPos.X, digPos.Z, approachYaw);

            // Plan path using same planner
            PlannedPath path = _pathPlanner.Plan(startPose, goalPose, Agent.Spec, _worldState);

            // Draw the dig path (cyan/blue)
            if (_drawPathCallback != null)
            {
                _drawPathCallback(path.Points.ToArray(), new Color(0.15f, 0.9f, 1.0f, 0.8f));
            }

            // Set the path on the vehicle
            Agent.SetPath(path.Points.ToArray(), path.Gears.ToArray());
        }

        /// <summary>
        /// Check if there's still terrain to dig (simple height check).
        /// </summary>
        private bool HasRemainingTerrain()
        {
            if (_terrain == null || _terrain.HeightGrid == null) return false;
            
            // Check if any grid cell has significant height
            int resolution = _terrain.GridResolution;
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float height = _terrain.HeightGrid[i, j];
                    // Threshold for "significant" dirt - at least 30cm
                    if (!float.IsNaN(height) && height > 0.3f)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Perform excavation at current dig site.
        /// </summary>
        private void PerformDigging(float deltaSeconds)
        {
            _digTimeAccumulated += deltaSeconds;

            // REMOVED: Safety checks that were causing robots to skip valid dig targets
            // The dig target selection already filters for valid heights and positions

            // Dig at the target position - LowerArea handles mesh rebuild automatically
            float volumeDug = _digService.DigAtPosition(DigState.CurrentDigTarget, deltaSeconds);

            // Add to payload
            DigState.CurrentPayload += volumeDug;
            DigState.TotalDugVolume += volumeDug;

            // Visualize dig activity
            if (_digVisualizer != null)
            {
                _digVisualizer.DrawDigCone(DigState.CurrentDigTarget, 1.5f, 0.8f, new Color(1.0f, 0.5f, 0f, 0.5f));
                _digVisualizer.DrawPayloadBar(Agent.GlobalTransform.Origin, DigState.CurrentPayload, DigState.MaxPayload);
            }
        }

        /// <summary>
        /// Plan path back to dump center (origin) using the same path planner.
        /// </summary>
        private void PlanPathToDump()
        {
            if (_pathPlanner == null || Agent == null)
                return;

            var robotPos = Agent.GlobalTransform.Origin;
            var fwd = -Agent.GlobalTransform.Basis.Z;
            float startYaw = Mathf.Atan2(fwd.Z, fwd.X);
            
            // Create start pose
            var startPose = new Pose(robotPos.X, robotPos.Z, startYaw);
            
            // Dump at origin with approach from current direction
            var goalPose = new Pose(0f, 0f, startYaw);

            // Plan path using same planner
            PlannedPath path = _pathPlanner.Plan(startPose, goalPose, Agent.Spec, _worldState);

            // Draw the dump path (yellow/orange)
            if (_drawPathCallback != null)
            {
                _drawPathCallback(path.Points.ToArray(), new Color(1.0f, 0.8f, 0f, 0.8f));
            }

            // Set the path on the vehicle
            Agent.SetPath(path.Points.ToArray(), path.Gears.ToArray());
        }

        /// <summary>
        /// Set the dig target (called when scheduler assigns target).
        /// </summary>
        public void SetDigTarget(Vector3 digPos, float approachYaw)
        {
            DigState.CurrentDigTarget = digPos;
            DigState.CurrentDigYaw = approachYaw;
            DigState.State = DigState.TaskState.TravelingToDigSite;
        }
    }
}
