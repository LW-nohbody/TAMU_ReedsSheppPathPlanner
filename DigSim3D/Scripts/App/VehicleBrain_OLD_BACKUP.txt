using Godot;
using System.Collections.Generic;
using DigSim3D.Services;
using DigSim3D.Domain;

namespace DigSim3D.App
{
    /// <summary>
    /// Enhanced VehicleBrain with stuck detection, terrain gradient sensing, and recovery strategies.
    /// States: Idle -> MovingToDig -> Digging -> MovingToDump -> Dumping -> (repeat)
    /// 
    /// Reactive Swarm Dig Logic:
    /// - Detects stuck situations (position not changing for 30 frames)
    /// - Samples terrain gradient ahead during movement
    /// - Progressive recovery: avoidance → escape → surrender
    /// - Tracks failed dig sites with grace period memory
    /// </summary>
    public partial class VehicleBrain : Node
    {
        // References
        public VehicleVisualizer Agent { get; private set; } = null!;
        public int RobotId { get; set; }
        public Color SectorColor { get; set; } = Colors.White;
        
        // Sector assignment (radial slice)
        public float ThetaMin { get; set; }
        public float ThetaMax { get; set; }
        public float MaxRadius { get; set; } = 15f;
        
        // State machine
        public enum State { Idle, MovingToDig, Digging, MovingToDump, Dumping }
        public State CurrentState { get; private set; } = State.Idle;
        
        // Payload tracking
        [Export] public float MaxPayload { get; set; } = 25f;
        [Export] public float DigRatePerSec { get; set; } = 2f;
        [Export] public float DigDuration { get; set; } = 3f;
        public float CurrentPayload { get; private set; } = 0f;
        public float TotalPayloadDelivered { get; private set; } = 0f;
        
        // Current targets
        public Vector3 CurrentDigTarget { get; private set; }
        public Vector3 DumpLocation { get; set; }
        
        // Timing
        private double _digTimer = 0;
        
        // Stuck detection and recovery
        private Vector3 _lastPositionCheck = Vector3.Zero;
        private int _framesSinceMovement = 0;
        private int _recoveryAttemptCount = 0;
        private List<Vector3> _recentFailedSites = new();
        private int _failureTimeRemaining = 0;
        
        // Configuration
        private const float StuckMovementThreshold = 0.3f;  // meters
        private const int StuckFrameThreshold = 30;         // frames before declaring stuck
        private const float TerrainGradientLimit = 0.2f;    // radians (~11°)
        private const int FailureMemoryDuration = 60;       // frames
        private const float FailureProximity = 0.5f;        // meters (avoid this distance)
        
        // Services (set by SimulationDirector)
        public TerrainDisk Terrain { get; set; } = null!;
        public RobotCoordinator Coordinator { get; set; } = null!;
        public TargetIndicator TargetIndicator { get; set; } = null!;
        public PathVisualizer PathVisualizer { get; set; } = null!;
        public PlannedPathVisualizer PlannedPathVisualizer { get; set; } = null!;
        
        // RS path params (set by director)
        public float TurnRadius { get; set; } = 2f;
        public float SampleStep { get; set; } = 0.25f;
        public List<Obstacle3D> Obstacles { get; set; } = new();
        public float ObstacleBuffer { get; set; } = 0.5f;

        public override void _Ready()
        {
            Agent = GetParent<VehicleVisualizer>();
        }

        public override void _Process(double delta)
        {
            // Update path visualization
            if (PathVisualizer != null && Agent != null)
            {
                PathVisualizer.AddPoint(RobotId, Agent.GlobalPosition);
            }
            
            switch (CurrentState)
            {
                case State.Idle:
                    ProcessIdle();
                    break;
                    
                case State.MovingToDig:
                    ProcessMovingToDig();
                    break;
                    
                case State.Digging:
                    ProcessDigging(delta);
                    break;
                    
                case State.MovingToDump:
                    ProcessMovingToDump();
                    break;
                    
                case State.Dumping:
                    ProcessDumping(delta);
                    break;
            }
        }

