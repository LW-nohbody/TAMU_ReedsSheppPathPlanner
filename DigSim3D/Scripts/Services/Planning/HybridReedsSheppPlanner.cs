using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using DigSim3D.App;
using DigSim3D.Domain;
using DigSim3D.App.Vehicles;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Algorithms.ReedsShepp;

namespace DigSim3D.Services
{
    /// <summary>
    /// Hybrid path planner that first tries a Reeds–Shepp path.
    /// If blocked, it computes an A* grid path and then stitches
    /// collision-free RS segments through key A* waypoints.
    /// </summary>
    public sealed class HybridReedsSheppPlanner : IHybridPlanner
    {
        private readonly double _sampleStep;
        private readonly float _gridSize;
        private readonly int _gridExtent;
        private readonly float _obstacleBuffer;
        private readonly int _maxAttempts;
        private readonly ReedsShepp PathPlanner = new ReedsShepp();

        public HybridReedsSheppPlanner(
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

        public IPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world)
        {
            double turnRadius = spec.TurnRadius 
                ?? throw new ArgumentException("HybridReedsSheppPlanner requires a non-null TurnRadius in VehicleSpec.");

            // var startPos = new Vector3((float)start.X, 0, (float)start.Y);
            // var goalPos = new Vector3((float)goal.X, 0, (float)goal.Y);

            var obstacles = world?.Obstacles?.OfType<CylinderObstacle>().ToList() ?? new List<CylinderObstacle>();
            
            // Get arena radius for wall boundary checking
            float arenaRadius = world?.Terrain?.Radius ?? float.PositiveInfinity;
            
            // Debug: Warn if terrain radius is not available
            if (arenaRadius == float.PositiveInfinity)
            {
                GD.PrintErr("⚠️ [HybridReedsSheppPlanner] WARNING: Arena radius not available! Wall checking disabled!");
            }
            else
            {
                GD.Print($"🔍 [HybridReedsSheppPlanner] Planning path with arena radius: {arenaRadius:F2}m (wall buffer: 0.5m)");
            }

            // 1️⃣ Direct Reeds–Shepp path
            ReedsSheppPath rsPath = PathPlanner.GetOptimalPath(Pose.Create(start.X / turnRadius, start.Y / turnRadius, start.Theta), Pose.Create(goal.X / turnRadius, goal.Y / turnRadius, goal.Theta));

            PosePath rsPoses = rsPath.Sample(_sampleStep, turnRadius, start);
            if (obstacles.Count == 0 || PathIsValid(rsPoses, obstacles, arenaRadius))
            {
                //GD.Print("[HybridReedsSheppPlanner] Using direct Reeds–Shepp path (clear).");
#if DEBUG
                //GD.Print("[HybridReedsSheppPlanner] (Debug) Forcing grid visualization even for clear RS path.");
                // DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, 
                //                    new List<Vector3> { startPos, goalPos }, 
                //                    _gridSize, _gridExtent);
#endif
                return rsPath;
            }

            //GD.Print("[HybridReedsSheppPlanner] Direct RS path blocked. Using cached A* grid.");

            var gridPath = GridPlannerPersistent.Plan2DPath(start, goal);

            // Always show grid visualization for debugging
            // DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, gridPath, _gridSize, _gridExtent);

            if (gridPath == null || gridPath.Count < 3)
            {
                GD.PrintErr("[HybridReedsSheppPlanner] A* grid path failed — returning fallback RS.");
                return rsPath;
            }

            // Attempt to replan
            ReedsSheppPath? merged = TryReplanWithMidpoints(start, goal, turnRadius, gridPath, obstacles, startGear: 1, arenaRadius);

            if (merged != null)
            {
                return merged;
            }

            GD.PrintErr("❌ Could not find clear RS route via midpoints. Returning fallback RS path.");
            //DrawDebugGridAndPath(GridPlannerPersistent.LastBlockedCenters, rsPts, _gridSize, _gridExtent);
            return rsPath;

        }

        // ================================================================
        // 🔧 Replanning logic — old midpoint-subdivision logic modernized
        // ================================================================

