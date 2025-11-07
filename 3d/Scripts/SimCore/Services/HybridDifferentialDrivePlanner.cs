using Godot;
using SimCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimCore.Services
{
    /// <summary>
    /// Hybrid path planner that first tries a Reeds–Shepp path.
    /// If blocked, it computes an A* grid path and then stitches
    /// collision-free RS segments through key A* waypoints.
    /// </summary>
    public sealed class HybridDifferentialDrivePlanner : IPathPlanner
    {
        private readonly float _sampleStep;
        private readonly float _gridSize;
        private readonly int _gridExtent;
        private readonly float _obstacleBuffer;
        private readonly int _maxAttempts;

        public HybridDifferentialDrivePlanner(
            float sampleStepMeters = 0.25f,
            float gridSize = 0.25f,
            int gridExtent = 60,
            float obstacleBufferMeters = 0.5f,
            int maxAttempts = 2000)
        {
            _sampleStep = sampleStepMeters;
            _gridSize = gridSize;
            _gridExtent = gridExtent;
            _obstacleBuffer = obstacleBufferMeters;
            _maxAttempts = maxAttempts;
        }

        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world)
        {
            //GD.Print($"[HybridReedsSheppPlanner] DEBUG: Running Plan() — obstacles={world?.Obstacles?.Count() ?? 0}");

            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos = new Vector3((float)goal.X, 0, (float)goal.Z);

            var obstacles = world?.Obstacles?.OfType<CylinderObstacle>().ToList() ?? new List<CylinderObstacle>();

            // 1️⃣ Direct Reeds–Shepp path
            var (ddPoints, ddGears) = DDAdapter.ComputePath3D(
                startPos, start.Yaw,
                goalPos, goal.Yaw,
                sampleStepMeters: _sampleStep
            );

            var ddPts = ddPoints.ToList();
            if (obstacles.Count == 0 || PathIsValid(ddPts, obstacles))
            {
                //GD.Print("[HybridReedsSheppPlanner] Using direct Reeds–Shepp path (clear).");
                #if DEBUG
                //GD.Print("[HybridReedsSheppPlanner] (Debug) Forcing grid visualization even for clear RS path.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, 
                //                    new List<Vector3> { startPos, goalPos }, 
                //                    _gridSize, _gridExtent);
                #endif
                return BuildPath(ddPts, ddGears.ToList());
            }

            //GD.Print("[HybridReedsSheppPlanner] Direct RS path blocked. Using cached A* grid.");

            var gridPath = GridPlannerPersistent.Plan2DPath(startPos, goalPos);

            // Always show grid visualization for debugging
            DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, gridPath, _gridSize, _gridExtent);

            if (gridPath == null || gridPath.Count < 3)
            {
                GD.PrintErr("[HybridReedsSheppPlanner] A* grid path failed — returning fallback RS.");
                return BuildPath(ddPts, ddGears.ToList());
            }

            // Attempt to replan
            var merged = TryReplanWithMidpoints(startPos, goalPos, gridPath, obstacles, startGear: 1, 
    goal.Yaw);

            if (merged.points != null)
            {
                //GD.Print($"✅ Hybrid replanning succeeded — {merged.points.Count} points total.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, merged, _gridSize, _gridExtent);
                return BuildPath(merged.points, merged.gears);
            }

            GD.PrintErr("❌ Could not find clear RS route via midpoints. Returning fallback RS path.");
            //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, rsPts, _gridSize, _gridExtent);
            return BuildPath(ddPts, ddGears.ToList());

        }

        // ================================================================
        // 🔧 Replanning logic — old midpoint-subdivision logic modernized
        // ================================================================

