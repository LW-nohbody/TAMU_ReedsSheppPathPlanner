// res://Scripts/Services/Planning/HybridPlanner.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.Domain;

namespace DigSim3D.Services
{
    /// <summary>
    /// Hybrid path planner:
    /// 1) Try direct Reeds–Shepp (RS).
    /// 2) If blocked, pull cached A* grid path, simplify it, and stitch RS
    ///    segments along that simplified route with greedy skipping,
    ///    midpoint subdivision, and post-simplification.
    /// </summary>
    public static class HybridPlanner
    {
        public static (List<Vector3> pts, List<int> gears) Plan(
            Vector3 startPos, double startYaw,
            Vector3 goalPos, double goalYaw,
            float turnRadiusMeters,
            float sampleStepMeters,
            List<Obstacle3D> obstacles,
            float obstacleBufferMeters = 0.50f,
            int maxAttempts = 2000)
        {
            // 0) Safety nudge: if goal is illegally inside an inflated obstacle, push it out.
            if (IsInsideInflated(goalPos, obstacles, obstacleBufferMeters))
                goalPos = PushOutside(goalPos, obstacles, obstacleBufferMeters);

            // 1) Direct RS attempt
            var (rsPtsArr, rsGearsArr) = RSAdapter.ComputePath3D(
                startPos, startYaw,
                goalPos, goalYaw,
                turnRadiusMeters,
                sampleStepMeters
            );

            var rsPtsDirect  = rsPtsArr?.ToList()   ?? new List<Vector3>();
            var rsGearsDirect = rsGearsArr?.ToList() ?? new List<int>();

            if (obstacles == null || obstacles.Count == 0 ||
                PathIsClearDetailed(rsPtsDirect, obstacles, obstacleBufferMeters))
            {
                // Direct RS is good enough.
                // (In 3D we sometimes *still* visualized grid, but we can early out here.)
                return (rsPtsDirect, rsGearsDirect);
            }

            // GD.Print("[HybridPlanner] Direct RS is blocked. Falling back to cached A* grid path.");

            // 2) Get cached A* path
            var gridPath = GridPlannerPersistent.Plan2DPath(startPos, goalPos);

            // optional debug overlay like 3D:
            // DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, gridPath);

            if (gridPath == null || gridPath.Count < 3)
            {
                GD.PrintErr("[HybridPlanner] Grid path failed/too small. Returning fallback RS.");
                return (rsPtsDirect, rsGearsDirect);
            }

            // 3) Hybrid replan using the upgraded stitching logic
            var merged = TryReplanWithMidpoints(
                startPos,
                goalPos,
                turnRadiusMeters,
                sampleStepMeters,
                gridPath,
                obstacles,
                obstacleBufferMeters,
                startGear: 1,
                goalYaw: goalYaw
            );

            if (merged.points != null && merged.points.Count > 1)
            {
                // success
                return (merged.points, merged.gears);
            }

            GD.PrintErr("[HybridPlanner] Could not stitch clean RS via A* waypoints. Using fallback RS.");
            return (rsPtsDirect, rsGearsDirect);
        }


