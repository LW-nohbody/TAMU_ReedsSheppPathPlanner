// res://Scripts/Services/Planning/HybridPlanner.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.Domain;

namespace DigSim3D.Services
{
    public static class HybridPlanner
    {
        // exactly like your 3D version’s defaults
        public static (List<Vector3> pts, List<int> gears) Plan(
            Vector3 startPos, double startYaw,
            Vector3 goalPos, double goalYaw,
            float turnRadiusMeters,
            float sampleStepMeters,
            List<Obstacle3D> obstacles,
            float obstacleBufferMeters = 0.50f,
            int maxAttempts = 200)
        {
            // If goal lies inside inflated forbidden area, nudge it out (3D behavior)
            if (IsInsideInflated(goalPos, obstacles, obstacleBufferMeters))
                goalPos = PushOutside(goalPos, obstacles, obstacleBufferMeters);

            // 1) direct RS
            var (rsPtsArr, rsGearsArr) = RSAdapter.ComputePath3D(
                startPos, startYaw, goalPos, goalYaw,
                turnRadiusMeters, sampleStepMeters);

            var rsPts = rsPtsArr?.ToList() ?? new List<Vector3>();
            var rsGears = rsGearsArr?.ToList() ?? new List<int>();

            if (obstacles == null || obstacles.Count == 0 || PathIsClear(rsPts, obstacles, obstacleBufferMeters))
                return (rsPts, rsGears);

            GD.Print("[HybridPlanner] RS blocked → grid fallback.");

            // Ensure grid uses the same params as 3D (BuildGrid called by director)
            var gridPath = GridPlannerPersistent.Plan2DPath(startPos, goalPos);
            if (gridPath == null || gridPath.Count < 3)
                return (rsPts, rsGears);

            // try midpoints from center outward
            int n = gridPath.Count;
            int center = n / 2;
            var tried = new HashSet<int>();
            int attempts = 0;

            for (int offset = 0; offset <= n && attempts < maxAttempts; offset++)
            {
                foreach (int idx in new[] { center - offset, center + offset })
                {
                    if (idx <= 0 || idx >= n - 1) continue;
                    if (!tried.Add(idx)) continue;
                    attempts++;

                    var mid = gridPath[idx];

                    // yaw hints like 3D
                    Vector3 startTan = (gridPath[Math.Min(1, n - 1)] - gridPath[0]); startTan.Y = 0; if (startTan.LengthSquared() < 1e-8f) startTan = Vector3.Right;
                    startTan = startTan.Normalized();

                    Vector3 prevT = (gridPath[idx] - gridPath[idx - 1]); prevT.Y = 0;
                    Vector3 nextT = (gridPath[Math.Min(idx + 1, n - 1)] - gridPath[idx]); nextT.Y = 0;
                    Vector3 midTan = (prevT.Normalized() + nextT.Normalized()); if (midTan.LengthSquared() < 1e-8f) midTan = prevT;

                    double sYaw = Math.Atan2(startTan.Z, startTan.X);
                    double mYaw = Math.Atan2(midTan.Z, midTan.X);

                    var (r1PtsA, r1GearsA) = RSAdapter.ComputePath3D(startPos, sYaw, mid, mYaw, turnRadiusMeters, sampleStepMeters);
                    var (r2PtsA, r2GearsA) = RSAdapter.ComputePath3D(mid, mYaw, goalPos, goalYaw, turnRadiusMeters, sampleStepMeters);

                    var r1 = r1PtsA?.ToList() ?? new List<Vector3>();
                    var g1 = r1GearsA?.ToList() ?? new List<int>();
                    var r2 = r2PtsA?.ToList() ?? new List<Vector3>();
                    var g2 = r2GearsA?.ToList() ?? new List<int>();

                    if (r1.Count == 0 || r2.Count == 0) continue;
                    if (!PathIsClear(r1, obstacles, obstacleBufferMeters)) continue;
                    if (!PathIsClear(r2, obstacles, obstacleBufferMeters)) continue;

                    var mergedPts = new List<Vector3>(r1);
                    var mergedG = new List<int>(g1);

                    int skip = (mergedPts.Count > 0 && r2.Count > 0 && mergedPts[^1].DistanceTo(r2[0]) < 1e-3f) ? 1 : 0;
                    mergedPts.AddRange(r2.Skip(skip));
                    mergedG.AddRange(g2.Skip(skip));

                    if (mergedG.Count > 0)
                    {
                        mergedG[0] = g1.Count > 0 ? g1[0] : 1;
                        mergedG[^1] = g2.Count > 0 ? g2[^1] : mergedG[^1];
                    }

                    return (mergedPts, mergedG);
                }
            }

            return (rsPts, rsGears);
        }

