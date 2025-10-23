using Godot;
using DigSim3D.Domain;
using DigSim3D.Debugging;

namespace DigSim3D.Services
{
    // Thin wrapper around your existing RSAdapter.ComputePath3D
    public sealed class ReedsSheppPlanner : IPathPlanner
    {
        private readonly float _step;
        public ReedsSheppPlanner(float step = 0.25f) => _step = step;

        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world)
        {
            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos = new Vector3((float)goal.X, 0, (float)goal.Z);

            var pathId = DebugPath.Begin("digsim.adapter", 0, 0);

            DebugPath.Check(pathId, "inputs_world",
                ("s.x", startPos.X), ("s.y", startPos.Z), ("s.th", start.Yaw),
                ("g.x", goalPos.X), ("g.y", goalPos.Z), ("g.th", goal.Yaw),
                ("R", spec.TurnRadius), ("stepM", _step));

            var (pts, gears) = RSAdapter.ComputePath3D(
                startPos, start.Yaw, goalPos, goal.Yaw,
                turnRadiusMeters: spec.TurnRadius,
                sampleStepMeters: _step);


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
            if (pts != null) path.Points.AddRange(pts);
            if (gears != null) path.Gears.AddRange(gears);
            return path;
        }
    }
}