        private void ProcessIdle()
        {
            // Decay failure timer
            if (_failureTimeRemaining > 0)
            {
                _failureTimeRemaining--;
                if (_failureTimeRemaining <= 0)
                {
                    _recentFailedSites.Clear();
                    GD.Print($"[Robot_{RobotId}] Failure memory cleared");
                }
            }
            
            // Find highest point in sector
            Vector3 digTarget = Coordinator.GetBestDigPoint(
                RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius);
            
            // Skip if in recent failures
            foreach (var failed in _recentFailedSites)
            {
                if (digTarget.DistanceTo(failed) < FailureProximity)
                {
                    // Get alternative site
                    digTarget = Coordinator.GetSafeAlternative(
                        RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius, failed);
                    GD.Print($"[Robot_{RobotId}] Avoiding failed site, trying alternative");
                    break;
                }
            }
            
            // Try to claim it
            if (Coordinator.ClaimDigSite(RobotId, digTarget, 0.5f))
            {
                CurrentDigTarget = digTarget;
                _recoveryAttemptCount = 0;
                _lastPositionCheck = Agent.GlobalPosition;
                _framesSinceMovement = 0;
                
                // Show target indicator
                if (TargetIndicator != null)
                {
                    TargetIndicator.ShowIndicator(RobotId, digTarget, SectorColor);
                }
                
                // Plan path to dig site
                PlanPathToDig(digTarget);
                CurrentState = State.MovingToDig;
            }
        }

        private void ProcessMovingToDig()
        {
            if (Agent == null) return;
            
            // Check if stuck
            if (IsStuck())
            {
                GD.Print($"[Robot_{RobotId}] STUCK for {_framesSinceMovement} cycles at {Agent.GlobalPosition}. Attempt #{_recoveryAttemptCount + 1}");
                RecoverFromStuck();
                return;
            }
            
            // Check terrain gradient ahead
            float gradient = SampleTerrainGradientAhead(Agent.GlobalPosition, CurrentDigTarget);
            if (gradient > TerrainGradientLimit)
            {
                GD.Print($"[Robot_{RobotId}] Steep terrain detected (gradient={gradient:F3}). Triggering avoidance recovery");
                RecoverFromSteepTerrain();
                return;
            }
            
            // Check if arrived (vehicle handles movement)
            if (IsAtTarget(Agent.GlobalPosition, CurrentDigTarget, 0.3f))
            {
                _recoveryAttemptCount = 0;
                CurrentState = State.Digging;
                _digTimer = 0;
            }
        }

        private void ProcessDigging(double delta)
        {
            _digTimer += delta;
            
            // Accumulate payload
            float digAmount = DigRatePerSec * (float)delta;
            CurrentPayload = Mathf.Min(CurrentPayload + digAmount, MaxPayload);
            
            // Modify terrain (visual effect - lower height slightly)
            if (Terrain != null)
            {
                Terrain.ModifyHeight(CurrentDigTarget, -digAmount * 0.01f, 1.0f);
            }
            
            // Done digging?
            if (_digTimer >= DigDuration || CurrentPayload >= MaxPayload)
            {
                // Release claim
                Coordinator.ReleaseClaim(RobotId);
                
                // Hide target indicator
                if (TargetIndicator != null)
                {
                    TargetIndicator.HideIndicator(RobotId);
                }
                
                // Head to dump
                PlanPathToDump();
                CurrentState = State.MovingToDump;
            }
        }

        private void ProcessMovingToDump()
        {
            if (Agent == null) return;
            
            // Check if stuck (more forgiving for dump movement)
            if (IsStuck())
            {
                GD.Print($"[Robot_{RobotId}] STUCK during dump travel at {Agent.GlobalPosition}. Attempting recovery");
                RecoverFromStuck();
                return;
            }
            
            // Check if arrived at dump
            if (IsAtTarget(Agent.GlobalPosition, DumpLocation, 0.5f))
            {
                _recoveryAttemptCount = 0;
                CurrentState = State.Dumping;
                _digTimer = 0;
            }
        }