private (List<Vector3> points, List<int> gears) TryReplanWithMidpoints(
    Vector3 start,
    Vector3 goal,
    List<Vector3> gridPath,
    List<CylinderObstacle> obstacles,
    int startGear,
    double goalYaw)
{
    if (gridPath == null || gridPath.Count < 2)
        return (null, null);

    // --- 1️⃣ Simplify the A* path (remove nearly-collinear points) ---
    var simplifiedPath = new List<Vector3> { gridPath[0] };
    const float angleThreshold = 5f * (float)(Math.PI / 180.0); 
    for (int i = 1; i < gridPath.Count - 1; i++)
    {
        Vector3 prev = simplifiedPath.Last();
        Vector3 curr = gridPath[i];
        Vector3 next = gridPath[i + 1];
        Vector3 v1 = (curr - prev).WithY(0).Normalized();
        Vector3 v2 = (next - curr).WithY(0).Normalized();
        float angle = (float)Math.Acos(Math.Clamp(v1.Dot(v2), -1f, 1f));
        if (Math.Abs(angle) > angleThreshold)
            simplifiedPath.Add(curr);
    }
    simplifiedPath.Add(gridPath[^1]);

    // --- 2️⃣ Build initial RS path along simplified path ---
    var mergedPoints = new List<Vector3> { start };
    var mergedGears = new List<int> { startGear };
    double prevYaw = Math.Atan2((simplifiedPath[0] - start).Z, (simplifiedPath[0] - start).X);

    int index = 0;
    while (index < simplifiedPath.Count)
    {
        Vector3 segStart = mergedPoints.Last();
        int farthestReachable = index;

        // Try skipping as many A* points as possible
        for (int j = index; j < simplifiedPath.Count; j++)
        {
            Vector3 segEnd = simplifiedPath[j];
            double segYaw = Math.Atan2((segEnd - segStart).Z, (segEnd - segStart).X);

            var (rsTest, rsGearsTest) = DDAdapter.ComputePath3D(segStart, prevYaw, segEnd, segYaw, _sampleStep);
            if (rsTest.Length == 0 || !PathIsValid(rsTest.ToList(), obstacles))
                break;

            farthestReachable = j;
        }

        Vector3 target = simplifiedPath[farthestReachable];
        double targetYaw = Math.Atan2((target - segStart).Z, (target - segStart).X);

        var (rsSegment, rsGears) = DDAdapter.ComputePath3D(segStart, prevYaw, target, targetYaw, _sampleStep);

        int subdiv = 0;
        while ((rsSegment.Length == 0 || !PathIsValid(rsSegment.ToList(), obstacles)) && subdiv < 5)
        {
            subdiv++;
            Vector3 mid = segStart.Lerp(target, 0.5f);
            double midYaw = Math.Atan2((mid - segStart).Z, (mid - segStart).X);

            var (rs1, rsGears1) = DDAdapter.ComputePath3D(segStart, prevYaw, mid, midYaw, _sampleStep);
            var (rs2, rsGears2) = DDAdapter.ComputePath3D(mid, midYaw, target, targetYaw, _sampleStep);

            if (rs1.Length == 0 || rs2.Length == 0 ||
                !PathIsValid(rs1.ToList(), obstacles) || !PathIsValid(rs2.ToList(), obstacles))
                break;

            mergedPoints.AddRange(rs1.Skip(1));
            mergedPoints.AddRange(rs2.Skip(1));
            mergedGears.AddRange(rsGears1.Skip(1));
            mergedGears.AddRange(rsGears2.Skip(1));

            prevYaw = targetYaw;
            rsSegment = new Vector3[] { };
        }

        if (rsSegment.Length > 0)
        {
            mergedPoints.AddRange(rsSegment.Skip(1));
            mergedGears.AddRange(rsGears.Skip(1));
            prevYaw = targetYaw;
        }

        index = farthestReachable + 1;
    }

    // --- 3️⃣ Post-process: simplify RS path by skipping intermediate points ---
    var simplifiedRSPoints = new List<Vector3> { mergedPoints[0] };
    var simplifiedGears = new List<int> { mergedGears[0] };
    int cur = 0;
    while (cur < mergedPoints.Count - 1)
    {
        int farthest = cur + 1;
        for (int j = mergedPoints.Count - 1; j > cur; j--)
        {
            double segYaw = Math.Atan2((mergedPoints[j] - mergedPoints[cur]).Z,
                                       (mergedPoints[j] - mergedPoints[cur]).X);
            var (rsTest, rsGearsTest) = DDAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
                                                                mergedPoints[j], segYaw, _sampleStep);
            if (rsTest.Length > 0 && PathIsValid(rsTest.ToList(), obstacles))
            {
                farthest = j;
                break;
            }
        }

        // Add RS segment to simplified list
        double yawToAdd = Math.Atan2((mergedPoints[farthest] - mergedPoints[cur]).Z,
                                     (mergedPoints[farthest] - mergedPoints[cur]).X);
        var (rsFinal, rsFinalGears) = DDAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
                                                              mergedPoints[farthest], yawToAdd, _sampleStep);
        if (rsFinal.Length > 0)
        {
            simplifiedRSPoints.AddRange(rsFinal.Skip(1));
            simplifiedGears.AddRange(rsFinalGears.Skip(1));
            prevYaw = yawToAdd;
        }

        cur = farthest;
    }

    if (simplifiedRSPoints.Count > 1)
        simplifiedGears[^1] = 1;

    //GD.Print($"[HybridReedsSheppPlanner] RS replanning + simplification finished with {simplifiedRSPoints.Count} points.");
    return (simplifiedRSPoints, simplifiedGears);
}




