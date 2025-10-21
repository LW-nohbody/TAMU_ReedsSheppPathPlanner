using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum Steering
{
    LEFT = -1,
    RIGHT = 1,
    STRAIGHT = 0
}

public enum Gear
{
    FORWARD = 1,
    BACKWARD = -1
}

public record PathElement(double Param, Steering Steering, Gear Gear)
{
    public static PathElement Create(double param, Steering steering, Gear gear)
    {
        if (param >= 0)
            return new PathElement(param, steering, gear);
        else
            return new PathElement(-param, steering, gear).ReverseGear();
    }

    public PathElement ReverseSteering()
    {
        var steering = (Steering)(-(int)this.Steering);
        return this with { Steering = steering };
    }

    public PathElement ReverseGear()
    {
        var gear = (Gear)(-(int)this.Gear);
        return this with { Gear = gear };
    }

    public override string ToString()
    {
        return $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Param, 2)} }}";
    }
}

public static class Utils
{
    public static double Deg2Rad(double deg) => deg * Math.PI / 180.0;

    // Wrap to [0, 2π)
    public static double M(double angle)
    {
        double twoPi = 2 * Math.PI;
        angle %= twoPi;
        if (angle < 0) angle += twoPi;
        return angle;
    }

    // Return polar coords
    public static (double rho, double theta) R(double x, double y)
    {
        double rho = Math.Sqrt(x * x + y * y);
        double theta = M(Math.Atan2(y, x));
        return (rho, theta);
    }

    public static (double x, double y, double theta) ChangeOfBasis(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        double dx = end.x - start.x;
        double dy = end.y - start.y;
        double dtheta = M(end.theta - start.theta);

        // Rotate into start's frame
        double cos = Math.Cos(-start.theta);
        double sin = Math.Sin(-start.theta);

        double xNew = dx * cos - dy * sin;
        double yNew = dx * sin + dy * cos;

        return (xNew, yNew, dtheta);
    }
}

public static class ReedsSheppPaths
{
    // Path families 1–12
    public static List<PathElement> Path1(double x, double y, double phi)
    {
        phi = Utils.Deg2Rad(phi);
        var path = new List<PathElement>();

        var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);

        path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));

        return path;
    }

    // --- Path2 .. Path12 ---

    public static List<PathElement> Path2(double x, double y, double phi)
    {
        // Formula 8.2: CSC (opposite turns)
        phi = Utils.M(Utils.Deg2Rad(phi)); // note: Python used M(deg2rad(phi))
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
        // Formula 8.3: C|C|C
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.4 (1): C|CC
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.4 (2): CC|C
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.7: CCu|CuC
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.8: C|CuCu|C
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.9 (1): C|C[pi/2]SC
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.9 (2): CSC[pi/2]|C
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.10 (1): C|C[pi/2]SC
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.10 (2): CSC[pi/2]|C
        phi = Utils.Deg2Rad(phi);
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
        // Formula 8.11: C|C[pi/2]SC[pi/2]|C
        phi = Utils.Deg2Rad(phi);
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

    // Path transformations
    public static List<PathElement> Timeflip(List<PathElement> path)
    {
        return path.Select(e => e.ReverseGear()).ToList();
    }

    public static List<PathElement> Reflect(List<PathElement> path)
    {
        return path.Select(e => e.ReverseSteering()).ToList();
    }

    // -------------------------
    // Generate all candidate paths
    public static List<List<PathElement>> GetAllPaths(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        var localEnd = Utils.ChangeOfBasis(start, end);
        double x = localEnd.x;
        double y = localEnd.y;
        double phi = localEnd.theta;

        var candidates = new List<List<PathElement>>();

        // Core set
        candidates.Add(Path1(x, y, phi));
        candidates.Add(Path2(x, y, phi));
        candidates.Add(Path3(x, y, phi));
        candidates.Add(Path4(x, y, phi));
        candidates.Add(Path5(x, y, phi));
        candidates.Add(Path6(x, y, phi));
        candidates.Add(Path7(x, y, phi));
        candidates.Add(Path8(x, y, phi));
        candidates.Add(Path9(x, y, phi));
        candidates.Add(Path10(x, y, phi));
        candidates.Add(Path11(x, y, phi));
        candidates.Add(Path12(x, y, phi));

        // Symmetries
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

    // -------------------------
    // Return shortest path
    public static List<PathElement> GetOptimalPath(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        var all = GetAllPaths(start, end);
        if (all.Count == 0) return new List<PathElement>();

        return all.OrderBy(p => p.Sum(e => e.Param)).First();
    }
}