        // ================================================================
        // Replanning logic (ported / adapted from SimCore.Services version)
        // ================================================================
        private static (List<Vector3> points, List<int> gears) TryReplanWithMidpoints(
            Vector3 start,
            Vector3 goal,
            double turnRadius,
            float sampleStep,
            List<Vector3> gridPath,
            List<Obstacle3D> obstacles,
            float obstacleBuffer,
            int startGear,
            double goalYaw)
        {
            if (gridPath == null || gridPath.Count < 2)
                return (null, null);

            // --- 1) Simplify the A* path by removing nearly-collinear interior points ---
            var simplifiedPath = new List<Vector3> { gridPath[0] };
            const float angleThresholdDeg = 5f;
            float angleThreshold = angleThresholdDeg * (float)(Math.PI / 180.0);

            for (int i = 1; i < gridPath.Count - 1; i++)
            {
                Vector3 prev = simplifiedPath.Last();
                Vector3 curr = gridPath[i];
                Vector3 next = gridPath[i + 1];

                Vector3 v1 = (curr - prev);
                v1.Y = 0;
                v1 = v1.Normalized();

                Vector3 v2 = (next - curr);
                v2.Y = 0;
                v2 = v2.Normalized();

                float dot = v1.Dot(v2);
                dot = Math.Clamp(dot, -1f, 1f);
                float angle = (float)Math.Acos(dot);

                if (Math.Abs(angle) > angleThreshold)
                    simplifiedPath.Add(curr);
            }

            simplifiedPath.Add(gridPath[^1]);

            // --- 2) Greedily stitch RS segments along that simplified path ---
            var mergedPoints = new List<Vector3> { start };
            var mergedGears = new List<int> { startGear };

            // prevYaw is "what direction are we currently facing"
            double prevYaw = Math.Atan2(
                (simplifiedPath[0] - start).Z,
                (simplifiedPath[0] - start).X
            );

            int index = 0;
            while (index < simplifiedPath.Count)
            {
                Vector3 segStart = mergedPoints.Last();
                int farthestReachable = index;

                // Try skipping as far forward in simplifiedPath as we can
                for (int j = index; j < simplifiedPath.Count; j++)
                {
                    Vector3 segEnd = simplifiedPath[j];
                    double segYaw = Math.Atan2(
                        (segEnd - segStart).Z,
                        (segEnd - segStart).X
                    );

                    var (rsTestSegArr, rsTestGearsArr) = RSAdapter.ComputePath3D(
                        segStart, prevYaw,
                        segEnd, segYaw,
                        turnRadius,
                        sampleStep
                    );

                    var rsTestSeg = rsTestSegArr?.ToList() ?? new List<Vector3>();
                    if (rsTestSeg.Count == 0)
                        break;

                    if (!PathIsClearDetailed(rsTestSeg, obstacles, obstacleBuffer))
                        break;

                    farthestReachable = j;
                }

                Vector3 target = simplifiedPath[farthestReachable];
                double targetYaw = Math.Atan2(
                    (target - segStart).Z,
                    (target - segStart).X
                );

                var (rsSegmentArr, rsGearsArr) = RSAdapter.ComputePath3D(
                    segStart, prevYaw,
                    target, targetYaw,
                    turnRadius,
                    sampleStep
                );

                var rsSegment = rsSegmentArr?.ToList() ?? new List<Vector3>();
                var rsGearsLocal = rsGearsArr?.ToList() ?? new List<int>();

                // If that direct segment is blocked, do recursive midpoint subdivision fallback
                int subdiv = 0;
                while ((rsSegment.Count == 0 ||
                        !PathIsClearDetailed(rsSegment, obstacles, obstacleBuffer)) &&
                       subdiv < 5)
                {
                    subdiv++;

                    // midpoint in world space
                    Vector3 mid = segStart.Lerp(target, 0.5f);
                    double midYaw = Math.Atan2(
                        (mid - segStart).Z,
                        (mid - segStart).X
                    );

                    var (rs1Arr, gears1Arr) = RSAdapter.ComputePath3D(
                        segStart, prevYaw,
                        mid, midYaw,
                        turnRadius,
                        sampleStep
                    );

                    var (rs2Arr, gears2Arr) = RSAdapter.ComputePath3D(
                        mid, midYaw,
                        target, targetYaw,
                        turnRadius,
                        sampleStep
                    );

                    var rs1 = rs1Arr?.ToList() ?? new List<Vector3>();
                    var g1  = gears1Arr?.ToList() ?? new List<int>();
                    var rs2 = rs2Arr?.ToList() ?? new List<Vector3>();
                    var g2  = gears2Arr?.ToList() ?? new List<int>();

                    if (rs1.Count == 0 || rs2.Count == 0)
                        break;

                    if (!PathIsClearDetailed(rs1, obstacles, obstacleBuffer) ||
                        !PathIsClearDetailed(rs2, obstacles, obstacleBuffer))
                        break;

                    // Merge rs1 and rs2, skipping duplicate midpoint
                    mergedPoints.AddRange(rs1.Skip(1));
                    mergedGears.AddRange(g1.Skip(1));

                    mergedPoints.AddRange(rs2.Skip(1));
                    mergedGears.AddRange(g2.Skip(1));

                    prevYaw = targetYaw;
                    rsSegment = new List<Vector3>(); // mark "handled"
                }

                // If we never successfully subdivided, and rsSegment is good, append it
                if (rsSegment.Count > 0 &&
                    PathIsClearDetailed(rsSegment, obstacles, obstacleBuffer))
                {
                    mergedPoints.AddRange(rsSegment.Skip(1));
                    mergedGears.AddRange(rsGearsLocal.Skip(1));
                    prevYaw = targetYaw;
                }

                index = farthestReachable + 1;
            }

            // --- 3) Post-process: attempt to simplify the final RS path itself
            var simplifiedRSPoints = new List<Vector3> { mergedPoints[0] };
            var simplifiedGears    = new List<int>   { mergedGears[0] };

            int cur = 0;
            while (cur < mergedPoints.Count - 1)
            {
                int farthest = cur + 1;

                // We'll try to "jump ahead" as far as we can from 'cur'
                for (int j = mergedPoints.Count - 1; j > cur; j--)
                {
                    double segYaw = Math.Atan2(
                        (mergedPoints[j] - mergedPoints[cur]).Z,
                        (mergedPoints[j] - mergedPoints[cur]).X
                    );

                    var (rsTestArr, rsTestGearsArr) = RSAdapter.ComputePath3D(
                        mergedPoints[cur], // start pos
                        prevYaw,           // assumed heading
                        mergedPoints[j],   // end pos
                        segYaw,            // end heading guess
                        turnRadius,
                        sampleStep
                    );

                    var rsTest = rsTestArr?.ToList() ?? new List<Vector3>();
                    if (rsTest.Count == 0)
                        continue;

                    if (!PathIsClearDetailed(rsTest, obstacles, obstacleBuffer))
                        continue;

                    // success: we can skip straight to j
                    farthest = j;
                    break;
                }

                // Generate and append that new RS jump for real
                double yawToAdd = Math.Atan2(
                    (mergedPoints[farthest] - mergedPoints[cur]).Z,
                    (mergedPoints[farthest] - mergedPoints[cur]).X
                );

                var (rsFinalArr, rsFinalGearsArr) = RSAdapter.ComputePath3D(
                    mergedPoints[cur], prevYaw,
                    mergedPoints[farthest], yawToAdd,
                    turnRadius,
                    sampleStep
                );

                var rsFinal = rsFinalArr?.ToList() ?? new List<Vector3>();
                var rsFinalGears = rsFinalGearsArr?.ToList() ?? new List<int>();

                if (rsFinal.Count > 0)
                {
                    simplifiedRSPoints.AddRange(rsFinal.Skip(1));
                    simplifiedGears.AddRange(rsFinalGears.Skip(1));
                    prevYaw = yawToAdd;
                }

                cur = farthest;
            }

            if (simplifiedRSPoints.Count > 1 && simplifiedGears.Count > 0)
            {
                // enforce forward gear at end
                simplifiedGears[^1] = 1;
            }

            return (simplifiedRSPoints, simplifiedGears);
        }