        // ===== helpers =====
        private static bool PathIsClear(List<Vector3> samples, List<Obstacle3D> obstacles, float buffer)
        {
            if (samples == null || samples.Count < 2) return true;

            for (int i = 1; i < samples.Count; i++)
            {
                var a = samples[i - 1];
                var b = samples[i];

                foreach (var o in obstacles)
                {
                    if (o.Shape == ObstacleShape.Cylinder)
                    {
                        if (SegmentIntersectsInflatedCylinderXZ(a, b, o.Center, o.Radius + buffer))
                            return false;
                    }
                    else
                    {
                        if (SegmentIntersectsInflatedAabbXZ(a, b, o.Center, o.Extents, buffer))
                            return false;
                    }
                }
            }
            return true;
        }

        // --- geometry helpers (XZ-plane) ---

        // true if distance from segment AB to circle center C in XZ <= r
        private static bool SegmentIntersectsInflatedCylinderXZ(Vector3 a, Vector3 b, Vector3 c, float r)
        {
            var axz = new Vector2(a.X, a.Z);
            var bxz = new Vector2(b.X, b.Z);
            var cxz = new Vector2(c.X, c.Z);

            var ab = bxz - axz;
            var len2 = ab.LengthSquared();
            if (len2 <= 1e-8f)
                return (axz - cxz).LengthSquared() <= r * r;

            var t = Mathf.Clamp((cxz - axz).Dot(ab) / len2, 0f, 1f);
            var closest = axz + ab * t;
            return (closest - cxz).LengthSquared() <= r * r;
        }

        // true if segment AB intersects an axis-aligned AABB (inflated in XZ by buffer)
        private static bool SegmentIntersectsInflatedAabbXZ(Vector3 a, Vector3 b, Vector3 center, Vector3 halfExtents, float buffer)
        {
            float minX = center.X - (Mathf.Max(0f, halfExtents.X) + buffer);
            float maxX = center.X + (Mathf.Max(0f, halfExtents.X) + buffer);
            float minZ = center.Z - (Mathf.Max(0f, halfExtents.Z) + buffer);
            float maxZ = center.Z + (Mathf.Max(0f, halfExtents.Z) + buffer);

            // Liang–Barsky style clip in XZ
            float x0 = a.X, z0 = a.Z, x1 = b.X, z1 = b.Z;
            float dx = x1 - x0, dz = z1 - z0;
            float t0 = 0f, t1 = 1f;

            bool Clip(float p, float q, ref float tt0, ref float tt1)
            {
                if (Mathf.IsZeroApprox(p)) return q >= 0; // parallel: keep if inside
                float t = q / p;
                if (p < 0) { if (t > tt1) return false; if (t > tt0) tt0 = t; }
                else { if (t < tt0) return false; if (t < tt1) tt1 = t; }
                return true;
            }

            if (!Clip(-dx, x0 - minX, ref t0, ref t1)) return false;
            if (!Clip(dx, maxX - x0, ref t0, ref t1)) return false;
            if (!Clip(-dz, z0 - minZ, ref t0, ref t1)) return false;
            if (!Clip(dz, maxZ - z0, ref t0, ref t1)) return false;

            return t0 <= t1;
        }

        private static bool IsInsideInflated(Vector3 x, List<Obstacle3D> obstacles, float buffer)
        {
            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    var dx = x.X - o.Center.X; var dz = x.Z - o.Center.Z;
                    if ((dx * dx + dz * dz) < Math.Pow(o.Radius + buffer, 2)) return true;
                }
                else
                {
                    float hx = Math.Max(0f, o.Extents.X) + buffer;
                    float hz = Math.Max(0f, o.Extents.Z) + buffer;
                    if (x.X >= o.Center.X - hx && x.X <= o.Center.X + hx &&
                        x.Z >= o.Center.Z - hz && x.Z <= o.Center.Z + hz) return true;
                }
            }
            return false;
        }

        private static Vector3 PushOutside(Vector3 goal, List<Obstacle3D> obstacles, float buffer)
        {
            // simple radial nudge away from nearest obstacle (matches 3D behavior)
            float best = float.MaxValue;
            Vector3 n = Vector3.Zero;
            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    var d = goal - o.Center; d.Y = 0;
                    var dist = d.Length();
                    var rInfl = o.Radius + buffer + 0.05f;
                    if (dist < rInfl && dist < best)
                    {
                        best = dist;
                        n = d.LengthSquared() > 1e-6 ? d / dist : Vector3.Right;
                        goal = o.Center + n * rInfl;
                    }
                }
            }
            return goal;
        }
    }
}