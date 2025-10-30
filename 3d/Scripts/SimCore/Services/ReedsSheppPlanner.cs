using Godot;
using SimCore.Core;

namespace SimCore.Services
{
    // Thin wrapper around your existing RSAdapter.ComputePath3D
    public sealed class ReedsSheppPlanner : IPathPlanner
    {
        private readonly float _sampleStep;

        public ReedsSheppPlanner(float sampleStepMeters = 0.25f)
        {
            _sampleStep = sampleStepMeters;
        }

        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState _world)
        {
            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos  = new Vector3((float)goal.X,  0, (float)goal.Z);

            var (pts, gears) = RSAdapter.ComputePath3D(
                startPos, start.Yaw, goalPos, goal.Yaw,
                turnRadiusMeters: spec.TurnRadius,
                sampleStepMeters: _sampleStep
            );

            var path = new PlannedPath();
            path.Points.AddRange(pts);
            path.Gears.AddRange(gears);
            return path;
        }
    }
}