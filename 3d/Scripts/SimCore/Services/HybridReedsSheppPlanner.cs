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
    public sealed class HybridReedsSheppPlanner : IPathPlanner
    {
        private readonly float _sampleStep;
        private readonly float _gridSize;
        private readonly int _gridExtent;
        private readonly float _obstacleBuffer;
        private readonly int _maxAttempts;

        public HybridReedsSheppPlanner(
            float sampleStepMeters = 0.25f,
            float gridSize = 0.25f,
            int gridExtent = 60,
            float obstacleBufferMeters = 0.5f,
            int maxAttempts = 200)
        {
            _sampleStep = sampleStepMeters;
            _gridSize = gridSize;
            _gridExtent = gridExtent;
            _obstacleBuffer = obstacleBufferMeters;
            _maxAttempts = maxAttempts;
        }

        public PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world)
        {
            GD.Print($"[HybridReedsSheppPlanner] DEBUG: Running Plan() — obstacles={world?.Obstacles?.Count() ?? 0}");

            var startPos = new Vector3((float)start.X, 0, (float)start.Z);
            var goalPos = new Vector3((float)goal.X, 0, (float)goal.Z);

            var obstacles = world?.Obstacles?.OfType<CylinderObstacle>().ToList() ?? new List<CylinderObstacle>();

            // 1️⃣ Direct Reeds–Shepp path
            var (rsPoints, rsGears) = RSAdapter.ComputePath3D(
                startPos, start.Yaw,
                goalPos, goal.Yaw,
                turnRadiusMeters: spec.TurnRadius,
                sampleStepMeters: _sampleStep
            );

            var rsPts = rsPoints.ToList();
            if (obstacles.Count == 0 || PathIsValid(rsPts, obstacles))
            {
                GD.Print("[HybridReedsSheppPlanner] Using direct Reeds–Shepp path (clear).");
                #if DEBUG
                //GD.Print("[HybridReedsSheppPlanner] (Debug) Forcing grid visualization even for clear RS path.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, 
                //                    new List<Vector3> { startPos, goalPos }, 
                //                    _gridSize, _gridExtent);
                #endif
                return BuildPath(rsPts, rsGears.ToList());
            }

            GD.Print("[HybridReedsSheppPlanner] Direct RS path blocked. Using cached A* grid.");

            var gridPath = GridPlannerPersistent.Plan2DPath(startPos, goalPos);

            // Always show grid visualization for debugging
            //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, gridPath, _gridSize, _gridExtent);

            if (gridPath == null || gridPath.Count < 3)
            {
                GD.PrintErr("[HybridReedsSheppPlanner] A* grid path failed — returning fallback RS.");
                return BuildPath(rsPts, rsGears.ToList());
            }

            // Attempt to replan
            var merged = TryReplanWithMidpoints(startPos, goalPos, spec.TurnRadius, gridPath, obstacles, goal.Yaw);

            if (merged != null)
            {
                GD.Print($"✅ Hybrid replanning succeeded — {merged.Count} points total.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, merged, _gridSize, _gridExtent);
                return BuildPath(merged, Enumerable.Repeat(1, merged.Count).ToList());
            }

            GD.PrintErr("❌ Could not find clear RS route via midpoints. Returning fallback RS path.");
            //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, rsPts, _gridSize, _gridExtent);
            return BuildPath(rsPts, rsGears.ToList());

        }

        // ================================================================
        // 🔧 Replanning logic — old midpoint-subdivision logic modernized
        // ================================================================
        private List<Vector3> TryReplanWithMidpoints(
            Vector3 start,
            Vector3 goal,
            double turnRadius,
            List<Vector3> gridPath,
            List<CylinderObstacle> obstacles,
            double preservedFinalYaw) // <- pass in the original car yaw
        {
            int attempts = 0;
            int n = gridPath.Count;
            int center = n / 2;
            var tried = new HashSet<int>();

            for (int offset = 0; offset <= n && attempts < _maxAttempts; offset++)
            {
                int[] candidates = { center - offset, center + offset };
                foreach (var idx in candidates)
                {
                    if (idx <= 0 || idx >= n - 1) continue;
                    if (!tried.Add(idx)) continue;

                    var mid = gridPath[idx];

                    // --- Tangents like VehicleAgent3D ---
                    Vector3 startTangent = (gridPath[Math.Min(1, n - 1)] - gridPath[0]).WithY(0).Normalized();
                    Vector3 midTangentPrev = (gridPath[idx] - gridPath[idx - 1]).WithY(0).Normalized();
                    Vector3 midTangentNext = (gridPath[Math.Min(idx + 1, n - 1)] - gridPath[idx]).WithY(0).Normalized();
                    Vector3 goalTangent = (gridPath[^1] - gridPath[^2]).WithY(0).Normalized();

                    // Smooth midpoint tangent
                    Vector3 midTangent = ((midTangentPrev + midTangentNext) * 0.5f).Normalized();

                    double startYaw = Math.Atan2(startTangent.Z, startTangent.X);
                    double midYaw = Math.Atan2(midTangent.Z, midTangent.X);

                    // Preserve final orientation from before replanning
                    double goalYaw = preservedFinalYaw;

                    attempts += 2;

                    var (rs1, _) = RSAdapter.ComputePath3D(
                        start, startYaw, mid, midYaw,
                        turnRadiusMeters: turnRadius,
                        sampleStepMeters: _sampleStep
                    );
                    var (rs2, _) = RSAdapter.ComputePath3D(
                        mid, midYaw, goal, goalYaw,
                        turnRadiusMeters: turnRadius,
                        sampleStepMeters: _sampleStep
                    );

                    if (rs1.Length == 0 || rs2.Length == 0)
                        continue;

                    bool coll1 = !PathIsValid(rs1.ToList(), obstacles);
                    bool coll2 = !PathIsValid(rs2.ToList(), obstacles);

                    GD.Print($"[Hybrid] try idx={idx}, attempts={attempts}, coll1={coll1}, coll2={coll2}");

                    if (!coll1 && !coll2)
                    {
                        // --- Merge while preserving tangent direction ---
                        var merged = new List<Vector3>(rs1);
                        if (rs2.Length > 0)
                        {
                            Vector3 dir1 = (rs1[^1] - rs1[Math.Max(0, rs1.Length - 2)]).WithY(0).Normalized();
                            Vector3 dir2 = (rs2[Math.Min(1, rs2.Length - 1)] - rs2[0]).WithY(0).Normalized();

                            // If angle between rs1 and rs2 tangents > small threshold, align rs2
                            float dot = dir1.Dot(dir2);
                            if (dot < 0.99f)
                            {
                                GD.Print($"[Hybrid] Adjusting rs2 to align yaw continuity (dot={dot:F3})");
                                // Simple fix: reverse rs2 if it points opposite
                                if (dot < 0.0f)
                                    rs2.Reverse();
                            }

                            if (merged.Last().DistanceTo(rs2.First()) < 1e-3f)
                                merged.AddRange(rs2.Skip(1));
                            else
                                merged.AddRange(rs2);
                        }

                        GD.Print($"✅ Replanning succeeded — merged path {merged.Count} pts, mid idx={idx}");
                        return merged;
                    }
                }
            }

            GD.PrintErr("❌ Midpoint replanning failed after all attempts.");
            return null;
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
    GD.Print($"[HybridReedsSheppPlanner] Checking {pathPoints.Count} points against {obstacles.Count} obstacles");

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
                GD.Print($"   sample ({p.X:F2},{p.Z:F2}) → obs ({obs.GlobalPosition.X:F2},{obs.GlobalPosition.Z:F2}), " +
                         $"dist={Math.Sqrt(distSq):F2}, min={minDist:F2}");
            }

            if (distSq < minDistSq)
            {
                GD.PrintErr($"❌ RS path collision: sample=({p.X:F2},{p.Z:F2}) obs=({obs.GlobalPosition.X:F2},{obs.GlobalPosition.Z:F2}) " +
                            $"dist={Math.Sqrt(distSq):F2} < min={minDist:F2}");
                return false;
            }
        }
    }

    GD.Print("[HybridReedsSheppPlanner] PathIsValid → CLEAR");
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