        // ================================================================
        // Optional recursive helper (not strictly required in main flow)
        // ================================================================
        private static (List<Vector3>, List<int>) ComputeRSWithSubdivision(
            Vector3 start,
            Vector3 end,
            double startYaw,
            double endYaw,
            double turnRadius,
            float sampleStep,
            List<Obstacle3D> obstacles,
            float obstacleBuffer,
            int depth,
            int maxDepth)
        {
            if (depth > maxDepth)
                return (null, null);

            var (segArr, gearsArr) = RSAdapter.ComputePath3D(
                start, startYaw,
                end, endYaw,
                turnRadius,
                sampleStep
            );

            var seg = segArr?.ToList() ?? new List<Vector3>();
            var g   = gearsArr?.ToList() ?? new List<int>();

            if (seg.Count > 0 && PathIsClearDetailed(seg, obstacles, obstacleBuffer))
                return (seg, g);

            // subdivide at midpoint
            Vector3 mid = start.Lerp(end, 0.5f);
            double midYaw = Math.Atan2((mid - start).Z, (mid - start).X);

            var (firstHalf, g1) = ComputeRSWithSubdivision(
                start, mid,
                startYaw, midYaw,
                turnRadius,
                sampleStep,
                obstacles,
                obstacleBuffer,
                depth + 1, maxDepth);

            var (secondHalf, g2) = ComputeRSWithSubdivision(
                mid, end,
                midYaw, endYaw,
                turnRadius,
                sampleStep,
                obstacles,
                obstacleBuffer,
                depth + 1, maxDepth);

            if (firstHalf == null || secondHalf == null)
                return (null, null);

            var mergedPts = new List<Vector3>(firstHalf);
            mergedPts.AddRange(secondHalf.Skip(1));

            var mergedGears = new List<int>(g1);
            mergedGears.AddRange(g2.Skip(1));

            return (mergedPts, mergedGears);
        }