        private void ProcessDumping(double delta)
        {
            _digTimer += delta;
            
            // Dump payload over time
            float dumpedAmount = Mathf.Min(CurrentPayload, DigRatePerSec * (float)delta * 2f);
            CurrentPayload -= dumpedAmount;
            TotalPayloadDelivered += dumpedAmount;
            
            // Done dumping?
            if (_digTimer >= 2f || CurrentPayload <= 0)
            {
                CurrentPayload = 0;
                
                // Check if sector still has work
                if (Coordinator.HasWorkInSector(ThetaMin, ThetaMax, MaxRadius, Terrain))
                {
                    CurrentState = State.Idle;
                }
                else
                {
                    // Sector complete - could idle or get reassigned
                    CurrentState = State.Idle;
                }
            }
        }

        private void PlanPathToDig(Vector3 target)
        {
            if (Agent == null) return;
            
            var start = Agent.GlobalPosition;
            var fwd = -Agent.GlobalTransform.Basis.Z;
            double startYaw = Mathf.Atan2(fwd.Z, fwd.X);
            
            // Calculate approach angle toward center for better dig positioning
            Vector3 toCenter = -target.Normalized();
            double targetYaw = Mathf.Atan2(toCenter.Z, toCenter.X);
            
            var (pts, gears) = HybridPlanner.Plan(
                start, startYaw,
                target, targetYaw,
                TurnRadius, SampleStep,
                Obstacles,
                ObstacleBuffer);
            
            if (pts != null && pts.Count > 0)
            {
                Agent.SetPath(pts.ToArray(), gears?.ToArray() ?? System.Array.Empty<int>());
                
                // Update planned path visualization
                if (PlannedPathVisualizer != null)
                {
                    PlannedPathVisualizer.UpdatePath(RobotId, pts, gears ?? new List<int>());
                }
            }
        }

        private void PlanPathToDump()
        {
            if (Agent == null) return;
            
            var start = Agent.GlobalPosition;
            var fwd = -Agent.GlobalTransform.Basis.Z;
            double startYaw = Mathf.Atan2(fwd.Z, fwd.X);
            
            // Approach dump from any angle
            double targetYaw = 0;
            
            var (pts, gears) = HybridPlanner.Plan(
                start, startYaw,
                DumpLocation, targetYaw,
                TurnRadius, SampleStep,
                Obstacles,
                ObstacleBuffer);
            
            if (pts != null && pts.Count > 0)
            {
                Agent.SetPath(pts.ToArray(), gears?.ToArray() ?? System.Array.Empty<int>());
                
                // Update planned path visualization
                if (PlannedPathVisualizer != null)
                {
                    PlannedPathVisualizer.UpdatePath(RobotId, pts, gears ?? new List<int>());
                }
            }
        }

        /// <summary>
        /// Check if robot is stuck (not moving for N frames)
        /// </summary>
        private bool IsStuck()
        {
            if (Agent == null) return false;
            
            float distMoved = Agent.GlobalPosition.DistanceTo(_lastPositionCheck);
            
            if (distMoved > StuckMovementThreshold)
            {
                // Good movement, reset counter
                _lastPositionCheck = Agent.GlobalPosition;
                _framesSinceMovement = 0;
                return false;
            }
            
            // Not moving enough
            _framesSinceMovement++;
            return _framesSinceMovement > StuckFrameThreshold;
        }