        private ReedsSheppPath TryReplanWithMidpoints(
            Pose start,
            Pose goal,
            double turnRadius,
            List<Vector3> gridPath,
            List<CylinderObstacle> obstacles,
            int startGear,
            float arenaRadius)
        {
            if (gridPath == null || gridPath.Count < 2)
                return null;

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
            // var mergedPoints = new List<Vector3> { new Vector3((float) start.X, 0f, (float) start.Y) };
            // var mergedGears = new List<int> { startGear };
            ReedsSheppPath mergedPath = new ReedsSheppPath();
            Vector3 lastPoint = new Vector3((float) start.X, 0f, (float) start.Y);
            double prevYaw = Math.Atan2((simplifiedPath[0] - lastPoint).Z, (simplifiedPath[0] - lastPoint).X);

            int index = 0;
            while (index < simplifiedPath.Count)
            {
                Vector3 segStart = lastPoint;
                int farthestReachable = index;

                // Try skipping as many A* points as possible
                for (int j = index; j < simplifiedPath.Count; j++)
                {
                    Vector3 segEnd = simplifiedPath[j];
                    double segYaw = Math.Atan2((segEnd - segStart).Z, (segEnd - segStart).X);

                    // var (rsTest, rsGearsTest) = RSAdapter.ComputePath3D(segStart, prevYaw, segEnd, segYaw, turnRadius, _sampleStep);
                    ReedsSheppPath rsTestPath = PathPlanner.GetOptimalPath(Pose.Create(start.X / turnRadius, start.Y / turnRadius, start.Theta), Pose.Create(segEnd.X / turnRadius, segEnd.Z / turnRadius, segYaw));
                    if (rsTestPath.Count == 0 || !PathIsValid(rsTestPath.Sample(_sampleStep, turnRadius, start), obstacles, arenaRadius))
                        break;

                    farthestReachable = j;
                }

                Vector3 target = simplifiedPath[farthestReachable];
                double targetYaw = Math.Atan2((target - segStart).Z, (target - segStart).X);

                // var (rsSegment, rsGears) = RSAdapter.ComputePath3D(segStart, prevYaw, target, targetYaw, turnRadius, _sampleStep);
                ReedsSheppPath rsSegment = PathPlanner.GetOptimalPath(
                    Pose.Create(segStart.X / turnRadius, segStart.Z / turnRadius, prevYaw),
                    Pose.Create(target.X / turnRadius, target.Z / turnRadius, targetYaw));

                int subdiv = 0;
                while ((rsSegment.Count == 0 || !PathIsValid(rsSegment.Sample(_sampleStep, turnRadius, start), obstacles, arenaRadius)) && subdiv < 5)
                {
                    subdiv++;
                    Vector3 mid = segStart.Lerp(target, 0.5f);
                    double midYaw = Math.Atan2((mid - segStart).Z, (mid - segStart).X);

                    // var (rs1, rsGears1) = RSAdapter.ComputePath3D(segStart, prevYaw, mid, midYaw, turnRadius, _sampleStep);
                    // var (rs2, rsGears2) = RSAdapter.ComputePath3D(mid, midYaw, target, targetYaw, turnRadius, _sampleStep);

                    ReedsSheppPath rs1 = PathPlanner.GetOptimalPath(
                        Pose.Create(segStart.X / turnRadius, segStart.Z / turnRadius, prevYaw),
                        Pose.Create(mid.X / turnRadius, mid.Z / turnRadius, midYaw));
                    ReedsSheppPath rs2 = PathPlanner.GetOptimalPath(
                        Pose.Create(mid.X / turnRadius, mid.Z / turnRadius, midYaw),
                        Pose.Create(target.X / turnRadius, target.Z / turnRadius, targetYaw));  

                    if (rs1.Count == 0 || rs2.Count == 0 ||
                        !PathIsValid(rs1.Sample(_sampleStep, turnRadius, Pose.Create(segStart.X, segStart.Z, prevYaw)), obstacles, arenaRadius) || !PathIsValid(rs2.Sample(_sampleStep, turnRadius, Pose.Create(mid.X, mid.Z, midYaw)), obstacles, arenaRadius))
                        break;

                    // Skip 1?
                    mergedPath.AddRange(rs1); 
                    mergedPath.AddRange(rs2);
                    // mergedGears.AddRange(rsGears1.Skip(1));
                    // mergedGears.AddRange(rsGears2.Skip(1));

                    prevYaw = targetYaw;
                    rsSegment = new ReedsSheppPath(); // Mark as successful
                }

                if (rsSegment.Count > 0)
                {
                    mergedPath.AddRange(rsSegment.Skip(1));
                    // mergedGears.AddRange(rsGears.Skip(1));
                    prevYaw = targetYaw;
                }

                index = farthestReachable + 1;
            }

            // --- 3️⃣ Post-process: simplify RS path by skipping intermediate points ---
            // ReedsSheppPath simplifiedRS = new ReedsSheppPath();
            // var simplifiedRSPoints = new List<Vector3> { mergedPoints[0] };
            // var simplifiedGears = new List<int> { mergedGears[0] };
            // int cur = 0;
            // while (cur < mergedPoints.Count - 1)
            // {
            //     int farthest = cur + 1;
            //     for (int j = mergedPoints.Count - 1; j > cur; j--)
            //     {
            //         double segYaw = Math.Atan2((mergedPoints[j] - mergedPoints[cur]).Z,
            //                                    (mergedPoints[j] - mergedPoints[cur]).X);
            //         var (rsTest, rsGearsTest) = RSAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
            //                                                             mergedPoints[j], segYaw,
            //                                                             turnRadius, _sampleStep);
            //         if (rsTest.Length > 0 && PathIsValid(rsTest.ToList(), obstacles, arenaRadius))
            //         {
            //             farthest = j;
            //             break;
            //         }
            //     }

            //     // Add RS segment to simplified list
            //     double yawToAdd = Math.Atan2((mergedPoints[farthest] - mergedPoints[cur]).Z,
            //                                  (mergedPoints[farthest] - mergedPoints[cur]).X);
            //     var (rsFinal, rsFinalGears) = RSAdapter.ComputePath3D(mergedPoints[cur], prevYaw,
            //                                                           mergedPoints[farthest], yawToAdd,
            //                                                           turnRadius, _sampleStep);
            //     if (rsFinal.Length > 0)
            //     {
            //         simplifiedRSPoints.AddRange(rsFinal.Skip(1));
            //         simplifiedGears.AddRange(rsFinalGears.Skip(1));
            //         prevYaw = yawToAdd;
            //     }

            //     cur = farthest;
            // }

            // if (simplifiedRSPoints.Count > 1)
            //     simplifiedGears[^1] = 1;

            ReedsSheppPath simplifiedRS = new ReedsSheppPath();
            Pose startPose = start;
            int cur = 0;
            while (cur < mergedPath.Count - 1)
            {
                for (int j = mergedPath.Count - 1; j > cur; j--)
                {
                    // Sample RS subpath as real poses
                    PosePath poses = SampleSegment(mergedPath, cur, j, _sampleStep, turnRadius, startPose);

                    Pose endPose   = poses.Last();

                    double yawStart = startPose.Theta;
                    double yawEnd   = endPose.Theta;

                    ReedsSheppPath rsTest = PathPlanner.GetOptimalPath(
                        Pose.Create(startPose.X / turnRadius, startPose.Y / turnRadius, yawStart),
                        Pose.Create(endPose.X / turnRadius, endPose.Y / turnRadius, yawEnd));

                    PosePath rsTestSampled = rsTest.Sample(_sampleStep, turnRadius, startPose);

                    if (rsTest.Count > 0 &&
                        PathIsValid(rsTestSampled, obstacles, arenaRadius))
                    {                        
                        simplifiedRS.AddRange(rsTest);
                        startPose = rsTestSampled.Last();
                        cur = j;
                        break;
                    }
                }
            }

            //GD.Print($"[HybridReedsSheppPlanner] RS replanning + simplification finished with {simplifiedRSPoints.Count} points.");
            // return (simplifiedRSPoints, simplifiedGears);
            return simplifiedRS;
        }




