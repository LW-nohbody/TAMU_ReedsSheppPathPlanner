using Godot;
using System;
using System.Collections.Generic;
using PathPlanningLib.Algorithms.ReedsShepp;          // ReedsShepp solver
using PathPlanningLib.Algorithms.Geometry.Paths;      // ReedsSheppPath, PosePath
using PathPlanningLib.Algorithms.Geometry.PathElements; // Pose
using DigSim3D.Debugging;

public static class RSAdapter
{
    private static (double x, double y, double th) ToMathXZ(Vector3 pos, double yawRad)
        => (pos.X, pos.Z, yawRad);

    public static (Vector3[] points, int[] gears) ComputePath3D(
        Vector3 startPos, double startYawRad,
        Vector3 goalPos, double goalYawRad,
        double turnRadiusMeters,
        double sampleStepMeters = 0.25)
    {
        // 1) world (x,z,yaw) -> library Pose in *normalized* units (R=1)
        double R = turnRadiusMeters;

        var sWorld = ToMathXZ(startPos, startYawRad);
        var gWorld = ToMathXZ(goalPos, goalYawRad);

        var sNorm = Pose.Create(sWorld.x / R, sWorld.y / R, sWorld.th);
        var gNorm = Pose.Create(gWorld.x / R, gWorld.y / R, gWorld.th);

        DebugPath.Check("digsim.adapter", "inputs_norm",
            ("sN.x", sNorm.X), ("sN.y", sNorm.Y), ("sN.th", sNorm.Theta),
            ("gN.x", gNorm.X), ("gN.y", gNorm.Y), ("gN.th", gNorm.Theta));

        // 2) solve optimal RS in normalized space
        var solver = new ReedsShepp();
        var rsPath = solver.GetOptimalPath(sNorm, gNorm);

        DebugPath.Check("digsim.adapter", "solver",
            ("elemCount", rsPath.Count));

        if (rsPath.Count == 0) return (Array.Empty<Vector3>(), Array.Empty<int>());

        // 3) sample WITH gears in normalized local frame starting at (0,0,0)
        var (posePathNorm, gears) = rsPath.SampleWithGears(
            stepSize: sampleStepMeters / R,  // normalized step
            turningRadius: 1.0,              // normalized radius
            startPose: Pose.Create(0, 0, 0));

        if (posePathNorm.Count > 0)
        {
            var last = posePathNorm.Elements[^1];
            DebugPath.Check("digsim.adapter", "sample_out_norm",
                ("nPts", posePathNorm.Count),
                ("last.x", last.X), ("last.y", last.Y), ("last.th", last.Theta));
        }

        // 4) map normalized local -> world XZ (rotate by start yaw, scale by R, translate by start pos)
        var pts = new List<Vector3>(posePathNorm.Count);
        double c0 = Math.Cos(sWorld.th), s0 = Math.Sin(sWorld.th);
        foreach (var p in posePathNorm.Elements)
        {
            double sx = p.X * R;
            double sy = -p.Y * R;
            double wx = sWorld.x + (sx * c0 - sy * s0);
            double wz = sWorld.y + (sx * s0 + sy * c0);
            pts.Add(new Vector3((float)wx, 0f, (float)wz));
        }

        if (pts.Count > 0)
        {
            var end = pts[^1];
            DebugPath.Check("digsim.adapter", "mapped_world", ("nPts", pts.Count), ("last", end));
        }

        return (pts.ToArray(), gears.ToArray());
    }
}