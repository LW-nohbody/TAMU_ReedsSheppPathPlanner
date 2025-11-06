using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.App;
using DigSim3D.Domain;


namespace DigSim3D.Services
{
    /// <summary>
    /// Hybrid path planner that first tries a Reeds–Shepp path.
    /// If blocked, it computes an A* grid path and then stitches
    /// collision-free RS segments through key A* waypoints.
    /// </summary>
    public sealed class HybridDubinsPlanner : IPathPlanner
    {
        private readonly float _sampleStep;
        private readonly float _gridSize;
        private readonly int _gridExtent;
        private readonly float _obstacleBuffer;
        private readonly int _maxAttempts;

        public HybridDubinsPlanner(
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
            var (rsPoints, dGears) = DAdapter.ComputePath3D(
                startPos, start.Yaw,
                goalPos, goal.Yaw,
                turnRadiusMeters: spec.TurnRadius,
                fieldRadius: world.Terrain.Radius,
                sampleStepMeters: _sampleStep
            );

            GD.Print($"[HybridDubinsPlanner] Goal Pos: ({goalPos.X}, {goalPos.Z}) and orientation: {goal.Yaw}");

            var rsPts = rsPoints.ToList();
            if (obstacles.Count == 0 || PathIsValid(rsPts, obstacles, goal.Yaw, world.Terrain.Radius))
            {
                //GD.Print("[HybridReedsSheppPlanner] Using direct Reeds–Shepp path (clear).");
#if DEBUG
                //GD.Print("[HybridReedsSheppPlanner] (Debug) Forcing grid visualization even for clear RS path.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, 
                //                    new List<Vector3> { startPos, goalPos }, 
                //                    _gridSize, _gridExtent);
#endif
                GD.Print($"[HybridDubinsPlanner] Final Pos: ({rsPts.Last().X}, {rsPts.Last().Z})");
                return BuildPath(rsPts, dGears.ToList());
            }

            //GD.Print("[HybridReedsSheppPlanner] Direct RS path blocked. Using cached A* grid.");

            var gridPath = GridPlannerPersistent.Plan2DPath(startPos, goalPos);

            // Always show grid visualization for debugging
            DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, gridPath, _gridSize, _gridExtent);

            if (gridPath == null || gridPath.Count < 3)
            {
                GD.PrintErr("[HybridDubinsPlanner] A* grid path failed — returning fallback RS.");
                return BuildPath(rsPts, dGears.ToList());
            }

            // Attempt to replan
            var merged = TryReplanWithMidpoints(startPos, goalPos, spec.TurnRadius, gridPath, obstacles, startGear: 1, world,
    goal.Yaw, start.Yaw);

            if (merged.points != null)
            {
                //GD.Print($"✅ Hybrid replanning succeeded — {merged.points.Count} points total.");
                //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, merged, _gridSize, _gridExtent);
                return BuildPath(merged.points, merged.gears);
            }

            GD.PrintErr("❌ Could not find clear RS route via midpoints. Returning fallback RS path.");
            //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, rsPts, _gridSize, _gridExtent);
            return BuildPath(rsPts, dGears.ToList());

        }

        // ================================================================
        // 🔧 Replanning logic — old midpoint-subdivision logic modernized
        // ================================================================

        private (List<Vector3> points, List<int> gears) TryReplanWithMidpoints(
            Vector3 start,
            Vector3 goal,
            double turnRadius,
            List<Vector3> gridPath,
            List<CylinderObstacle> obstacles,
            int startGear,
            WorldState world,
            double goalYaw,
            double startYaw)
        {
            if (gridPath == null || gridPath.Count < 2)
                return (null, null);

            // --- 1️⃣ Simplify the A* path (remove nearly-collinear points) ---
            var simplifiedPath = new List<Vector3> { gridPath[0] };
            var temp = gridPath[0];
            simplifiedPath[0] = temp;
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
                {
                    simplifiedPath.Add(curr);
                }
            }
            temp = gridPath[^1];
            simplifiedPath.Add(temp);

            // --- 2️⃣ Build initial RS path along simplified path ---
            var mergedPoints = new List<Vector3> { start };
            var mergedGears = new List<int> { startGear };
            // double prevYaw = Math.Atan2((simplifiedPath[0] - start).Z, (simplifiedPath[0] - start).X);
            double prevYaw = startYaw;

            int index = 0;
            
