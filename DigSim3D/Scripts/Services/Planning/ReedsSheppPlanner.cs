using System;
using Godot;
using DigSim3D.Domain;
using DigSim3D.Debugging;

namespace DigSim3D.Services
{
    /// <summary>
    /// Wrapper around the RSAdapter.ComputePath3D
    /// </summary>
    public sealed class ReedsSheppPlanner : IPathPlanner
    {
        private readonly float _sampleStep;

        public ReedsSheppPlanner(float sampleStepMeters = 0.25f)
        {
            _sampleStep = sampleStepMeters;
        }

        /// <summary>
        /// [Deprecated] Plans RS path to reach goa position. Does not accound for obstacles
        /// </summary>
        /// <param name="start"></param>
        /// <param name="goal"></param>
        /// <param name="spec"></param>
        /// <param name="_world"></param>
        /// <returns></returns>
        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState _world)
        {
            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos = new Vector3((float)goal.X, 0, (float)goal.Z);

            var pathId = DebugPath.Begin("3d.adapter", 0, 0);

            DebugPath.Check(pathId, "inputs_world",
                ("s.x", startPos.X), ("s.y", startPos.Z), ("s.th", start.Yaw),
                ("g.x", goalPos.X), ("g.y", goalPos.Z), ("g.th", goal.Yaw),
                ("R", spec.TurnRadius), ("stepM", _sampleStep));


            var (pts, gears) = RSAdapter.ComputePath3D(
                startPos, start.Yaw, goalPos, goal.Yaw,
                turnRadiusMeters: spec.TurnRadius,
                sampleStepMeters: _sampleStep
            );

            if (pts != null && pts.Length > 0)
            {
                var end = pts[^1];
                double endErr = Math.Sqrt(Math.Pow(end.X - goalPos.X, 2) + Math.Pow(end.Z - goalPos.Z, 2));
                DebugPath.End(pathId, "done",
                    ("nPts", pts.Length), ("end", end), ("goal", goalPos), ("endErrM", endErr));
            }
            else
            {
                DebugPath.End(pathId, "empty_path");
            }


            var path = new PlannedPath();
            path.Points.AddRange(pts ?? Array.Empty<Vector3>());
            path.Gears.AddRange(gears ?? Array.Empty<int>());
            return path;
        }
    }
}