// Recursive RS computation with midpoint subdivision
private (List<Vector3>, List<int>) ComputeRSWithSubdivision(
    Vector3 start,
    Vector3 end,
    double startYaw,
    double endYaw,
    List<CylinderObstacle> obstacles,
    int depth,
    int maxDepth)
{
    if (depth > maxDepth)
        return (null, null);

    var (rsSegment, rsGears) = DDAdapter.ComputePath3D(start, startYaw, end, endYaw, _sampleStep);
    if (rsSegment.Length > 0 && PathIsValid(rsSegment.ToList(), obstacles))
        return (rsSegment.ToList(), rsGears.ToList());

    // Subdivide at midpoint
    Vector3 mid = start.Lerp(end, 0.5f);
    double midYaw = Math.Atan2((mid - start).Z, (mid - start).X);

    var (firstHalf, gears1) = ComputeRSWithSubdivision(start, mid, startYaw, midYaw, obstacles, depth + 1, maxDepth);
    var (secondHalf, gears2) = ComputeRSWithSubdivision(mid, end, midYaw, endYaw, obstacles, depth + 1, maxDepth);

    if (firstHalf == null || secondHalf == null)
        return (null, null);

    // Merge, skip duplicate midpoint
    var merged = new List<Vector3>(firstHalf);
    merged.AddRange(secondHalf.Skip(1));
    var mergedGears = new List<int>(gears1);
    mergedGears.AddRange(gears2.Skip(1));
    return (merged, mergedGears);
}













        // ================================================================
        // ✅ Helpers
        // ================================================================
        private PlannedPath BuildPath(List<Vector3> pts, List<int> gears)
        {
            var path = new PlannedPath();
            path.Points.AddRange(pts);
            path.Gears.AddRange(gears);
            return path;
        }

        private bool PathIsValid(List<Vector3> pathPoints, List<CylinderObstacle> obstacles)
{
    //GD.Print($"[HybridReedsSheppPlanner] Checking {pathPoints.Count} points against {obstacles.Count} obstacles");

    int hitCount = 0;

    foreach (var p in pathPoints)
    {
        foreach (var obs in obstacles)
        {
            var dx = p.X - obs.GlobalPosition.X;
            var dz = p.Z - obs.GlobalPosition.Z;
            var distSq = dx * dx + dz * dz;
            var minDist = obs.Radius + _obstacleBuffer;
            var minDistSq = minDist * minDist;

            // Optional: Print every ~10th point to not flood logs
            if ((hitCount % 10 == 0) && distSq < minDistSq * 4)
            {
                //GD.Print($"   sample ({p.X:F2},{p.Z:F2}) → obs ({obs.GlobalPosition.X:F2},{obs.GlobalPosition.Z:F2}), " +
                //         $"dist={Math.Sqrt(distSq):F2}, min={minDist:F2}");
            }

            if (distSq < minDistSq)
            {
                //GD.PrintErr($"❌ RS path collision: sample=({p.X:F2},{p.Z:F2}) obs=({obs.GlobalPosition.X:F2},{obs.GlobalPosition.Z:F2}) " +
                //            $"dist={Math.Sqrt(distSq):F2} < min={minDist:F2}");
                return false;
            }
        }
    }

    //GD.Print("[HybridReedsSheppPlanner] PathIsValid → CLEAR");
    return true;
}





        #if DEBUG
        private void DrawDebugGridAndPath(IReadOnlyList<Vector2> blockedCenters, List<Vector3> path3, float gridSize, int gridExtent)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var scene = tree?.CurrentScene;
            if (scene == null)
                return;

            // Remove any previous debug geometry
            foreach (var child in scene.GetChildren())
            {
                if (child is Node n)
                {
                    var nm = n.Name.ToString();
                    if (nm.StartsWith("DebugGrid", StringComparison.Ordinal) || nm.StartsWith("DebugPath", StringComparison.Ordinal))
                        n.QueueFree();
                }
            }

            const float debugHeight = 1.0f; // 🔹 raise everything 1 meter above ground

            // --- Draw blocked grid cells (semi-transparent red) ---
            if (blockedCenters != null && blockedCenters.Count > 0)
            {
                var gridMeshInst = new MeshInstance3D { Name = "DebugGrid" };
                var gridIm = new ImmediateMesh();
                gridIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);

                float half = gridSize * 0.45f;

                foreach (var c in blockedCenters)
                {
                    var a = new Vector3(c.X - half, debugHeight, c.Y - half);
                    var b = new Vector3(c.X + half, debugHeight, c.Y - half);
                    var c2 = new Vector3(c.X + half, debugHeight, c.Y + half);
                    var d = new Vector3(c.X - half, debugHeight, c.Y + half);

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

            // --- Draw A* path (blue line) ---
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
        #endif


    }
}
