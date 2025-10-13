using System;
using System.Collections.Generic;
using System.Linq;

public enum Steering { LEFT = -1, RIGHT = 1, STRAIGHT = 0 }
public enum Gear     { FORWARD = 1 }

public record PathElement(double Param, Steering Steering, Gear Gear)
{
    public static PathElement Create(double param, Steering steering, Gear gear)
        => new PathElement(param, steering, gear);

    public override string ToString()
        => $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Param, 3)} }}";
}

public static class Utils
{
    public static double M(double angle) // wrap to [0,2π)
    {
        double twoPi = 2 * Math.PI;
        angle %= twoPi;
        if (angle < 0) angle += twoPi;
        return angle;
    }

    public static (double rho, double theta) PolarConversion(double x, double y)
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

public static class DubinsPaths
{
    // ---------- families 1..12 (all take phi in RADIANS) ----------
    public static List<PathElement> Path1(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.PolarConversion(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        if (rho <= 4.0)
        {
            double a = Math.Acos(rho / 4.0);
            double u = Utils.M(Math.PI - 2.0 * a);
            double t = Utils.M(t1 + Math.PI / 2.0 - a);
            double v = Utils.M(phi - t - u);
            path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
        }
        return path;
    }

    public static List<PathElement> Path2(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.PolarConversion(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        if (rho <= 4.0)
        {
            double a = Math.Acos(rho / 4.0);
            double u = Utils.M(Math.PI - 2.0 * a);
            double t = Utils.M(t1 - Math.PI / 2.0 + a);
            double v = Utils.M(phi - t - u);
            path.Add(PathElement.Create(t, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.LEFT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        }
        return path;
    }
    
    public static List<PathElement> Path3(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        var (u, t) = Utils.PolarConversion(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.LEFT,     Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.LEFT,     Gear.FORWARD));
        return path;
    }

    public static List<PathElement> Path4(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.PolarConversion(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
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
    
    public static List<PathElement> Path5(double x, double y, double phi)
    {
        var path = new List<PathElement>();
        var (u, t) = Utils.PolarConversion(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.RIGHT,     Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.RIGHT,     Gear.FORWARD));
        return path;
    }

    public static List<PathElement> Path6(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.PolarConversion(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        if (rho * rho >= 4.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = Utils.M(t1 + Math.Atan2(2.0, u));
            double v = Utils.M(t - phi);
            path.Add(PathElement.Create(t, Steering.RIGHT,     Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.LEFT,    Gear.FORWARD));
        }
        return path;
    }


    // public static List<PathElement> Reflect(List<PathElement> path) //TODO: Delete Reflect?
    //     => path.Select(e => new PathElement(e.Param, (Steering)(-(int)e.Steering), e.Gear)).ToList();

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
            Path5(x,y,phi),  Path6(x,y,phi)
        };

        // // Re-enable the 1 symmetry variant for full 6 paths
        // var more = new List<List<PathElement>>();
        // foreach (var p in candidates)
        // {
        //     if (p.Count == 0) continue;
        //     more.Add(Reflect(p));
        // }
        // candidates.AddRange(more);

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