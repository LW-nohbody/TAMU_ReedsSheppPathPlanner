
namespace PathPlanningLib.Algorithms.Dubins;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

public class Dubins : IPathPlanner
{
    // ---------- families 1..12 (all take phi in RADIANS) ----------
    public static List<PathElement> Path1(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
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
        var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
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
        var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
        return path;
    }

    public static List<PathElement> Path4(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        // if (rho * rho >= 4.0)
        if(rho >= 2.0)
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
        var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
        double v = Utils.M(phi - t);
        path.Add(PathElement.Create(t, Steering.RIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
        path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
        return path;
    }

    public static List<PathElement> Path6(double x, double y, double phi)
    {
        phi = Utils.M(phi);
        var path = new List<PathElement>();
        var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
        // if (rho * rho >= 4.0)
        if(rho >= 2.0)
        {
            double u = Math.Sqrt(rho * rho - 4.0);
            double t = Utils.M(t1 + Math.Atan2(2.0, u));
            double v = Utils.M(t - phi);
            path.Add(PathElement.Create(t, Steering.RIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
            path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
        }
        return path;
    }

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
