using System;
using Godot;
using SimCore.Core;
using ThreeD.Debugging;


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
            var goalPos = new Vector3((float)goal.X, 0, (float)goal.Z);

            var pathId = DebugPath.Begin("3d.adapter", 0, 0);

            DebugPath.Check(pathId, "inputs_world",
                ("s.x", startPos.X), ("s.y", startPos.Z), ("s.th", start.Yaw),
                ("g.x", goalPos.X),  ("g.y", goalPos.Z),  ("g.th", goal.Yaw),
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
            path.Points.AddRange(pts);
            path.Gears.AddRange(gears);
            return path;
        }
    }
}