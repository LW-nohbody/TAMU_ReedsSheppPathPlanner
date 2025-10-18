using PathPlanningLib.PathPlanners.ReedsShepp;
using PathPlanningLib.Geometry;
using PathPlanningLib.Vehicles.Kinematics;
using System;
using System.Linq;

class Program
{
    static void Main()
    {
        // Start and goal poses
        var start = new Pose(0, 0, 0);
        var goal  = new Pose(5, 5, Math.PI / 2);

        // Original Reeds–Shepp
        var originalElements = ReedsSheppPaths.GetOptimalPath(
            (start.X, start.Y, start.Theta),
            (goal.X,  goal.Y,  goal.Theta)
        );

        double originalLength = originalElements.Sum(e => e.Param);
        Console.WriteLine($"Original Reeds–Shepp path length: {originalLength:F3}");

        // New PathPlanningLib Reeds–Shepp
        var planner = new ReedsSheppPlanner<DifferentialDriveKinematics>(turningRadius: 1.0);
        var path = planner.PlanPath(start, goal, new DifferentialDriveKinematics(4.0, 4.0));

        double newLength = 0.0;
        for (int i = 1; i < path.Poses.Count; i++)
        {
            double dx = path.Poses[i].X - path.Poses[i - 1].X;
            double dy = path.Poses[i].Y - path.Poses[i - 1].Y;
            newLength += Math.Sqrt(dx * dx + dy * dy);
        }
        Console.WriteLine($"New PathPlanningLib path length: {newLength:F3}");

        // Optional: print first few poses
        Console.WriteLine("First few poses of new path:");
        foreach (var p in path.Poses.Take(5))
            Console.WriteLine($"({p.X:F2}, {p.Y:F2}, {p.Theta:F2})");
    }
}