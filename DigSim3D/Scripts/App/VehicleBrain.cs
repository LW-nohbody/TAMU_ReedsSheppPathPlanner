using Godot;
using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    public partial class VehicleBrain : Node
    {
        public VehicleVisualizer Agent { get; private set; } = null!;
        public DigState DigState { get; private set; } = new();

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

        public override void _Ready()
        {
            Agent = GetParent<VehicleVisualizer>();
        }

        /// <summary>
        /// Initialize dig brain with external services.
        /// </summary>
        public void InitializeDigBrain(DigService digService, TerrainDisk terrain, 
            RadialScheduler scheduler, DigConfig digConfig, HybridReedsSheppPlanner pathPlanner,
            WorldState worldState, DigVisualizer digVisualizer, System.Action<Vector3[], Color> drawPathCallback)
        {
            _digService = digService;
            _terrain = terrain;
            _scheduler = scheduler;
            _digConfig = digConfig;
            _pathPlanner = pathPlanner;
            _worldState = worldState;
            _digVisualizer = digVisualizer;
            _drawPathCallback = drawPathCallback;
        }

        /// <summary>
        /// Update dig state each frame (called by SimulationDirector).
        /// </summary>
        public void UpdateDigBehavior(float deltaSeconds)
        {
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
                    DigState.State = DigState.TaskState.Idle;
                    break;
            }
        }

        /// <summary>
        /// Request a new dig target from the scheduler.
        /// </summary>
        private void RequestNewDigTarget()
        {
            // TODO: Would need the full vehicle list and world state
            // For now, just transition to traveling
            DigState.State = DigState.TaskState.TravelingToDigSite;
        }

        /// <summary>
        /// Perform excavation at current dig site.
        /// </summary>
        private void PerformDigging(float deltaSeconds)
        {
            _digTimeAccumulated += deltaSeconds;

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
