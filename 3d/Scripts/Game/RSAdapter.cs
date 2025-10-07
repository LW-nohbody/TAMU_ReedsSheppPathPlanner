using Godot;
using RSCore;
using System;
using System.Collections.Generic;

public static class RSAdapter
{
    // Map 3D (x,0,z, yaw) to math 2D (x,y,theta).
    private static (double x, double y, double th) ToMath3D(Vector3 pos, double yawRad)
        => (pos.X, pos.Z, yawRad);

    public static Vector3[] ComputePath3D(
        Vector3 startPos, double startYawRad,
        Vector3 goalPos,  double goalYawRad,
        double turnRadiusMeters,
        double sampleStepMeters = 0.25)
    {
        // 1) 3D → math
        var sM = ToMath3D(startPos, startYawRad);
        var gM = ToMath3D(goalPos,  goalYawRad);

        // 2) Normalize by R for planning (x,y only)
        double R = turnRadiusMeters;
        var sN = (sM.x / R, sM.y / R, sM.th);
        var gN = (gM.x / R, gM.y / R, gM.th);

        // 3) Optimal RS in normalized space
        var best = ReedsSheppPaths.GetOptimalPath(sN, gN);
        if (best == null || best.Count == 0)
            return Array.Empty<Vector3>();

        // 4) Sample polyline in local normalized (start at 0,0,0)
        var ptsLocalNorm = RsSampler.SamplePolylineExact((0.0, 0.0, 0.0), best, 1.0, sampleStepMeters / R);

        // 5) Transform back to world-math (scale, rotate by start θ, translate by start x,y)
        var list3 = new List<Vector3>(ptsLocalNorm.Length);
        double c0 = Math.Cos(sM.th), s0 = Math.Sin(sM.th);
        foreach (var p in ptsLocalNorm)
        {
            double sx = p.X * R, sy = p.Y * R;
            double wx = sM.x + (sx * c0 - sy * s0);
            double wy = sM.y + (sx * s0 + sy * c0);
            // math (x, y) -> 3D (x, 0, z=y)
            list3.Add(new Vector3((float)wx, 0f, (float)wy));
        }
        return list3.ToArray();
    }
}