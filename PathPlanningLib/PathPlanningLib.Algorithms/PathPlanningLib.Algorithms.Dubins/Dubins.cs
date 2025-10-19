
namespace PathPlanningLib.Algorithms.Dubins;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Algorithms.Geometry.PathElements;

using System;
using System.Collections.Generic;
using System.Linq;

public class Dubins : IPathPlanner
{
    // ---------- Internal Path Families 1..6 (all take phi in RADIANS) ----------
    private static DubinsPath Path1(Pose pose)
    {
        var path = new DubinsPath();
        double phi = MathUtils.NormalizeAngle(pose.Theta);
        var (rho, t1) = MathUtils.CartesianToPolar(pose.X + Math.Sin(phi), pose.Y - 1 - Math.Cos(phi));
        if (rho <= 4.0)
        {
            double a = Math.Acos(rho / 4.0);
            double u = MathUtils.NormalizeAngle(Math.PI - 2.0 * a);
            double t = MathUtils.NormalizeAngle(t1 + Math.PI / 2.0 - a);
            double v = MathUtils.NormalizeAngle(phi - t - u);
            path.Add(DubinsElement.Create(t, Steering.LEFT));
            path.Add(DubinsElement.Create(u, Steering.RIGHT));
            path.Add(DubinsElement.Create(v, Steering.LEFT));
        }
        return path;
    }

    private static DubinsPath Path2(Pose pose)
    {
        var path = new DubinsPath();
        double phi = MathUtils.NormalizeAngle(pose.Theta);
        var (rho, t1) = MathUtils.CartesianToPolar(pose.X + Math.Sin(phi), pose.Y - 1 - Math.Cos(phi));
        if (rho <= 4.0)
        {
            double a = Math.Acos(rho / 4.0);
            double u = MathUtils.NormalizeAngle(Math.PI - 2.0 * a);
            double t = MathUtils.NormalizeAngle(t1 - Math.PI / 2.0 + a);
            double v = MathUtils.NormalizeAngle(phi - t - u);
            path.Add(DubinsElement.Create(t, Steering.RIGHT));
            path.Add(DubinsElement.Create(u, Steering.LEFT));
            path.Add(DubinsElement.Create(v, Steering.RIGHT));
        }
        return path;
    }

    private static DubinsPath Path3(Pose pose)
    {
        var path = new DubinsPath();
        var (u, t) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        double v = MathUtils.NormalizeAngle(pose.Theta - t);
        path.Add(DubinsElement.Create(t, Steering.LEFT));
        path.Add(DubinsElement.Create(u, Steering.STRAIGHT));
        path.Add(DubinsElement.Create(v, Steering.LEFT));
        return path;
    }

    private static DubinsPath Path4(Pose pose)
    {
        var path = new DubinsPath();
        double phi = MathUtils.NormalizeAngle(pose.Theta);
        var (rho, t1) = MathUtils.CartesianToPolar(pose.X + Math.Sin(phi), pose.Y - 1 - Math.Cos(phi));
        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = MathUtils.NormalizeAngle(t1 + Math.Atan2(2.0, u));
            double v = MathUtils.NormalizeAngle(t - phi);
            path.Add(DubinsElement.Create(t, Steering.LEFT));
            path.Add(DubinsElement.Create(u, Steering.STRAIGHT));
            path.Add(DubinsElement.Create(v, Steering.RIGHT));
        }
        return path;
    }

    private static DubinsPath Path5(Pose pose)
    {
        var path = new DubinsPath();
        var (u, t) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        double v = MathUtils.NormalizeAngle(pose.Theta - t);
        path.Add(DubinsElement.Create(t, Steering.RIGHT));
        path.Add(DubinsElement.Create(u, Steering.STRAIGHT));
        path.Add(DubinsElement.Create(v, Steering.RIGHT));
        return path;
    }

    private static DubinsPath Path6(Pose pose)
    {
        var path = new DubinsPath();
        double phi = MathUtils.NormalizeAngle(pose.Theta);
        var (rho, t1) = MathUtils.CartesianToPolar(pose.X + Math.Sin(phi), pose.Y - 1 - Math.Cos(phi));
        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = MathUtils.NormalizeAngle(t1 + Math.Atan2(2.0, u));
            double v = MathUtils.NormalizeAngle(t - phi);
            path.Add(DubinsElement.Create(t, Steering.RIGHT));
            path.Add(DubinsElement.Create(u, Steering.STRAIGHT));
            path.Add(DubinsElement.Create(v, Steering.LEFT));
        }
        return path;
    }

    // ----- Planner APIs: start/end as Pose, radians, x/y normalized -----
    public static List<DubinsPath> GetAllPaths(Pose start, Pose end)
    {
        Pose local = MathUtils.ChangeOfBasis(start, end); // Pose in start's local frame

        var candidates = new List<DubinsPath>
        {
            Path1(local), Path2(local), Path3(local),
            Path4(local), Path5(local), Path6(local)
        };

        return candidates.Where(p => p.Count > 0).ToList();
    }

    public static DubinsPath GetOptimalPath(Pose start, Pose end)
    {
        var all = GetAllPaths(start, end);

        foreach (var path in all)
            path.ComputeLength();

        return (all.Count == 0)
            ? new DubinsPath()
            : all.OrderBy(p => p.Length).First();
    }
}


