using Godot;
using System;
using System.Collections.Generic;

// public static class RsSampler
// {
//     // startWorld: (x,y,thetaRadians) in world units; path normalized (R=1); R scales to world units
//     public static Vector2[] SamplePolyline(
//         (double x, double y, double theta) startWorld,
//         List<PathElement> path,
//         double R,
//         double ds = 4.0)
//     {
//         var pts = new List<Vector2>();
//         double x = startWorld.x, y = startWorld.y, th = startWorld.theta;
//         pts.Add(new Vector2((float)x, (float)y));

//         foreach (var seg in path)
//         {
//             if (seg.Steering == Steering.STRAIGHT)
//             {
//                 double len = seg.Param * R;                    // straight length
//                 double dir = seg.Gear == Gear.FORWARD ? 1 : -1;

//                 double remaining = len;
//                 while (remaining > 1e-6)
//                 {
//                     double step = Math.Min(ds, remaining);
//                     x += dir * step * Math.Cos(th);
//                     y += dir * step * Math.Sin(th);
//                     pts.Add(new Vector2((float)x, (float)y));
//                     remaining -= step;
//                 }
//             }
//             else
//             {
//                 int steerSign = seg.Steering == Steering.LEFT ? +1 : -1;   // circle side
//                 int gearSign  = seg.Gear     == Gear.FORWARD   ? +1 : -1;   // travel direction

//                 // total signed heading change (radians)
//                 double total = seg.Param * steerSign * gearSign;

//                 // geometry scale = 1/|k| with sign from STEER ONLY
//                 double invk = R * steerSign; // <-- key fix

//                 double remaining = Math.Abs(total);
//                 double dTheta = ds / R; // ds = R * dθ (magnitude)

//                 while (remaining > 1e-6)
//                 {
//                     double dth = Math.Min(dTheta, remaining) * Math.Sign(total);
//                     double thPrev = th;
//                     th += dth;

//                     // y-up update for constant-curvature arc:
//                     x += (Math.Sin(th) - Math.Sin(thPrev)) * invk;
//                     y += -(Math.Cos(th) - Math.Cos(thPrev)) * invk;

//                     pts.Add(new Vector2((float)x, (float)y));
//                     remaining -= Math.Abs(dth);
//                 }
//             }
//         }
//         return pts.ToArray();
//     }
    
//     public static Vector2[] SamplePolylineExact(
//     (double x, double y, double theta) startWorldMath,
//     List<PathElement> path,
//     double R,
//     double dsApprox = 4.0) // dsApprox only controls point density
//     {
//         var pts = new List<Vector2>();
//         double x = startWorldMath.x, y = startWorldMath.y, th = startWorldMath.theta;
//         pts.Add(new Vector2((float)x, (float)y));

//         // choose how many points per segment based on nominal length
//         int PointsFor(double length) => Math.Max(1, (int)Math.Ceiling((length * R) / dsApprox));

//         foreach (var seg in path)
//         {
//             if (seg.Steering == Steering.STRAIGHT)
//             {
//                 double s = seg.Param * (seg.Gear == Gear.FORWARD ? 1.0 : -1.0); // normalized
//                 int n = PointsFor(Math.Abs(s));
//                 for (int i = 1; i <= n; i++)
//                 {
//                     double t = (double)i / n;
//                     double ss = s * t;
//                     double xx = x + ss * Math.Cos(th);
//                     double yy = y + ss * Math.Sin(th);
//                     pts.Add(new Vector2((float)xx, (float)yy));
//                 }
//                 x += s * Math.Cos(th);
//                 y += s * Math.Sin(th);
//             }
//             else
//             {
//                 int steerSign = seg.Steering == Steering.LEFT ? +1 : -1;   // circle side
//                 int gearSign  = seg.Gear     == Gear.FORWARD   ? +1 : -1;   // travel direction

//                 double total = seg.Param;             // magnitude (≥ 0)
//                 int n = PointsFor(total);
//                 double invk = R * steerSign;          // <-- key fix

//                 for (int i = 1; i <= n; i++)
//                 {
//                     double t = (double)i / n;
//                     double dth = gearSign * steerSign * (total * t);
//                     double thNew = th + dth;

//                     double xx = x + (Math.Sin(thNew) - Math.Sin(th)) * invk;
//                     double yy = y - (Math.Cos(thNew) - Math.Cos(th)) * invk;

//                     pts.Add(new Vector2((float)xx, (float)yy));
//                 }

//                 // advance to segment end
//                 double dthTot = gearSign * steerSign * total;
//                 double thPrev = th;
//                 th += dthTot;

//                 x += (Math.Sin(th) - Math.Sin(thPrev)) * invk;
//                 y += -(Math.Cos(th) - Math.Cos(thPrev)) * invk;
//             }
//         }
//         return pts.ToArray();
//     }
// }