        // ================================================================
        // Collision / clearance helpers
        // ================================================================
        /// <summary>
        /// DigSim3D version of collision check.
        /// Walks the sampled RS path and ensures each short segment does NOT
        /// intersect any inflated obstacle (cyl or AABB on XZ).
        /// </summary>
        private static bool PathIsClearDetailed(List<Vector3> samples, List<Obstacle3D> obstacles, float buffer)
        {
            if (samples == null || samples.Count < 2)
                return true;

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

        // Distance from segment AB to circle center C in XZ <= r?
        private static bool SegmentIntersectsInflatedCylinderXZ(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float r)
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

        // Segment-vs-inflated axis-aligned box in XZ
        private static bool SegmentIntersectsInflatedAabbXZ(
            Vector3 a,
            Vector3 b,
            Vector3 center,
            Vector3 halfExtents,
            float buffer)
        {
            float minX = center.X - (Mathf.Max(0f, halfExtents.X) + buffer);
            float maxX = center.X + (Mathf.Max(0f, halfExtents.X) + buffer);
            float minZ = center.Z - (Mathf.Max(0f, halfExtents.Z) + buffer);
            float maxZ = center.Z + (Mathf.Max(0f, halfExtents.Z) + buffer);

            // Liang–Barsky style clipping in XZ plane
            float x0 = a.X, z0 = a.Z, x1 = b.X, z1 = b.Z;
            float dx = x1 - x0, dz = z1 - z0;
            float t0 = 0f, t1 = 1f;

            bool Clip(float p, float q, ref float tt0, ref float tt1)
            {
                if (Mathf.IsZeroApprox(p)) return q >= 0;
                float t = q / p;
                if (p < 0)
                {
                    if (t > tt1) return false;
                    if (t > tt0) tt0 = t;
                }
                else
                {
                    if (t < tt0) return false;
                    if (t < tt1) tt1 = t;
                }
                return true;
            }

            if (!Clip(-dx, x0 - minX, ref t0, ref t1)) return false;
            if (!Clip(dx, maxX - x0, ref t0, ref t1)) return false;
            if (!Clip(-dz, z0 - minZ, ref t0, ref t1)) return false;
            if (!Clip(dz, maxZ - z0, ref t0, ref t1)) return false;

            return t0 <= t1;
        }


        // ================================================================
        // Goal nudge helpers (DigSim3D-specific)
        // ================================================================
        private static bool IsInsideInflated(Vector3 x, List<Obstacle3D> obstacles, float buffer)
        {
            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    var dx = x.X - o.Center.X;
                    var dz = x.Z - o.Center.Z;
                    if ((dx * dx + dz * dz) < Math.Pow(o.Radius + buffer, 2))
                        return true;
                }
                else
                {
                    float hx = Math.Max(0f, o.Extents.X) + buffer;
                    float hz = Math.Max(0f, o.Extents.Z) + buffer;

                    if (x.X >= o.Center.X - hx && x.X <= o.Center.X + hx &&
                        x.Z >= o.Center.Z - hz && x.Z <= o.Center.Z + hz)
                        return true;
                }
            }
            return false;
        }