            while (index < simplifiedPath.Count)
            {
                Vector3 segStart = mergedPoints.Last();
                int farthestReachable = index;
                bool AStarValid = false;
                bool nonShortestPath = false;
                Vector3[] nextBestPath = null;

                // Try skipping as many A* points as possible
                GD.Print($"[HyrbridDubinsPlanner] Current pos: ({segStart.X}, {segStart.Z})");
                for (int j = simplifiedPath.Count - 1; j >= index; j--)
                {
                    Vector3 segEnd = simplifiedPath[j];
                    double segYaw = 0.0;
                    if (j == simplifiedPath.Count - 1) { segYaw = goalYaw; }
                    else
                    {
                        segYaw = Math.Atan2((segEnd - segStart).Z, (segEnd - segStart).X);
                    }

                    var (dTest, dTestGears) = DAdapter.ComputePath3D(segStart, prevYaw, segEnd, segYaw, turnRadius, world.Terrain.Radius, _sampleStep);

                    if (dTest.Length == 0 || !PathIsValid(dTest.ToList(), obstacles, segYaw, world.Terrain.Radius))
                    {
                        //If shortest path cannot reach, check all other Dubins paths
                        
                        var (dTests, dTestsGears) = DAdapter.ComputeAllPath3D(segStart, prevYaw, segEnd, segYaw, turnRadius, world.Terrain.Radius, _sampleStep);
                        foreach (var test in dTests)
                        {
                            if(test.Length > 0 && PathIsValid(test.ToList(), obstacles, segYaw, world.Terrain.Radius))
                            {
                                farthestReachable = j;
                                AStarValid = true;
                                nonShortestPath = true;
                                nextBestPath = test;
                                break;
                            }
                        }
                    }
                    
                    else
                    {
                        farthestReachable = j;
                        AStarValid = true;
                        nonShortestPath = false;
                        break;
                    }
                }

                Vector3 target = simplifiedPath[farthestReachable];
                double targetYaw = 0.0;
                int nextIdx = Math.Min(farthestReachable + 1, simplifiedPath.Count - 1);
                if (farthestReachable == simplifiedPath.Count - 1)
                {
                    targetYaw = goalYaw;
                }
                else
                {
                    Vector3 nextDirVec = target - segStart;
                    targetYaw = Math.Atan2(nextDirVec.Z, nextDirVec.X);
                }
                // GD.Print($"[HybridDubinsPlanner] Target: {target.X}, {target.Z}");

                // Get Dubins path to the nearest reachable A* point, angled to follow the rest of A*
                // GD.Print("[HybridDubinsPlanner] Generating A* based Dubins path");
                var (dSegment, dGears) = DAdapter.ComputePath3D(segStart, prevYaw, target, targetYaw, turnRadius, world.Terrain.Radius, _sampleStep);
                if (nonShortestPath)
                {
                    dSegment = nextBestPath;
                    nonShortestPath = false;
                }
                

                //If path to next A* point isn't valid, split path into multiple Dubins paths up to 5 times
                int subdiv = 0;
                while ((!AStarValid) && subdiv < 5)
                {                    
                    GD.Print("[HybridDubinsPlanner] Running Lerp for loop");
                    subdiv++;
                    Vector3 mid = segStart.Lerp(target, 0.5f);
                    // Vector3 mid = target - segStart;
                    // mid.X = mid.X / MathF.Pow(2,subdiv);
                    // mid.Z = mid.Z / MathF.Pow(2, subdiv);
                    // mid = mid + segStart;
                    double midYaw = Math.Atan2((mid - segStart).Z, (mid - segStart).X); //TODO: Redo so the midYaw is angled to follow the rest of the path

                    var (d1, dGears1) = DAdapter.ComputePath3D(segStart, prevYaw, mid, midYaw, turnRadius, world.Terrain.Radius, _sampleStep);

                    if (d1.Length == 0 || !PathIsValid(d1.ToList(), obstacles, midYaw, world.Terrain.Radius))
                    {
                        GD.Print("[HybridDubinsPlanner] Valid Lerp Path not found");
                        continue;
                    }
                    mergedPoints.AddRange(d1.Skip(1));
                    mergedGears.AddRange(dGears1.Skip(1));
                    farthestReachable--;

                    // var (d2, dGears2) = DAdapter.ComputePath3D(mid, midYaw, target, targetYaw, turnRadius, world.Terrain.Radius, _sampleStep);

                    // if (d1.Length == 0 || d2.Length == 0 ||
                    //     !PathIsValid(d1.ToList(), obstacles, midYaw, world.Terrain.Radius) || !PathIsValid(d2.ToList(), obstacles, targetYaw, world.Terrain.Radius))
                    // {
                    //     GD.Print("[HybridDubinsPlanner] Valid Lerp Path not found");
                    //     break;
                    // }

                    // mergedPoints.AddRange(d1.Skip(1));
                    // mergedPoints.AddRange(d2.Skip(1));
                    // mergedGears.AddRange(dGears1.Skip(1));
                    // mergedGears.AddRange(dGears2.Skip(1));


                    prevYaw = targetYaw;
                    // dSegment = new Vector3[] { };
                    // GD.Print($"[HyrbridDubinsPLanner] dSegment.Length: {dSegment.Length}");
                    break;
                }

                if (AStarValid)
                {
                    if (!PathIsValid(dSegment.ToList(), obstacles, targetYaw, world.Terrain.Radius))
                    {
                        GD.Print("[HybridDubinsPlanner] A*-based path is invalid");
                    }
                    GD.Print($"[HybridDubinsPlanner] Adding path to closest A* point. Final Pos ({dSegment.Last().X}, {dSegment.Last().Z})");

                    mergedPoints.AddRange(dSegment.Skip(1));
                    mergedGears.AddRange(dGears.Skip(1));
                    // prevYaw = targetYaw;
                    if (dSegment.Length > 1)
                    {
                        var last = dSegment[^1];
                        var prevLast = dSegment[^2];
                        prevYaw = Math.Atan2((last - prevLast).Z, (last - prevLast).X);
                    }
                }

                index = farthestReachable + 1;
            }
            return (mergedPoints, mergedGears);

