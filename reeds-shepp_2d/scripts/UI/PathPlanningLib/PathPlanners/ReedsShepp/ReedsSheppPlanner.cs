namespace PathPlanningLib.PathPlanners.ReedsShepp;
using System;
using System.Collections.Generic;
using System.Linq;
using PathPlanningLib.Geometry;
using PathPlanningLib.Vehicles.Kinematics;

public class ReedsSheppPlanner<TKinematics> : IPathPlanner<TKinematics>
    where TKinematics : IKinematicModel
{
    private readonly double turningRadius;

    public ReedsSheppPlanner(double turningRadius)
    {
        this.turningRadius = turningRadius;
    }

    /// <summary>
    /// Plans a path from start to goal using Reeds–Shepp curves.
    /// Returns a Path object (sequence of Poses).
    /// </summary>
    public Path PlanPath(Pose start, Pose goal, TKinematics model)
    {
        // Step 1: convert start/goal to tuples for Reeds–Shepp
        var startTuple = (start.X / turningRadius, start.Y / turningRadius, start.Theta);
        var goalTuple  = (goal.X / turningRadius,  goal.Y / turningRadius,  goal.Theta);

        // Step 2: compute optimal Reeds–Shepp path (as PathElements)
        var elements = ReedsSheppPaths.GetOptimalPath(startTuple, goalTuple);

        // Step 3: convert PathElements to Path with Poses
        return ConvertToPath(start, elements, turningRadius);
    }

    /// <summary>
    /// Converts a list of Reeds–Shepp PathElements to a Path of Poses.
    /// </summary>
    private Path ConvertToPath(Pose start, List<PathElement> elements, double turningRadius)
    {
        var path = new Path();
        // start is expected in WORLD units (same units as turningRadius)
        double x = start.X;
        double y = start.Y;
        double theta = start.Theta;
        path.AddPose(new Pose(x, y, theta));

        foreach (var seg in elements)
        {
            // seg.Param is in NORMALIZED units (because RS computed with x/R, y/R).
            // Convert a segment length to WORLD units:
            double s_norm = seg.Param;
            double s_world = s_norm * turningRadius; // <<--- VERY IMPORTANT

            // discretize the world distance
            //int nSteps = Math.Max(1, (int)(s_world / 5.0)); // choose step size in world units (e.g., 5 px)
            //double dsWorld = s_world / nSteps;
            int nSteps = Math.Max(2, (int)Math.Ceiling(s_world / 1.0)); // 1 px per step
            double dsWorld = s_world / nSteps;

            for (int i = 0; i < nSteps; i++)
            {
                if (seg.Steering == Steering.STRAIGHT)
                {
                    // move forward/back along heading
                    double move = dsWorld * (int)seg.Gear;
                    x += move * Math.Cos(theta);
                    y += move * Math.Sin(theta);
                }
                else
                {
                    // arc: normalized curvature is 1 / turningRadius, so:
                    // angular change for this micro-step = (ds_world / turningRadius) * steerSign * gearSign
                    double steerSign = (int)seg.Steering; // +1 left, -1 right
                    double gearSign = (int)seg.Gear;     // +1 forward, -1 backward
                    double dtheta = steerSign * (dsWorld / turningRadius) * gearSign;
                    // rotate about instantaneous center
                    double thetaPrev = theta;
                    theta += dtheta;

                    // arc-based incremental update (exact integration of circular arc)
                    // center-based incremental:
                    double cx = x - steerSign * turningRadius * Math.Sin(thetaPrev);
                    double cy = y + steerSign * turningRadius * Math.Cos(thetaPrev);

                    double newX = cx + steerSign * turningRadius * Math.Sin(theta);
                    double newY = cy - steerSign * turningRadius * Math.Cos(theta);

                    x = newX;
                    y = newY;
                }

                path.AddPose(new Pose(x, y, theta));
            }
        }

        return path;
    }
}