        private static Vector3 PushOutside(Vector3 goal, List<Obstacle3D> obstacles, float buffer)
        {
            // Push radially outward from the closest obstacle (matching your original DigSim3D behavior).
            float best = float.MaxValue;
            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    Vector3 d = goal - o.Center;
                    d.Y = 0;
                    float dist = d.Length();
                    float rInfl = o.Radius + buffer + 0.05f;

                    if (dist < rInfl && dist < best)
                    {
                        best = dist;
                        Vector3 n = d.LengthSquared() > 1e-6f ? d / dist : Vector3.Right;
                        goal = o.Center + n * rInfl;
                    }
                }
            }
            return goal;
        }


        // ================================================================
        // [Optional] Debug geometry like the 3D version
        // ================================================================
        // To match SimCore.Services.DrawDebugGridAndPath() you can uncomment this
        // and call it from Plan(). DigSim3D currently doesn't auto-clean these
        // MeshInstance3D nodes, so just be aware you'll spawn new debug meshes
        // each call if you don't also cull previous ones.
        //
        private static void DrawDebugGridAndPath(
            IReadOnlyList<Vector2> blockedCenters,
            List<Vector3> path3)
        {
            var tree  = Engine.GetMainLoop() as SceneTree;
            var scene = tree?.CurrentScene;
            if (scene == null)
                return;
        
            // TODO: Optionally remove previous debug nodes by name.
        
            const float debugHeight = 1.0f;
        
            // blocked cells (red quads)
            if (blockedCenters != null && blockedCenters.Count > 0)
            {
                var gridMeshInst = new MeshInstance3D { Name = "DebugGrid" };
                var gridIm = new ImmediateMesh();
                gridIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        
                // We don't have direct _gridSize here; you could capture it from
                // GridPlannerPersistent if you expose it.
                // For now assume 0.25f:
                float cell = 0.25f * 0.45f;
        
                foreach (var c in blockedCenters)
                {
                    var a = new Vector3(c.X - cell, debugHeight, c.Y - cell);
                    var b = new Vector3(c.X + cell, debugHeight, c.Y - cell);
                    var c2 = new Vector3(c.X + cell, debugHeight, c.Y + cell);
                    var d = new Vector3(c.X - cell, debugHeight, c.Y + cell);
        
                    gridIm.SurfaceAddVertex(a);
                    gridIm.SurfaceAddVertex(b);
                    gridIm.SurfaceAddVertex(c2);
                    gridIm.SurfaceAddVertex(a);
                    gridIm.SurfaceAddVertex(c2);
                    gridIm.SurfaceAddVertex(d);
                }
        
                gridIm.SurfaceEnd();
                gridMeshInst.Mesh = gridIm;
        
                var matGrid = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1, 0, 0, 0.35f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
                gridMeshInst.SetSurfaceOverrideMaterial(0, matGrid);
                scene.AddChild(gridMeshInst);
            }
        
            // path polyline (blue strip)
            if (path3 != null && path3.Count > 1)
            {
                var pathMeshInst = new MeshInstance3D { Name = "DebugPath" };
                var im = new ImmediateMesh();
                im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        
                foreach (var p in path3)
                    im.SurfaceAddVertex(new Vector3(p.X, debugHeight, p.Z));
        
                im.SurfaceEnd();
                pathMeshInst.Mesh = im;
        
                var matPath = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.0f, 0.5f, 1f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
                pathMeshInst.SetSurfaceOverrideMaterial(0, matPath);
                scene.AddChild(pathMeshInst);
            }
        }
    }
}