using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

// public enum Steering { LEFT = -1, RIGHT = 1, STRAIGHT = 0 }
// public enum Gear { FORWARD = 1 }

// public record PathElement(double Param, Steering Steering, Gear Gear)
// {
//     public static PathElement Create(double param, Steering steering, Gear gear)
//         => new PathElement(param, steering, gear);

//     public override string ToString()
//         => $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Param, 3)} }}";
// }

// public static class Utils
// {
//     public static double M(double angle) // wrap to [0,2π)
//     {
//         double twoPi = 2 * Math.PI;
//         angle %= twoPi;
//         if (angle < 0) angle += twoPi;
//         return angle;
//     }

//     public static (double rho, double theta) PolarConversion(double x, double y)
//     {
//         double rho = Math.Sqrt(x * x + y * y);
//         double theta = M(Math.Atan2(y, x));
//         return (rho, theta);
//     }

//     // start/end: (x,y,thetaRadians). Returns end in start's local frame, theta in radians.
//     public static (double x, double y, double theta) ChangeOfBasis(
//         (double x, double y, double theta) start,
//         (double x, double y, double theta) end)
//     {
//         double dx = end.x - start.x;
//         double dy = end.y - start.y;
//         double dtheta = M(end.theta - start.theta);

//         double cos = Math.Cos(-start.theta);
//         double sin = Math.Sin(-start.theta);
//         double xNew = dx * cos - dy * sin;
//         double yNew = dx * sin + dy * cos;
//         return (xNew, yNew, dtheta);
//     }
// }

public static class DifferentialDrivePaths
{
    // ---------- families 1..12 (all take phi in RADIANS) ----------
    public static List<PathElement> Path1(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.LEFT,     Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.LEFT,     Gear.FORWARD));
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
            path.Add(PathElement.Create(t, Steering.LEFT,     Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT,    Gear.FORWARD));
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


    public static List<List<PathElement>> GetAllPaths(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        var local = Utils.ChangeOfBasis(start, end);      // radians in, radians out
        double x = local.x, y = local.y, phi = Utils.M(local.theta); // ensure [0,2π)

        var candidates = new List<List<PathElement>>
        {
            Path1(x,y,phi),  Path2(x,y,phi)
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
