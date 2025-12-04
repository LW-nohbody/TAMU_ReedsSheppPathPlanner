// using System;
// using System.Collections.Generic;
// using System.Linq;

// namespace DigSim3D.Services
// {
//     public static class ReedsSheppPaths
//     {
//         // ---------- families 1..12 (all take phi in RADIANS) ----------
//         public static List<PathElement> Path1(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             var (u, t) = Utils.R(x - Math.Sin(phi), y - 1 + Math.Cos(phi));
//             double v = Utils.M(phi - t);
//             path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//             path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
//             path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
//             return path;
//         }

//         public static List<PathElement> Path2(double x, double y, double phi)
//         {
//             phi = Utils.M(phi);
//             var path = new List<PathElement>();
//             var (rho, t1) = Utils.R(x + Math.Sin(phi), y - 1 - Math.Cos(phi));
//             if (rho * rho >= 4.0)
//             {
//                 double u = Math.Sqrt(rho * rho - 4.0);
//                 double t = Utils.M(t1 + Math.Atan2(2.0, u));
//                 double v = Utils.M(t - phi);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path3(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x - Math.Sin(phi);
//             double eta = y - 1 + Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho <= 4.0)
//             {
//                 double A = Math.Acos(rho / 4.0);
//                 double t = Utils.M(theta + Math.PI / 2.0 + A);
//                 double u = Utils.M(Math.PI - 2.0 * A);
//                 double v = Utils.M(phi - t - u);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.LEFT, Gear.FORWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path4(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x - Math.Sin(phi);
//             double eta = y - 1 + Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho <= 4.0)
//             {
//                 double A = Math.Acos(rho / 4.0);
//                 double t = Utils.M(theta + Math.PI / 2.0 + A);
//                 double u = Utils.M(Math.PI - 2.0 * A);
//                 double v = Utils.M(t + u - phi);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path5(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x - Math.Sin(phi);
//             double eta = y - 1 + Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho <= 4.0)
//             {
//                 double u = Math.Acos(1.0 - rho * rho / 8.0);
//                 double A = Math.Asin(2.0 * Math.Sin(u) / rho);
//                 double t = Utils.M(theta + Math.PI / 2.0 - A);
//                 double v = Utils.M(t - u - phi);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.RIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path6(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x + Math.Sin(phi);
//             double eta = y - 1 - Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho <= 4.0)
//             {
//                 double t, u, v;
//                 if (rho <= 2.0)
//                 {
//                     double A = Math.Acos((rho + 2.0) / 4.0);
//                     t = Utils.M(theta + Math.PI / 2.0 + A);
//                     u = Utils.M(A);
//                     v = Utils.M(phi - t + 2.0 * u);
//                 }
//                 else
//                 {
//                     double A = Math.Acos((rho - 2.0) / 4.0);
//                     t = Utils.M(theta + Math.PI / 2.0 - A);
//                     u = Utils.M(Math.PI - A);
//                     v = Utils.M(phi - t + 2.0 * u);
//                 }
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.RIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.LEFT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path7(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x + Math.Sin(phi);
//             double eta = y - 1 - Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             double u1 = (20.0 - rho * rho) / 16.0;
//             if (rho <= 6.0 && u1 >= 0.0 && u1 <= 1.0)
//             {
//                 double u = Math.Acos(u1);
//                 double A = Math.Asin(2.0 * Math.Sin(u) / rho);
//                 double t = Utils.M(theta + Math.PI / 2.0 + A);
//                 double v = Utils.M(t - phi);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(u, Steering.LEFT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path8(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x - Math.Sin(phi);
//             double eta = y - 1 + Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho >= 2.0)
//             {
//                 double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
//                 double A = Math.Atan2(2.0, u + 2.0);
//                 double t = Utils.M(theta + Math.PI / 2.0 + A);
//                 double v = Utils.M(t - phi + Math.PI / 2.0);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path9(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x - Math.Sin(phi);
//             double eta = y - 1 + Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho >= 2.0)
//             {
//                 double u = Math.Sqrt(rho * rho - 4.0) - 2.0;
//                 double A = Math.Atan2(u + 2.0, 2.0);
//                 double t = Utils.M(theta + Math.PI / 2.0 - A);
//                 double v = Utils.M(t - phi - Math.PI / 2.0);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(v, Steering.LEFT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path10(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x + Math.Sin(phi);
//             double eta = y - 1 - Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho >= 2.0)
//             {
//                 double t = Utils.M(theta + Math.PI / 2.0);
//                 double u = rho - 2.0;
//                 double v = Utils.M(phi - t - Math.PI / 2.0);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path11(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x + Math.Sin(phi);
//             double eta = y - 1 - Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho >= 2.0)
//             {
//                 double t = Utils.M(theta);
//                 double u = rho - 2.0;
//                 double v = Utils.M(phi - t - Math.PI / 2.0);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.FORWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.BACKWARD));
//             }
//             return path;
//         }

//         public static List<PathElement> Path12(double x, double y, double phi)
//         {
//             var path = new List<PathElement>();
//             double xi = x + Math.Sin(phi);
//             double eta = y - 1 - Math.Cos(phi);
//             var (rho, theta) = Utils.R(xi, eta);
//             if (rho >= 4.0)
//             {
//                 double u = Math.Sqrt(rho * rho - 4.0) - 4.0;
//                 double A = Math.Atan2(2.0, u + 4.0);
//                 double t = Utils.M(theta + Math.PI / 2.0 + A);
//                 double v = Utils.M(t - phi);
//                 path.Add(PathElement.Create(t, Steering.LEFT, Gear.FORWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.RIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(u, Steering.STRAIGHT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(Math.PI / 2.0, Steering.LEFT, Gear.BACKWARD));
//                 path.Add(PathElement.Create(v, Steering.RIGHT, Gear.FORWARD));
//             }
//             return path;
//         }

//         // ----- symmetries (time-flip reverses order + gear) -----
//         public static List<PathElement> Timeflip(List<PathElement> path)
//         {
//             return path.Select(e => e.ReverseGear()).ToList(); // matches reference
//         }


//         public static List<PathElement> Reflect(List<PathElement> path)
//             => path.Select(e => new PathElement(e.Param, (Steering)(-(int)e.Steering), e.Gear)).ToList();

//         // ----- planner API: start/end in RADIANS, x/y normalized by R -----
//         public static List<List<PathElement>> GetAllPaths(
//             (double x, double y, double theta) start,
//             (double x, double y, double theta) end)
//         {
//             var local = Utils.ChangeOfBasis(start, end);      // radians in, radians out
//             double x = local.x, y = local.y, phi = Utils.M(local.theta); // ensure [0,2π)

//             var candidates = new List<List<PathElement>>
//         {
//             Path1(x,y,phi),  Path2(x,y,phi),  Path3(x,y,phi),  Path4(x,y,phi),
//             Path5(x,y,phi),  Path6(x,y,phi),  Path7(x,y,phi),  Path8(x,y,phi),
//             Path9(x,y,phi),  Path10(x,y,phi), Path11(x,y,phi), Path12(x,y,phi)
//         };

//             // Re-enable the 3 symmetry variants for full 48-path coverage
//             var more = new List<List<PathElement>>();
//             foreach (var p in candidates)
//             {
//                 if (p.Count == 0) continue;
//                 more.Add(Timeflip(p));
//                 more.Add(Reflect(p));
//                 more.Add(Timeflip(Reflect(p)));
//             }
//             candidates.AddRange(more);

//             return candidates.Where(p => p.Count > 0).ToList();
//         }

//         public static List<PathElement> GetOptimalPath(
//             (double x, double y, double theta) start,
//             (double x, double y, double theta) end)
//         {
//             var all = GetAllPaths(start, end);
//             return (all.Count == 0) ? new List<PathElement>() : all.OrderBy(p => p.Sum(e => e.Param)).First();
//         }
//     }
// }