// Represents Reeds-Shepp steering commands
internal enum Steering { LEFT = -1, RIGHT = 1, STRAIGHT = 0 }
// Represent forwards/backwards motion
internal enum Gear     { FORWARD = 1, BACKWARD = -1 }

// Represents a segment of a Reeds–Shepp path
internal record PathElement(double Param, Steering Steering, Gear Gear)
{
    public static PathElement Create(double param, Steering steering, Gear gear)
        => (param >= 0)
           ? new PathElement(param, steering, gear)
           : new PathElement(-param, steering, gear).ReverseGear();

    public PathElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };
    public PathElement ReverseGear() => this with { Gear = (Gear)(-(int)Gear) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Param, 3)} }}";
}

// Reeds-Shepp Utility Function
internal static class Utils
{
    public static double M(double angle) // wrap to [0,2π)
    {
        double twoPi = 2 * Math.PI;
        angle %= twoPi;
        if (angle < 0) angle += twoPi;
        return angle;
    }

    public static (double rho, double theta) R(double x, double y)
    {
        double rho = Math.Sqrt(x * x + y * y);
        double theta = M(Math.Atan2(y, x));
        return (rho, theta);
    }

    // start/end: (x,y,thetaRadians). Returns end in start's local frame, theta in radians.
    public static (double x, double y, double theta) ChangeOfBasis(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        double dx = end.x - start.x;
        double dy = end.y - start.y;
        double dtheta = M(end.theta - start.theta);

        double cos = Math.Cos(-start.theta);
        double sin = Math.Sin(-start.theta);
        double xNew = dx * cos - dy * sin;
        double yNew = dx * sin + dy * cos;
        return (xNew, yNew, dtheta);
    }
}

// Represents Original 12 Reeds-Shepp Families
internal static class ReedsSheppPaths
{
    // ---------- families 1..12 (all take phi in RADIANS) ----------
    public static List<PathElement> Path1(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
        return path;
    }

    public static List<PathElement> Path2(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        if (rho * rho >= 4.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = Utils.M(t1 + Math.Atan2(2.0, u));
            double v = Utils.M(t - phi);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }

    public static List<PathElement> Path3(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x - Math.Sin(phi);
        double eta = y - 1 + Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho <= 4.0)
        {
            double A = Math.Acos(rho / 4.0);
            double t = Utils.M(theta + Math.PI / 2.0 + A);
            double u = Utils.M(Math.PI - 2.0 * A);
            double v = Utils.M(phi - t - u);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
        }
        return path;
    }

