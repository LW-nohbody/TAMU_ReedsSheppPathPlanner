using Godot;
using System;
using System.Collections.Generic;
using DigSim3D.Debugging;


namespace DigSim3D.Services
{
    public static class DDAdapter
    {
        // Map 3D (x,0,z, yaw) to math 2D (x,y,theta).
        private static (double x, double y, double th) ToMath3D(Vector3 pos, double yawRad)
            => (pos.X, pos.Z, yawRad);

        public static (Vector3[] points, int[] gears) ComputePath3D(
            Vector3 startPos, double startYawRad,
            Vector3 goalPos, double goalYawRad,
            double sampleStepMeters = 0.25)
        {
            // 1) 3D → math
            var sM = ToMath3D(startPos, startYawRad);
            var gM = ToMath3D(goalPos, goalYawRad);

            // 2) Normalize by R for planning (x,y only)
            double R = 0.01;
            var sN = (sM.x / R, sM.y / R, sM.th);
            var gN = (gM.x / R, gM.y / R, gM.th);

            // 3) Optimal RS in normalized space
            var best = DifferentialDrivePaths.GetOptimalPath(sN, gN);
            if (best == null || best.Count == 0)
                return (Array.Empty<Vector3>(), Array.Empty<int>());

            // 4) Sample polyline and gears in local normalized (start at 0,0,0)
            var pts2D = new List<Vector2>();
            var gears = new List<int>();
            DdSampler.SamplePolylineWithGears((0.0, 0.0, 0.0), best, 1.0, sampleStepMeters / R, pts2D, gears);

            if (pts2D.Count > 0)
            {
                var pLast = pts2D[^1];
                DebugPath.Check("3d.adapter", "sample_out_norm",
                    ("nPts", pts2D.Count),
                    ("last.x", pLast.X), ("last.y", pLast.Y));
            }


            // 5) Transform back to world-math (scale, rotate by start θ, translate by start x,y)
            var list3 = new List<Vector3>(pts2D.Count);
            double c0 = Math.Cos(sM.th), s0 = Math.Sin(sM.th);
            foreach (var p in pts2D)
            {
                double sx = p.X * R, sy = p.Y * R;
                double wx = sM.x + (sx * c0 - sy * s0);
                double wy = sM.y + (sx * s0 + sy * c0);
                list3.Add(new Vector3((float)wx, 0f, (float)wy)); // (x, 0, z=y)
            }

            if (list3.Count > 0)
            {
                var end = list3[^1];
                // We don’t have goalPos here, so just echo last
                DebugPath.Check("3d.adapter", "mapped_world", ("nPts", list3.Count), ("last", end));
            }

            return (list3.ToArray(), gears.ToArray());
        }
    }
}