        // Recursive RS computation with midpoint subdivision
        // private (List<Vector3>?, List<int>?) ComputeRSWithSubdivision(
        //     Vector3 start,
        //     Vector3 end,
        //     double startYaw,
        //     double endYaw,
        //     double turnRadius,
        //     List<CylinderObstacle> obstacles,
        //     int depth,
        //     int maxDepth,
        //     float arenaRadius)
        // {
        //     if (depth > maxDepth)
        //         return (null, null);

        //     var (rsSegment, rsGears) = RSAdapter.ComputePath3D(start, startYaw, end, endYaw, turnRadius, _sampleStep);
        //     if (rsSegment.Length > 0 && PathIsValid(rsSegment.ToList(), obstacles, arenaRadius))
        //         return (rsSegment.ToList(), rsGears.ToList());

        //     // Subdivide at midpoint
        //     Vector3 mid = start.Lerp(end, 0.5f);
        //     double midYaw = Math.Atan2((mid - start).Z, (mid - start).X);

        //     var (firstHalf, gears1) = ComputeRSWithSubdivision(start, mid, startYaw, midYaw, turnRadius, obstacles, depth + 1, maxDepth, arenaRadius);
        //     var (secondHalf, gears2) = ComputeRSWithSubdivision(mid, end, midYaw, endYaw, turnRadius, obstacles, depth + 1, maxDepth, arenaRadius);