    public static List<PathElement> Path4(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x - Math.Sin(phi);
        double eta = y - 1 + Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho <= 4.0)
        {
            double A = Math.Acos(rho / 4.0);
            double t = Utils.M(theta + Math.PI / 2.0 + A);
            double u = Utils.M(Math.PI - 2.0 * A);
            double v = Utils.M(t + u - phi);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path5(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x - Math.Sin(phi);
        double eta = y - 1 + Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho <= 4.0)
        {
            double u = Math.Acos(1.0 - rho * rho / 8.0);
            double A = Math.Asin(2.0 * Math.Sin(u) / rho);
            double t = Utils.M(theta + Math.PI / 2.0 - A);
            double v = Utils.M(t - u - phi);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path6(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x + Math.Sin(phi);
        double eta = y - 1 - Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho <= 4.0)
        {
            double t, u, v;
            if (rho <= 2.0)
            {
                double A = Math.Acos((rho + 2.0) / 4.0);
                t = Utils.M(theta + Math.PI / 2.0 + A);
                u = Utils.M(A);
                v = Utils.M(phi - t + 2.0 * u);
            }
            else
            {
                double A = Math.Acos((rho - 2.0) / 4.0);
                t = Utils.M(theta + Math.PI / 2.0 - A);
                u = Utils.M(Math.PI - A);
                v = Utils.M(phi - t + 2.0 * u);
            }
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.LEFT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path7(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x + Math.Sin(phi);
        double eta = y - 1 - Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        double u1 = (20.0 - rho * rho) / 16.0;
        if (rho <= 6.0 && u1 >= 0.0 && u1 <= 1.0)
        {
            double u = Math.Acos(u1);
            double A = Math.Asin(2.0 * Math.Sin(u) / rho);
            double t = Utils.M(theta + Math.PI / 2.0 + A);
            double v = Utils.M(t - phi);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(u, Steering.LEFT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }

    public static List<PathElement> Path8(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x - Math.Sin(phi);
        double eta = y - 1 + Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
            double A = Math.Atan2(2.0, u + 2.0);
            double t = Utils.M(theta + Math.PI / 2.0 + A);
            double v = Utils.M(t - phi + Math.PI / 2.0);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path9(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x - Math.Sin(phi);
        double eta = y - 1 + Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
            double A = Math.Atan2(u + 2.0, 2.0);
            double t = Utils.M(theta + Math.PI / 2.0 - A);
            double v = Utils.M(t - phi - Math.PI / 2.0);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path10(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x + Math.Sin(phi);
        double eta = y - 1 - Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho >= 2.0)
        {
            double t = Utils.M(theta + Math.PI / 2.0);
            double u = rho - 2.0;
            double v = Utils.M(phi - t - Math.PI / 2.0);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path11(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x + Math.Sin(phi);
        double eta = y - 1 - Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho >= 2.0)
        {
            double t = Utils.M(theta);
            double u = rho - 2.0;
            double v = Utils.M(phi - t - Math.PI / 2.0);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
        }
        return path;
    }

    public static List<PathElement> Path12(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        double xi = x + Math.Sin(phi);
        double eta = y - 1 - Math.Cos(phi);
        var (rho, theta) = Utils.R(xi, eta);
        if (rho >= 4.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0) - 4.0;
            double A = Math.Atan2(2.0, u + 4.0);
            double t = Utils.M(theta + Math.PI / 2.0 + A);
            double v = Utils.M(t - phi);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
            path.Add(PathElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.BACKWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }

    // ----- symmetries (time-flip reverses order + gear) -----
    public static List<PathElement> Timeflip(List<PathElement> path)
    {
        return path.Select(e => e.ReverseGear()).ToList(); // matches reference
    }


    public static List<PathElement> Reflect(List<PathElement> path)
        => path.Select(e => new PathElement(e.Param, (Steering)(-(int)e.Steering), e.Gear)).ToList();

    // ----- planner API: start/end in RADIANS, x/y normalized by R -----
    public static List<List<PathElement>> GetAllPaths(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        var local = Utils.ChangeOfBasis(start, end);      // radians in, radians out
        double x = local.x, y = local.y, phi = Utils.M(local.theta); // ensure [0,2π)

        var candidates = new List<List<PathElement>>
        {
            Path1(x,y,phi),  Path2(x,y,phi),  Path3(x,y,phi),  Path4(x,y,phi),
            Path5(x,y,phi),  Path6(x,y,phi),  Path7(x,y,phi),  Path8(x,y,phi),
            Path9(x,y,phi),  Path10(x,y,phi), Path11(x,y,phi), Path12(x,y,phi)
        };

        // Re-enable the 3 symmetry variants for full 48-path coverage
        var more = new List<List<PathElement>>();
        foreach (var p in candidates)
        {
            if (p.Count == 0) continue;
            more.Add(Timeflip(p));
            more.Add(Reflect(p));
            more.Add(Timeflip(Reflect(p)));
        }
        candidates.AddRange(more);

        return candidates.Where(p => p.Count > 0).ToList();
    }

    public static List<PathElement> GetOptimalPath(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        var all = GetAllPaths(start, end);
        return (all.Count == 0) ? new List<PathElement>() : all.OrderBy(p => p.Sum(e => e.Param)).First();
    }
}