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

        /// <summary> This brain's robot index (matches SimulationDirector index) </summary>
        private int _robotIndex = -1;

        /// <summary> Total number of sectors (equal to number of robots) </summary>
        private int _totalSectors = 1;

        /// <summary> Cached reference to dig service (set by SimulationDirector) </summary>
        private DigService _digService = null!;

        /// <summary> Cached reference to terrain (set by SimulationDirector) </summary>
        private TerrainDisk _terrain = null!;

        /// <summary> Cached reference to scheduler (set by SimulationDirector) </summary>
        private RadialScheduler _scheduler = null!;

        /// <summary> Cached reference to path planner (set by SimulationDirector) </summary>
        private IPathPlanner _pathPlanner = null!;

        /// <summary> Cached reference to world state (set by SimulationDirector) </summary>
        private WorldState _worldState = null!;

        /// <summary> Dig configuration </summary>
        private DigConfig _digConfig = null!;

        /// <summary> Dig visualizer </summary>
        private DigVisualizer _digVisualizer = null!;

        /// <summary> Path drawing callback (set by SimulationDirector) </summary>
        private System.Action<int, Vector3[], Color> _drawPathCallback = null!;

        /// <summary> Accumulated dig time at current site (seconds) </summary>
        private float _digTimeAccumulated = 0f;

        /// <summary> Time since last check for better dig site (seconds) </summary>
        private float _timeSinceLastSiteCheck = 0f;

        /// <summary> How often to check for better dig sites (seconds) </summary>
        private const float SiteCheckInterval = 0.5f; // Check every 0.5 seconds

        /// <summary> Accumulated time since last terrain mesh update (seconds) </summary>
        private float _timeSinceLastMeshUpdate = 0f;

        /// <summary> How often to update terrain mesh while digging (seconds) </summary>
        private const float MeshUpdateInterval = 0.1f; // Update mesh 10 times per second instead of every frame

        /// <summary> Last known position of robot for stuck detection </summary>
        private Vector3 _lastPosition = Vector3.Zero;

        /// <summary> Time accumulated while robot hasn't moved significantly (seconds) </summary>
        private float _stuckTime = 0f;

        /// <summary> How long robot must be stuck before triggering recovery (seconds) </summary>
        private const float StuckTimeThreshold = 3.0f; // 3 seconds

        /// <summary> Minimum distance robot must move to be considered "moving" (meters) </summary>
        private const float MinMovementThreshold = 0.1f; // 10cm

        /// <summary> Distance to back up when stuck (meters) </summary>
        private const float BackupDistance = 2.0f; // 2 meters

        /// <summary> Number of consecutive stuck attempts at current target </summary>
        private int _stuckAttempts = 0;

        /// <summary> Maximum stuck attempts before abandoning current target </summary>
        private const int MaxStuckAttempts = 3;

        /// <summary> List of recently failed dig sites to avoid </summary>
        private System.Collections.Generic.List<Vector3> _failedDigSites = new();

        /// <summary> How long to remember failed sites (seconds) </summary>
        private const float FailedSiteMemoryTime = 30f;

        /// <summary> Time when each failed site was added </summary>
        private System.Collections.Generic.Dictionary<Vector3, float> _failedSiteTimes = new();

        /// <summary> Current game time for failed site tracking </summary>
        private float _gameTime = 0f;

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
        public void InitializeDigBrain(
            DigService digService, TerrainDisk terrain,
            RadialScheduler scheduler, DigConfig digConfig, IPathPlanner pathPlanner,
            WorldState worldState, DigVisualizer digVisualizer, System.Action<int, Vector3[], Color> drawPathCallback,
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

            // Assign permanent sector + robot index
            SectorIndex = sectorIndex;
            _robotIndex = sectorIndex;      // ← this matches the index passed from SimulationDirector
            _totalSectors = totalSectors;

            // Initialize position tracking for stuck detection
            if (Agent != null)
            {
                _lastPosition = Agent.GlobalTransform.Origin;
                GD.Print($"[VehicleBrain] {Agent.Name} assigned to sector {SectorIndex}/{_totalSectors}");
                GD.Print($"🤖 [VehicleBrain] {Agent.Name} received DigConfig: DigDepth={_digConfig.DigDepth:F2}m, DigRadius={_digConfig.DigRadius:F2}m, DepthRate={_digConfig.DepthRatePerSecond:F2}m/s (config object hash: {_digConfig.GetHashCode()})");
            }
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

            // Update game time for failed site tracking
            _gameTime += deltaSeconds;

            // Clean up old failed sites (older than 30 seconds)
            CleanupOldFailedSites();

            switch (DigState.State)
            {
                case DigState.TaskState.Idle:
                    // Request new dig target
                    RequestNewDigTarget();
                    _stuckTime = 0f; // Reset stuck timer when getting new target
                    _stuckAttempts = 0; // Reset stuck attempts for new target
                    _lastPosition = robotPos;
                    break;

                case DigState.TaskState.TravelingToDigSite:
                    // Check if robot is stuck (hasn't moved significantly)
                    float distanceMoved = robotPos.DistanceTo(_lastPosition);
                    if (distanceMoved < MinMovementThreshold)
                    {
                        // Robot hasn't moved much, accumulate stuck time
                        _stuckTime += deltaSeconds;

                        if (_stuckTime >= StuckTimeThreshold)
                        {
                            _stuckAttempts++;

                            if (_stuckAttempts >= MaxStuckAttempts)
                            {
                                // Too many attempts at this target - abandon it
                                GD.Print($"� [VehicleBrain] {Agent.Name} STUCK {_stuckAttempts} times at current target! Abandoning site and finding new target...");

                                // Mark current site as failed
                                MarkSiteAsFailed(DigState.CurrentDigTarget);

                                // Reset stuck counters
                                _stuckTime = 0f;
                                _stuckAttempts = 0;

                                // Find a completely new dig target
                                DigState.State = DigState.TaskState.Idle;
                                RequestNewDigTarget();
                                break;
                            }
                            else
                            {
                                // Try backing up and replanning with randomization
                                GD.Print($"🚨 [VehicleBrain] {Agent.Name} STUCK (attempt {_stuckAttempts}/{MaxStuckAttempts})! Backing up and replanning with offset...");

                                // Back up in reverse direction
                                var fwd = -Agent.GlobalTransform.Basis.Z;
                                Vector3 backupTarget = robotPos - (fwd * BackupDistance);

                                // Add random offset to avoid repeating same path
                                var random = new Random();
                                float randomOffsetX = (float)(random.NextDouble() - 0.5) * 2.0f; // ±1 meter
                                float randomOffsetZ = (float)(random.NextDouble() - 0.5) * 2.0f;
                                backupTarget += new Vector3(randomOffsetX, 0, randomOffsetZ);

                                // Move robot backwards
                                Agent.GlobalTransform = new Transform3D(Agent.GlobalTransform.Basis, backupTarget);

                                // Reset stuck timer
                                _stuckTime = 0f;
                                _lastPosition = backupTarget;

                                // Replan path with slightly different approach angle
                                float angleVariation = (float)(random.NextDouble() - 0.5) * 0.5f; // ±0.25 radians (~14 degrees)
                                float newApproachYaw = DigState.CurrentDigYaw + angleVariation;
                                DigState.CurrentDigYaw = newApproachYaw;

                                GD.Print($"🔄 [VehicleBrain] {Agent.Name} backed up with random offset, replanning with angle variation...");
                                PlanPathToDigSite(DigState.CurrentDigTarget, newApproachYaw);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Robot is moving, reset stuck timer (but keep attempt counter)
                        _stuckTime = 0f;
                        _lastPosition = robotPos;
                    }

                    // Check if arrived at dig site
                    float distToDig = robotPos.DistanceTo(DigState.CurrentDigTarget);
                    if (distToDig < _digConfig.AtSiteThreshold)
                    {
                        DigState.State = DigState.TaskState.Digging;
                        _digTimeAccumulated = 0f;
                        _timeSinceLastSiteCheck = 0f; // Reset site check timer when starting to dig
                        _stuckTime = 0f; // Reset stuck timer when arriving at site
                        _stuckAttempts = 0; // Reset stuck attempts on successful arrival

                        // Only reset volume tracker if this is a NEW site (not returning after dump)
                        if (DigState.CurrentSiteComplete)
                        {
                            // New site - reset everything
                            DigState.InitialDigHeight = GetTerrainHeightAt(DigState.CurrentDigTarget);
                            DigState.CurrentSiteVolumeExcavated = 0f;
                            DigState.CurrentSiteComplete = false;
                            GD.Print($"🎯 [VehicleBrain] {Agent.Name} arrived at NEW dig site, initial height: {DigState.InitialDigHeight:F2}m, TARGET DEPTH FROM CONFIG: {_digConfig.DigDepth:F2}m");
                        }
                        else
                        {
                            // Returning to same site after dump - keep volume tracker
                            GD.Print($"🔄 [VehicleBrain] {Agent.Name} RETURNED to dig site after dump, already excavated: {DigState.CurrentSiteVolumeExcavated:F3}m³, continuing...");
                        }
                    }
                    break;

                case DigState.TaskState.Digging:
                    // Dig one frame at current site FIRST
                    PerformDigging(deltaSeconds);

                    // CONDITION 1: Check if payload is full
                    if (DigState.IsPayloadFull)
                    {
                        GD.Print($"💼 [VehicleBrain] {Agent.Name} PAYLOAD FULL ({DigState.CurrentPayload:F2}/{DigState.MaxPayload:F2}m³), going to dump (will return to continue this site)");
                        // Site is NOT complete, just payload full - will return after dump
                        DigState.State = DigState.TaskState.TravelingToDump;
                        PlanPathToDump();
                        break;
                    }

                    // OPTIMIZATION: Only check site completion every 0.5 seconds instead of every frame
                    _timeSinceLastSiteCheck += deltaSeconds;
                    if (_timeSinceLastSiteCheck >= SiteCheckInterval)
                    {
                        _timeSinceLastSiteCheck = 0f;

                        // Calculate cylinder volume: Volume = DigDepth × π × DigRadius²
                        // But we can't dig below the floor, so use effective depth
                        float floorY = _terrain?.FloorY ?? 0f;
                        float targetHeightBasedOnDepth = DigState.InitialDigHeight - _digConfig.DigDepth;
                        float effectiveTargetHeight = Mathf.Max(targetHeightBasedOnDepth, floorY);
                        float effectiveTargetDepth = DigState.InitialDigHeight - effectiveTargetHeight;
                        float cylinderVolume = effectiveTargetDepth * Mathf.Pi * _digConfig.DigRadius * _digConfig.DigRadius;

                        // Check if there's no more dirt at the site (all terrain in radius at floor level)
                        float maxHeight = GetMaxHeightInRadius(DigState.CurrentDigTarget, _digConfig.DigRadius);
                        float centerHeight = GetTerrainHeightAt(DigState.CurrentDigTarget);
                        bool noMoreDirt = centerHeight <= (floorY + 0.001f); // Within 1cm of floor

                        // Debug: Log site completion check
                        float volumePercent = cylinderVolume > 0 ? (DigState.CurrentSiteVolumeExcavated / cylinderVolume * 100f) : 0f;
                        GD.Print($"⛏️ [VehicleBrain] {Agent.Name} digging: maxH={maxHeight:F3}m, centerH={centerHeight:F3}m, floor={floorY:F3}m | volume={DigState.CurrentSiteVolumeExcavated:F3}/{cylinderVolume:F3}m³ ({volumePercent:F0}%) | payload={DigState.CurrentPayload:F2}/{DigState.MaxPayload:F2}m³");

                        // Site is ONLY complete when there's no more dirt (removed cylinder volume check)
                        // Robots must dig until all dirt in the radius is at floor level
                        if (noMoreDirt)
                        {
                            // Mark this site as COMPLETE
                            DigState.CurrentSiteComplete = true;
                            GD.Print($"✅ [VehicleBrain] {Agent.Name} SITE COMPLETE! All terrain at floor (maxH={maxHeight:F3}m ≈ floor={floorY:F3}m). Excavated {DigState.CurrentSiteVolumeExcavated:F3}m³. Moving to next site!");

                            // Find next dig site (will dump first if payload > 0)
                            RequestNewDigTarget();
                            break;
                        }
                    }

                    // Continue digging at current site
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
                    // Unload payload
                    DigState.DumpCount++;
                    float dumpedAmount = DigState.CurrentPayload;
                    DigState.CurrentPayload = 0f;

                    GD.Print($"📦 [VehicleBrain] {Agent.Name} dumped {dumpedAmount:F2}m³ (dump #{DigState.DumpCount})");

                    // Check if there's still dirt to dig globally
                    if (!HasRemainingTerrain())
                    {
                        DigState.State = DigState.TaskState.Complete;
                        GD.Print($"🏁 [VehicleBrain] {Agent.Name} ARENA COMPLETE - no dirt remaining anywhere in arena!");
                        break;
                    }

                    // If current site is NOT complete, return to it. Otherwise find new site.
                    if (!DigState.CurrentSiteComplete)
                    {
                        // Return to same dig site to continue excavation
                        GD.Print($"🔄 [VehicleBrain] {Agent.Name} returning to SAME dig site (already excavated {DigState.CurrentSiteVolumeExcavated:F3}m³)");
                        DigState.State = DigState.TaskState.TravelingToDigSite;
                        PlanPathToDigSite(DigState.CurrentDigTarget, DigState.CurrentDigYaw);
                    }
                    else
                    {
                        // Current site is complete, find a new one
                        GD.Print($"🎯 [VehicleBrain] {Agent.Name} previous site complete, finding NEW dig site");
                        DigState.State = DigState.TaskState.Idle;
                        RequestNewDigTarget();
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
                GD.Print($"🏁 [VehicleBrain] {Agent.Name} SECTOR {SectorIndex} COMPLETE - no dirt remaining in assigned sector");
                DigState.State = DigState.TaskState.Complete;
                return;
            }

            // Find best dig location in this robot's specific sector
            Vector3 digPos = FindBestDigInSector();

            // Check if we got a valid position (negative Y means failure from FindBestDigInSector)
            if (digPos.Y < 0f)
            {
                // No valid dig targets found in sector
                GD.Print($"🏁 [VehicleBrain] {Agent.Name} SECTOR {SectorIndex} COMPLETE - no valid dig targets available (all blocked or too shallow)");
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

            // Removed sector buffer - robots should be able to work all the way to sector boundaries
            // (Previous 2-degree buffer was creating unreachable dirt in boundary zones)

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;

            Vector3 bestPos = Vector3.Zero;
            float bestScore = float.NegativeInfinity;
            int candidatesChecked = 0;
            int candidatesInSector = 0;

            // Calculate wall buffer zone
            float arenaRadius = _terrain.Radius;
            // Wall buffer accounts for: vehicle width (~0.5m) + turn radius (1.0m) + safety margin
            // Must match path planner's wall buffer to ensure selected dig sites are pathable
            const float WallBufferMeters = 0.1f; // Must match HybridReedsSheppPlanner.PathIsValid buffer
            float maxAllowedRadius = arenaRadius - WallBufferMeters;

            // Get obstacles from world state for manual checking
            const float ObstacleBufferMeters = 0.5f; // 0.5m obstacle buffer

            // Search terrain grid for best dig point in this sector
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    // Convert grid indices to world position
                    float worldX = (i - resolution / 2f) * gridStep;
                    float worldZ = (j - resolution / 2f) * gridStep;
                    Vector3 candidateXZ = new Vector3(worldX, 0, worldZ);

                    float centerHeight = GetTerrainHeightAt(candidateXZ);



                    Vector3 candidate = new Vector3(worldX, centerHeight, worldZ);

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
                                Vector2 candidate2D = new Vector2(candidate.X, candidate.Z);
                                Vector2 obstacleXZ = new Vector2(cyl.GlobalPosition.X, cyl.GlobalPosition.Z);
                                float distToObstacle = candidate2D.DistanceTo(obstacleXZ);

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

                    float score = GetDigSiteScore(candidate);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidate;
                    }

                }
            }

            if (bestPos == Vector3.Zero || bestScore == float.NegativeInfinity)
            {
                GD.PrintErr($"[VehicleBrain] {Agent.Name} sector {SectorIndex}: NO VALID dig targets found! Checked {candidatesChecked} cells, {candidatesInSector} in sector (using max height in {_digConfig.DigRadius:F2}m radius)");
                // Return a clearly invalid position so RequestNewDigTarget can handle it properly
                return new Vector3(0, -1f, 0); // Negative Y indicates failure
            }

            GD.Print($"[VehicleBrain] {Agent.Name} sector {SectorIndex}: checked {candidatesChecked} cells, {candidatesInSector} in sector, best MAX height: {bestPos.Y:F2}m at ({bestPos.X:F1}, {bestPos.Z:F1}) (dig radius: {_digConfig.DigRadius:F2}m)");

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

            // Removed sector buffer - robots should be able to work all the way to sector boundaries
            // (Previous 2-degree buffer was creating unreachable dirt in boundary zones)

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;

            // Calculate wall buffer zone (0.5m)
            float arenaRadius = _terrain.Radius;
            // Wall buffer must match the path planner's buffer (0.5m) to ensure all selected sites are pathable
            const float WallBufferMeters = 0.1f; // Must match HybridReedsSheppPlanner.PathIsValid buffer
            float maxAllowedRadius = arenaRadius - WallBufferMeters;

            const float ObstacleBufferMeters = 0.5f; // 0.5m obstacle buffer

            // Check if any grid cell in our sector has significant height
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float height = _terrain.HeightGrid[i, j];

                    // Accept dirt > 0cm (same threshold as HasRemainingTerrain for consistency)
                    if (float.IsNaN(height) || height <= 0.01f) continue;

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
            float startYaw = Mathf.Atan2(fwd.Z, fwd.X);

            // Create start pose
            var startPose = new Pose(robotPos.X, robotPos.Z, startYaw);

            // Create goal pose at dig site
            var goalPose = new Pose(digPos.X, digPos.Z, approachYaw);

            // Plan path using same planner
            PlannedPath path = _pathPlanner.Plan(startPose, goalPose, Agent.Spec, _worldState);

            // Draw the dig path (cyan/blue)
            if (_drawPathCallback != null)
            {
                _drawPathCallback(_robotIndex, path.Points.ToArray(), new Color(0.15f, 0.9f, 1.0f, 0.8f));
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
                    // Threshold for "significant" dirt - lowered to 0cm to catch any remaining dirt
                    // (was 30cm which caused robots to declare complete while dirt remained)
                    if (!float.IsNaN(height) && height > 0.01f)
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
            var position = Agent.GlobalTransform.Origin;
            float remainingCapacity = MathF.Max(0f, DigState.MaxPayload - DigState.CurrentPayload);

            var (swelledVolume, inSituVolume) = _digService.DigAtPosition(position, deltaSeconds, remainingCapacity);

            DigState.CurrentPayload += swelledVolume;
            DigState.CurrentSiteVolumeExcavated += inSituVolume;

            if (DigState.IsPayloadFull)
            {
                GD.Print($"[VehicleBrain] {Agent.Name} payload full, going to dump");
                DigState.State = DigState.TaskState.TravelingToDump;
                PlanPathToDump();
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
                _drawPathCallback(_robotIndex, path.Points.ToArray(), new Color(1.0f, 0.8f, 0f, 0.8f));
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

        /// <summary>
        /// Get the current terrain height at a specific location.
        /// </summary>
        private float GetTerrainHeightAt(Vector3 position)
        {
            if (_terrain == null || _terrain.HeightGrid == null)
                return 0f;

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;

            // Convert world position to grid indices
            int i = (int)((position.X / gridStep) + resolution / 2f);
            int j = (int)((position.Z / gridStep) + resolution / 2f);

            // Check bounds
            if (i < 0 || i >= resolution || j < 0 || j >= resolution)
                return 0f;

            float height = _terrain.HeightGrid[i, j];
            return float.IsNaN(height) ? 0f : height;
        }

        /// <summary>
        /// Get the minimum (lowest) height within a circular radius around the position.
        /// This ensures we check if ALL dirt in the dig area has been removed.
        /// </summary>
        private float GetMaxHeightInRadius(Vector3 center, float radius)
        {
            if (_terrain == null || _terrain.HeightGrid == null)
                return 0f;

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;
            float maxHeight = float.MinValue;
            bool foundAny = false;

            // Exclude the rim by shrinking the radius slightly
            float innerRadius = MathF.Max(0f, radius - gridStep * 0.5f);


            // Calculate grid cell range to check
            int centerI = (int)((center.X / gridStep) + resolution / 2f);
            int centerJ = (int)((center.Z / gridStep) + resolution / 2f);
            int cellRadius = (int)(innerRadius / gridStep) + 1;

            for (int di = -cellRadius; di <= cellRadius; di++)
            {
                for (int dj = -cellRadius; dj <= cellRadius; dj++)
                {
                    int i = centerI + di;
                    int j = centerJ + dj;

                    // Check bounds
                    if (i < 0 || i >= resolution || j < 0 || j >= resolution)
                        continue;

                    // Check if within circular radius
                    float worldX = (i - resolution / 2f) * gridStep;
                    float worldZ = (j - resolution / 2f) * gridStep;
                    float distFromCenter = Mathf.Sqrt(
                        (worldX - center.X) * (worldX - center.X) +
                        (worldZ - center.Z) * (worldZ - center.Z)
                    );

                    if (distFromCenter > radius)
                        continue;

                    float height = _terrain.HeightGrid[i, j];
                    if (!float.IsNaN(height))
                    {
                        maxHeight = Mathf.Max(maxHeight, height);
                        foundAny = true;
                    }
                }
            }

            return foundAny ? maxHeight : 0f;
        }

        /// <summary>
        /// Get the average height within a circular radius around the position.
        /// This is used to select dig sites that actually have dirt to excavate.
        /// Returns 0 if no valid cells found.
        /// </summary>
        private float GetAverageHeightInRadius(Vector3 center, float radius)
        {
            if (_terrain == null || _terrain.HeightGrid == null)
                return 0f;

            int resolution = _terrain.GridResolution;
            float gridStep = _terrain.GridStep;
            float sumHeight = 0f;
            int count = 0;

            // Calculate grid cell range to check
            int centerI = (int)((center.X / gridStep) + resolution / 2f);
            int centerJ = (int)((center.Z / gridStep) + resolution / 2f);
            int cellRadius = (int)(radius / gridStep) + 1;

            for (int di = -cellRadius; di <= cellRadius; di++)
            {
                for (int dj = -cellRadius; dj <= cellRadius; dj++)
                {
                    int i = centerI + di;
                    int j = centerJ + dj;

                    // Check bounds
                    if (i < 0 || i >= resolution || j < 0 || j >= resolution)
                        continue;

                    // Check if within circular radius
                    float worldX = (i - resolution / 2f) * gridStep;
                    float worldZ = (j - resolution / 2f) * gridStep;
                    float distFromCenter = Mathf.Sqrt(
                        (worldX - center.X) * (worldX - center.X) +
                        (worldZ - center.Z) * (worldZ - center.Z)
                    );

                    if (distFromCenter > radius)
                        continue;

                    float height = _terrain.HeightGrid[i, j];
                    if (!float.IsNaN(height))
                    {
                        sumHeight += height;
                        count++;
                    }
                }
            }

            return count > 0 ? (sumHeight / count) : 0f;
        }

        /// <summary>
        /// Calculate dig site score based on height and distance from robot.
        /// </summary>
        private float GetDigSiteScore(Vector3 digSite)
        {
            Vector3 robotPos = Agent.GlobalTransform.Origin;

            // Horizontal distance only (ignore height difference)
            float dx = digSite.X - robotPos.X;
            float dz = digSite.Z - robotPos.Z;
            float distFromRobot = Mathf.Sqrt(dx * dx + dz * dz);

            float height = digSite.Y;

            // Height should dominate; distance is a small penalty.
            const float HeightWeight = 10.0f;
            const float DistanceWeight = 0.05f;

            float score = height * HeightWeight - distFromRobot * DistanceWeight;

            return score;
        }

        /// <summary>
        /// Mark a dig site as failed/unreachable and remember it for a while.
        /// </summary>
        private void MarkSiteAsFailed(Vector3 site)
        {
            if (!_failedDigSites.Contains(site))
            {
                _failedDigSites.Add(site);
                _failedSiteTimes[site] = _gameTime;
                GD.Print($"❌ [VehicleBrain] {Agent.Name} marked site ({site.X:F1}, {site.Z:F1}) as FAILED/UNREACHABLE");
            }
        }

        /// <summary>
        /// Remove failed sites that are older than the memory time.
        /// </summary>
        private void CleanupOldFailedSites()
        {
            // Remove sites older than 30 seconds
            _failedDigSites.RemoveAll(site =>
            {
                if (_failedSiteTimes.TryGetValue(site, out float addTime))
                {
                    if (_gameTime - addTime > FailedSiteMemoryTime)
                    {
                        _failedSiteTimes.Remove(site);
                        return true; // Remove from list
                    }
                }
                return false;
            });
        }
    }
}
