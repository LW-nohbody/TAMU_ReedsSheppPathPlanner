using System;
using System.Collections.Generic;
using System.Linq;

namespace DigSim3D.Services
{   
    /// <summary>
    /// Enum for capturing the steering direction
    /// </summary>
    public enum Steering { LEFT = -1, RIGHT = 1, STRAIGHT = 0 }

    /// <summary>
    /// Enum for capturing the movement/gear direction
    /// </summary>
    public enum Gear { FORWARD = 1, BACKWARD = -1 }

    /// <summary>
    /// Defines a PathElement, consists of a duration(how long is this element valid), steering, and gear
    /// </summary>
    /// <param name="Param"></param>
    /// <param name="Steering"></param>
    /// <param name="Gear"></param>
    public record PathElement(double Param, Steering Steering, Gear Gear)
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

    /// <summary>
    /// Utility class
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Wraps angles to be between 0 and 2*pi, (modulo 2*pi)
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static double M(double angle) // wrap to [0,2π)
        {
            double twoPi = 2 * Math.PI;
            angle %= twoPi;
            if (angle < 0) angle += twoPi;
            return angle;
        }

        /// <summary>
        /// Performs a cartesian to polar conversion
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static (double rho, double theta) R(double x, double y)
        {
            double rho = Math.Sqrt(x * x + y * y);
            double theta = M(Math.Atan2(y, x));
            return (rho, theta);
        }

        // start/end: (x,y,thetaRadians). Returns end in start's local frame, theta in radians.
        /// <summary>
        /// Changes the basis of the cooridnates such that the start is at the origin and rotated such it is at 0 degrees
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
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

    /// <summary>
    /// Generates all Reeds-Sheep Paths
    /// </summary>
    public static class ReedsSheppPaths
    {
        // ---------- families 1..12 (all take phi in RADIANS) ----------
        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a main path model
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="phi"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Flips the gears of the path,so forward movements are now reverse and vice-versa
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<PathElement> Timeflip(List<PathElement> path)
        {
            return path.Select(e => e.ReverseGear()).ToList(); // matches reference
        }


        /// <summary>
        /// Flips the steering of the path, so left turns are now right turns and vice-versa
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<PathElement> Reflect(List<PathElement> path)
            => path.Select(e => new PathElement(e.Param, (Steering)(-(int)e.Steering), e.Gear)).ToList();

        // ----- planner API: start/end in RADIANS, x/y normalized by R -----
        /// <summary>
        /// Gets all possible Reeds-Shepp paths
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets the shortest Reeds-Shepp path
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static List<PathElement> GetOptimalPath(
            (double x, double y, double theta) start,
            (double x, double y, double theta) end)
        {
            var all = GetAllPaths(start, end);
            return (all.Count == 0) ? new List<PathElement>() : all.OrderBy(p => p.Sum(e => e.Param)).First();
        }
    }
}