            // --- 3️⃣ Post-process: simplify RS path by skipping intermediate points ---
            // var simplifiedRSPoints = new List<Vector3> { mergedPoints[0] };
            // var simplifiedGears = new List<int> { mergedGears[0] };
            // int cur = 0;
            // while (cur < mergedPoints.Count - 1)
            // {
            //     int farthest = cur + 1;
            //     for (int j = mergedPoints.Count - 1; j > cur; j--)
            //     {
            //         double segYaw = Math.Atan2((mergedPoints[j] - mergedPoints[cur]).Z,
            //                                 (mergedPoints[j] - mergedPoints[cur]).X);
            //         var (dTest, dGearsTest) = DAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
            //                                                             mergedPoints[j], segYaw,
            //                                                             turnRadius, world.Terrain.Radius, _sampleStep);
            //         if (dTest.Length > 0 && PathIsValid(dTest.ToList(), obstacles))
            //         {
            //             farthest = j;
            //             break;
            //         }
            //     }

            //     // Add D segment to simplified list
            //     double yawToAdd = Math.Atan2((mergedPoints[farthest] - mergedPoints[cur]).Z,
            //                                 (mergedPoints[farthest] - mergedPoints[cur]).X);
            //     var (dFinal, dFinalGears) = DAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
            //                                                         mergedPoints[farthest], yawToAdd,
            //                                                         turnRadius, world.Terrain.Radius, _sampleStep);
            //     if (dFinal.Length > 0)
            //     {
            //         simplifiedRSPoints.AddRange(dFinal.Skip(1));
            //         simplifiedGears.AddRange(dFinalGears.Skip(1));
            //         prevYaw = yawToAdd;
            //     }

            //     cur = farthest;
            // }

            // if (simplifiedRSPoints.Count > 1)
            //     simplifiedGears[^1] = 1;

            // //GD.Print($"[HybridReedsSheppPlanner] RS replanning + simplification finished with {simplifiedRSPoints.Count} points.");
            // return (simplifiedRSPoints, simplifiedGears);
        }


        private Godot.Vector3 DubinsToGodot(Vector3 p){ return new Vector3(p.X, p.Y, -p.Z);  }

        // Recursive RS computation with midpoint subdivision
        private (List<Vector3>, List<int>) ComputeRSWithSubdivision(
            Vector3 start,
            Vector3 end,
            double startYaw,
            double endYaw,
            double turnRadius,
            List<CylinderObstacle> obstacles,
            int depth,
            WorldState world,
            int maxDepth)
        {
            if (depth > maxDepth)
                return (null, null);

            var (dSegment, dGears) = DAdapter.ComputePath3D(start, startYaw, end, endYaw, turnRadius, world.Terrain.Radius, _sampleStep);
            if (dSegment.Length > 0 && PathIsValid(dSegment.ToList(), obstacles, endYaw, world.Terrain.Radius))
                return (dSegment.ToList(), dGears.ToList());

            // Subdivide at midpoint
            Vector3 mid = start.Lerp(end, 0.5f);
            double midYaw = Math.Atan2((mid - start).Z, (mid - start).X);

            var (firstHalf, gears1) = ComputeRSWithSubdivision(start, mid, startYaw, midYaw, turnRadius, obstacles, depth + 1, world, maxDepth);
            var (secondHalf, gears2) = ComputeRSWithSubdivision(mid, end, midYaw, endYaw, turnRadius, obstacles, depth + 1, world, maxDepth);

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

        private bool PathIsValid(List<Vector3> pathPoints, List<CylinderObstacle> obstacles, double endYaw, double radius)
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
                    double angleToObstacle = Math.Atan2(obs.GlobalPosition.Z - p.Z, obs.GlobalPosition.X - p.X);
                    double angleDist = Math.Abs(angleToObstacle - endYaw);
                    double turnDist = obs.Radius + radius;
                    double turnDistSq = turnDist * turnDist;

                    // Optional: Print every ~10th point to not flood logs
                    // if ((hitCount % 10 == 0) && distSq < minDistSq * 4)
                    // {
                    //     GD.Print($"   sample ({p.X:F2},{p.Z:F2}) → obs ({obs.GlobalPosition.X:F2},{obs.GlobalPosition.Z:F2}), " +
                    //             $"dist={Math.Sqrt(distSq):F2}, min={minDist:F2}");
                    // }

                    if (distSq < minDistSq || (angleDist < 0.48 && distSq < turnDistSq))
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
