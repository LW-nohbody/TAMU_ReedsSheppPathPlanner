namespace PathPlanningLib.Algorithms.ReedsShepp;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;

using System;
using System.Collections.Generic;
using System.Linq;

public class ReedsShepp : IPathPlanner
{
    // ---------- Internal Path Families 1..12 (Note that pose.Theta is in radians) ----------
    private static ReedsSheppPath Path1(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (u, t) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        double v = MathUtils.NormalizeAngle(pose.Theta - t);

        path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
        path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.FORWARD));
        return path;
    }

    private static ReedsSheppPath Path2(Pose pose)
    {
        double phi = MathUtils.NormalizeAngle(pose.Theta);
        var path = new ReedsSheppPath();
        var (rho, t1) = MathUtils.CartesianToPolar(pose.X + Math.Sin(phi), pose.Y - 1 - Math.Cos(phi));
        if (rho * rho >= 4.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = MathUtils.NormalizeAngle(t1 + Math.Atan2(2.0, u));
            double v = MathUtils.NormalizeAngle(t - phi);
            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path3(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        if (rho <= 4.0)
        {
            double A = Math.Acos(rho / 4.0);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
            double u = MathUtils.NormalizeAngle(Math.PI - 2.0 * A);
            double v = MathUtils.NormalizeAngle(pose.Theta - t - u);
            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.FORWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path4(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        if (rho <= 4.0)
        {
            double A = Math.Acos(rho / 4.0);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
            double u = MathUtils.NormalizeAngle(Math.PI - 2.0 * A);
            double v = MathUtils.NormalizeAngle(t + u - pose.Theta);
            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path5(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));
        if (rho <= 4.0)
        {
            double u = Math.Acos(1.0 - rho * rho / 8.0);
            double A = Math.Asin(2.0 * Math.Sin(u) / rho);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 - A);
            double v = MathUtils.NormalizeAngle(t - u - pose.Theta);
            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.RIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path6(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X + Math.Sin(pose.Theta), pose.Y - 1 - Math.Cos(pose.Theta));
        if (rho <= 4.0)
        {
            double t, u, v;
            if (rho <= 2.0)
            {
                double A = Math.Acos((rho + 2.0) / 4.0);
                t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
                u = MathUtils.NormalizeAngle(A);
                v = MathUtils.NormalizeAngle(pose.Theta - t + 2.0 * u);
            }
            else
            {
                double A = Math.Acos((rho - 2.0) / 4.0);
                t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 - A);
                u = MathUtils.NormalizeAngle(Math.PI - A);
                v = MathUtils.NormalizeAngle(pose.Theta - t + 2.0 * u);
            }
            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.RIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.LEFT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path7(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X + Math.Sin(pose.Theta), pose.Y - 1 - Math.Cos(pose.Theta));
        double u1 = (20.0 - rho * rho) / 16.0;

        if (rho <= 6.0 && u1 >= 0.0 && u1 <= 1.0)
        {
            double u = Math.Acos(u1);
            double A = Math.Asin(2.0 * Math.Sin(u) / rho);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
            double v = MathUtils.NormalizeAngle(t - pose.Theta);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.LEFT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path8(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));

        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
            double A = Math.Atan2(2.0, u + 2.0);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
            double v = MathUtils.NormalizeAngle(t - pose.Theta + Math.PI / 2.0);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path9(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X - Math.Sin(pose.Theta), pose.Y - 1 + Math.Cos(pose.Theta));

        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
            double A = Math.Atan2(u + 2.0, 2.0);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 - A);
            double v = MathUtils.NormalizeAngle(t - pose.Theta - Math.PI / 2.0);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path10(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X + Math.Sin(pose.Theta), pose.Y - 1 - Math.Cos(pose.Theta));

        if (rho >= 2.0)
        {
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0);
            double u = rho - 2.0;
            double v = MathUtils.NormalizeAngle(pose.Theta - t - Math.PI / 2.0);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path11(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X + Math.Sin(pose.Theta), pose.Y - 1 - Math.Cos(pose.Theta));

        if (rho >= 2.0)
        {
            double t = MathUtils.NormalizeAngle(theta);
            double u = rho - 2.0;
            double v = MathUtils.NormalizeAngle(pose.Theta - t - Math.PI / 2.0);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    private static ReedsSheppPath Path12(Pose pose)
    {
        var path = new ReedsSheppPath();
        var (rho, theta) = MathUtils.CartesianToPolar(pose.X + Math.Sin(pose.Theta), pose.Y - 1 - Math.Cos(pose.Theta));

        if (rho >= 4.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 4.0;
            double A = Math.Atan2(2.0, u + 4.0);
            double t = MathUtils.NormalizeAngle(theta + Math.PI / 2.0 + A);
            double v = MathUtils.NormalizeAngle(t - pose.Theta);

            path.Add(ReedsSheppElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.BACKWARD));
            path.Add(ReedsSheppElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }


    // ----- Planner API: start/end as Pose: theta in radians, x/y normalized -----
    public static List<ReedsSheppPath> GetAllPaths(Pose start, Pose end)
    {
        // Convert end into local coordinates relative to start
        Pose local = MathUtils.ChangeOfBasis(start, end);
        var candidates = new List<ReedsSheppPath>
        {
            Path1(local), Path2(local),  Path3(local),  Path4(local),
            Path5(local), Path6(local),  Path7(local),  Path8(local),
            Path9(local), Path10(local), Path11(local), Path12(local)
        };

        // Apply symmetries
        var more = new List<ReedsSheppPath>();
        foreach (var p in candidates)
        {
            if (p.Count == 0) continue;
            more.Add(p.Timeflip());
            more.Add(p.Reflect());
            more.Add(p.Timeflip().Reflect());
        }
        candidates.AddRange(more);

        return candidates.Where(p => p.Count > 0).ToList();
    }

    public static ReedsSheppPath GetOptimalPath(Pose start, Pose end)
    {
        var all = GetAllPaths(start, end);
        foreach (var path in all)
            path.ComputeLength();

        return all.Count == 0 ? new ReedsSheppPath() : all.OrderBy(p => p.Length).First();
    }
}