        //     if (firstHalf == null || secondHalf == null || gears1 == null || gears2 == null)
        //         return (null, null);

        //     // Merge, skip duplicate midpoint
        //     var merged = new List<Vector3>(firstHalf);
        //     merged.AddRange(secondHalf.Skip(1));
        //     var mergedGears = new List<int>(gears1);
        //     mergedGears.AddRange(gears2.Skip(1));
        //     return (merged, mergedGears);
        // }

        // ================================================================
        // Helpers
        // ================================================================
        // private PlannedPath BuildPath(List<Vector3> pts, List<int> gears)
        // {
        //     var path = new PlannedPath();
        //     path.Points.AddRange(pts);
        //     path.Gears.AddRange(gears);
        //     return path;
        // }

        PosePath SampleSegment(ReedsSheppPath path, int startIdx, int endIdx,
                         double step, double turnRadius, Pose startPose)
        {
            var subpath = new ReedsSheppPath(path.Elements.Skip(startIdx).Take(endIdx - startIdx + 1));
            return subpath.Sample(step, turnRadius, startPose);
        }


        private bool PathIsValid(PosePath pathPoses, List<CylinderObstacle> obstacles, float arenaRadius)
        {
            //GD.Print($"[HybridReedsSheppPlanner] Checking {pathPoints.Count} points against {obstacles.Count} obstacles");

            // Wall buffer must account for vehicle width and safety margin
            // Set to 0.1m for tighter wall avoidance
            const float WallBufferMeters = 0.1f; // Changed from previous value
            float maxAllowedRadius = arenaRadius - WallBufferMeters;
            
            int hitCount = 0;

            foreach (Pose p in pathPoses)
            {
                // Check arena boundary (wall buffer zone)
                double distFromCenter = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                if (distFromCenter > maxAllowedRadius)
                {
                    // PathIsValid error fills up all logs, so commented out
                    // GD.PrintErr($"❌ [PathPlanner] Path goes through wall buffer: point=({p.X:F2},{p.Z:F2}) " +
                    //             $"distFromCenter={distFromCenter:F2} > maxAllowed={maxAllowedRadius:F2} (arenaRadius={arenaRadius:F2}, buffer={WallBufferMeters:F2}m)");
                    return false;
                }
                
                // Check obstacle collisions
                foreach (var obs in obstacles)
                {
                    var dx = p.X - obs.GlobalPosition.X;
                    var dz = p.Y - obs.GlobalPosition.Z;
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
                hitCount++;
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
