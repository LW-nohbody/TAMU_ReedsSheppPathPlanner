using Godot;
using System.Collections.Generic;
using DigSim3D.Services;
using DigSim3D.Domain;

namespace DigSim3D.App
{
    /// <summary>
    /// Enhanced VehicleBrain with continuous dig cycles, payload management, and coordinator integration.
    /// States: Idle -> MovingToDig -> Digging -> MovingToDump -> Dumping -> (repeat)
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
        [Export] public float MaxPayload { get; set; } = 10f;
        [Export] public float DigRatePerSec { get; set; } = 2f;
        [Export] public float DigDuration { get; set; } = 3f;
        public float CurrentPayload { get; private set; } = 0f;
        public float TotalPayloadDelivered { get; private set; } = 0f;
        
        // Current targets
        public Vector3 CurrentDigTarget { get; private set; }
        public Vector3 DumpLocation { get; set; }
        
        // Timing
        private double _digTimer = 0;
        
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
            // Find highest point in sector
            Vector3 digTarget = Coordinator.GetBestDigPoint(
                RobotId, Terrain, ThetaMin, ThetaMax, MaxRadius);
            
            // Try to claim it
            if (Coordinator.ClaimDigSite(RobotId, digTarget, 0.5f))
            {
                CurrentDigTarget = digTarget;
                
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
            // Check if arrived (vehicle handles movement)
            if (Agent != null && IsAtTarget(Agent.GlobalPosition, CurrentDigTarget, 0.3f))
            {
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
            // Check if arrived at dump
            if (Agent != null && IsAtTarget(Agent.GlobalPosition, DumpLocation, 0.5f))
            {
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

        private bool IsAtTarget(Vector3 current, Vector3 target, float threshold)
        {
            var currentXZ = new Vector3(current.X, 0, current.Z);
            var targetXZ = new Vector3(target.X, 0, target.Z);
            return currentXZ.DistanceTo(targetXZ) < threshold;
        }
    }
}