        /// <summary>
        /// Sample terrain gradient in direction of travel (forward look)
        /// Returns max slope detected from start to target
        /// </summary>
        private float SampleTerrainGradientAhead(Vector3 from, Vector3 to)
        {
            if (Terrain == null) return 0f;
            
            Vector3 direction = (to - from).Normalized();
            float distance = from.DistanceTo(to);
            float lookAhead = Mathf.Min(distance * 0.5f, 3.0f); // Look 50% of path or 3m max
            
            // Sample 3 points: current, mid, end
            float maxGradient = 0f;
            
            Vector3[] samplePoints = new[]
            {
                from,
                from + direction * lookAhead * 0.5f,
                from + direction * lookAhead
            };
            
            float prevHeight = 0f;
            bool firstSample = true;
            
            foreach (var pt in samplePoints)
            {
                if (Terrain.SampleHeightNormal(pt, out var hitPos, out _))
                {
                    float currentHeight = hitPos.Y;
                    
                    if (!firstSample)
                    {
                        float heightDiff = Mathf.Abs(currentHeight - prevHeight);
                        float sampleDist = lookAhead / 2f;
                        float gradient = Mathf.Atan2(heightDiff, sampleDist);
                        maxGradient = Mathf.Max(maxGradient, gradient);
                    }
                    
                    prevHeight = currentHeight;
                    firstSample = false;
                }
            }
            
            return maxGradient;
        }

        /// <summary>
        /// Recover from steep terrain: try alternative dig site
        /// </summary>
        private void RecoverFromSteepTerrain()
        {
            _recoveryAttemptCount++;
            
            // Try to get alternative target
            Vector3 alt = Coordinator.GetSafeAlternative(
                RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius, CurrentDigTarget);
            
            if (alt != CurrentDigTarget)
            {
                GD.Print($"[Robot_{RobotId}] Switching to alternative dig site");
                Coordinator.ReleaseClaim(RobotId);
                CurrentDigTarget = alt;
                PlanPathToDig(alt);
                _recoveryAttemptCount = 0;
            }
            else
            {
                GD.Print($"[Robot_{RobotId}] No safe alternative found, surrendering dig site");
                RecoverSurrenderAndDump();
            }
        }

        /// <summary>
        /// Recover from being stuck: try alternative or surrender
        /// </summary>
        private void RecoverFromStuck()
        {
            _recoveryAttemptCount++;
            
            if (_recoveryAttemptCount < 2)
            {
                // Level 1: Try alternative dig site
                GD.Print($"[Robot_{RobotId}] Stuck recovery - Level 1: Trying alternative target");
                Vector3 alt = Coordinator.GetSafeAlternative(
                    RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius, CurrentDigTarget);
                
                if (alt != CurrentDigTarget)
                {
                    Coordinator.ReleaseClaim(RobotId);
                    CurrentDigTarget = alt;
                    PlanPathToDig(alt);
                    _lastPositionCheck = Agent.GlobalPosition;
                    _framesSinceMovement = 0;
                    return;
                }
            }
            
            // Level 2/3: Mark as failed and go dump
            GD.Print($"[Robot_{RobotId}] Stuck recovery - Level 3: Surrendering site");
            RecoverSurrenderAndDump();
        }

        /// <summary>
        /// Surrender current dig site and head to dump (Level 3 recovery)
        /// </summary>
        private void RecoverSurrenderAndDump()
        {
            _recentFailedSites.Add(CurrentDigTarget);
            _failureTimeRemaining = FailureMemoryDuration;
            _recoveryAttemptCount = 0;
            
            Coordinator.ReleaseClaim(RobotId);
            
            if (TargetIndicator != null)
            {
                TargetIndicator.HideIndicator(RobotId);
            }
            
            GD.Print($"[Robot_{RobotId}] Marked dig site as failed, heading to dump");
            PlanPathToDump();
            CurrentState = State.MovingToDump;
        }

        private bool IsAtTarget(Vector3 current, Vector3 target, float threshold)
        {
            var currentXZ = new Vector3(current.X, 0, current.Z);
            var targetXZ = new Vector3(target.X, 0, target.Z);
            return currentXZ.DistanceTo(targetXZ) < threshold;
        }
    }
}
