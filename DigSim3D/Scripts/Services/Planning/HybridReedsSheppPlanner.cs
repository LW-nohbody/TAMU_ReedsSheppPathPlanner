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
        
        // 2*NUM_SUBDIVISIONS = max number of segments to try subdividing into
        private const int NUM_SUBDIVISIONS = 5;

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
                GD.Print("[HybridReedsSheppPlanner] Using direct Reeds–Shepp path (clear).");
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
            ReedsSheppPath? merged = TryReplanWithMidpoints(start, goal, turnRadius, gridPath, obstacles, arenaRadius);

            if (merged != null)
            {
                GD.Print("[HybridReedsSheppPlanner] ✅ Successfully found clear RS route via midpoints.");
                return merged;
            }

            GD.Print("❌ Could not find clear RS route via midpoints. Returning fallback RS path.");
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

            // --- 2️⃣ Build initial RS path along simplified path --- };
            ReedsSheppPath mergedPath = new ReedsSheppPath();
            Vector3 prevPoint = new Vector3((float) start.X, 0f, (float) start.Y);
            double prevYaw = start.Theta;

            int index = 0;
            while (index < simplifiedPath.Count)
            {
                Vector3 segStart = prevPoint;
                int farthestReachable = index;

                // Try skipping as many A* points as possible
                for (int j = simplifiedPath.Count-1; j > index; j--)
                {
                    Vector3 segEnd = simplifiedPath[j];
                    double segYaw = Math.Atan2((segEnd - segStart).Z, (segEnd - segStart).X);

                    ReedsSheppPath rsTestPath = PathPlanner.GetOptimalPath(Pose.Create(segStart.X / turnRadius, segStart.Z / turnRadius, prevYaw), Pose.Create(segEnd.X / turnRadius, segEnd.Z / turnRadius, segYaw));

                    if (rsTestPath.Count > 0 && PathIsValid(rsTestPath.Sample(_sampleStep, turnRadius, Pose.Create(segStart.X, segStart.Z, prevYaw)), obstacles, arenaRadius))
                    {
                        
                        mergedPath.AddRange(rsTestPath);
                        farthestReachable = j;
                        prevPoint = simplifiedPath[j];
                        prevYaw = segYaw;
                        break;
                    }
                }
                
                if (farthestReachable == index && index == 0) // try replaning entire path with subdivision
                {
                    // Stuck try replan with subdivision
                    mergedPath = TryReplanWithSubdivision(start, goal, turnRadius, obstacles, arenaRadius, simplifiedPath);

                    if (mergedPath == null)
                    {
                        GD.Print("[HybridReedsSheppPlanner] Replanning with subdivision also failed [2].");
                        return null;
                    } else
                    {
                        break;
                    }
                } else if (farthestReachable == index) //replan this portion of path to end with subdivision
                {
                    // Stuck try replan with subdivision
                    ReedsSheppPath pathAddition = TryReplanWithSubdivision(Pose.Create(segStart.X, segStart.Z, prevYaw), goal, turnRadius, obstacles, arenaRadius, simplifiedPath.Skip(index-1).ToList());

                    if (pathAddition == null)
                    {
                        GD.Print("[HybridReedsSheppPlanner] Replanning with subdivision also failed [1].");
                        return null;
                    } else
                    {
                        mergedPath.AddRange(pathAddition);
                        break;
                    }
                }

                index = farthestReachable;
            }                

            // --- 3️⃣ Post-process: Try to simplify found RS Path in case something was missed may not be necessary ---
            PosePath mergedPathPoses = mergedPath.Sample(_sampleStep, turnRadius, start);
            ReedsSheppPath simplifiedRS = new ReedsSheppPath();
            Pose startPose = start;
            int cur = 0;
            while (cur < mergedPathPoses.Count - 1)
            {
                bool foundValidSegment = false;
                for (int j = mergedPathPoses.Count-1; j > cur; j--)
                {
                    Pose endPose = mergedPathPoses.ElementAt(j);
                    
                    ReedsSheppPath rsTest = PathPlanner.GetOptimalPath(
                        Pose.Create(startPose.X / turnRadius, startPose.Y / turnRadius, startPose.Theta),
                        Pose.Create(endPose.X / turnRadius, endPose.Y / turnRadius, endPose.Theta));

                    PosePath rsTestSampled = rsTest.Sample(_sampleStep, turnRadius, startPose);

                    if (rsTest.Count > 0 &&
                        PathIsValid(rsTestSampled, obstacles, arenaRadius))
                    {                        
                        simplifiedRS.AddRange(rsTest);
                        cur = j;
                        startPose = mergedPathPoses.ElementAt(j);
                        foundValidSegment = true;
                        break;
                    }
                }
                
                // could not find a valid simplified segment from index cur to any further point. Add the current point and move on.
                if (!foundValidSegment)
                {
                    return mergedPath;
                }
            }

            //GD.Print($"[HybridReedsSheppPlanner] RS replanning + simplification finished with {simplifiedRSPoints.Count} points.");
            // return (simplifiedRSPoints, simplifiedGears);
            return simplifiedRS;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ReedsSheppPath TryReplanWithSubdivision(Pose start,
                        Pose goal,
                        double turnRadius,
                        List<CylinderObstacle> obstacles,
                        float arenaRadius,
                        List<Vector3> simplifiedPath)
        {
            GD.Print("[HybridReedsSheppPlanner] Attempting replanning with subdivision...");

            ReedsSheppPath mergedPath = new ReedsSheppPath();
            Vector3 prevPoint = new Vector3((float) start.X, 0f, (float) start.Y);
            double prevYaw = start.Theta;
            int index = 0;
            while (index < simplifiedPath.Count)
            {
                Vector3 segStart = prevPoint;
                int farthestReachable = index;

                // Try skipping as many A* points as possible
                for (int j = index; j < simplifiedPath.Count; j++)
                {
                    Vector3 segEnd = simplifiedPath[j];
                    double segYaw = Math.Atan2((segEnd - segStart).Z, (segEnd - segStart).X);

                    // var (rsTest, rsGearsTest) = RSAdapter.ComputePath3D(segStart, prevYaw, segEnd, segYaw, turnRadius, _sampleStep);
                    ReedsSheppPath rsTestPath = PathPlanner.GetOptimalPath(Pose.Create(segStart.X / turnRadius, segStart.Z / turnRadius, prevYaw), Pose.Create(segEnd.X / turnRadius, segEnd.Z / turnRadius, segYaw));

                    if (rsTestPath.Count == 0 || !PathIsValid(rsTestPath.Sample(_sampleStep, turnRadius, Pose.Create(segStart.X, segStart.Z, prevYaw)), obstacles, arenaRadius))
                        break;

                    farthestReachable = j;
                }

                Vector3 target = simplifiedPath[farthestReachable];
                double targetYaw = Math.Atan2((target - segStart).Z, (target - segStart).X);
                if (farthestReachable == 0)
                {
                    target = simplifiedPath[index].Lerp(simplifiedPath[index+1], 0.5f);

                    // NEED TO HANDLE THIS CONDITION
                } 

                ReedsSheppPath rsSegment = PathPlanner.GetOptimalPath(
                    Pose.Create(segStart.X / turnRadius, segStart.Z / turnRadius, prevYaw),
                    Pose.Create(target.X / turnRadius, target.Z / turnRadius, targetYaw));

                if (rsSegment.Count == 0 || !PathIsValid(rsSegment.Sample(_sampleStep, turnRadius, Pose.Create(segStart.X, segStart.Z, prevYaw)), obstacles, arenaRadius))
                {
                    // subdivide paths
                    int subdiv = 1;

                    // Create start and end poses for CURRENT subdivision (end goal is to have path of subdivisions from segStart to target)
                    Vector3 subdivStartPoint = new Vector3(segStart.X, segStart.Y, segStart.Z);
                    double subdivStartYaw = prevYaw;
                    Pose subdivStartPose = Pose.Create(subdivStartPoint.X, subdivStartPoint.Z, subdivStartYaw);

                    Vector3 subdivEndPoint = subdivStartPoint.Lerp(target, 1 / NUM_SUBDIVISIONS);
                    double subdivEndYaw = Math.Atan2((subdivEndPoint - segStart).Z, (subdivEndPoint - segStart).X);
                    while (subdiv < NUM_SUBDIVISIONS)
                    {
                        // Initialize new rsSegment
                        ReedsSheppPath subdividedRS = new ReedsSheppPath();
                        for (int i = 0; i < subdiv; i++)
                        {
                            ReedsSheppPath rsTemp = PathPlanner.GetOptimalPath(
                            Pose.Create(subdivStartPoint.X / turnRadius, subdivStartPoint.Z / turnRadius, subdivStartYaw),
                            Pose.Create(subdivEndPoint.X / turnRadius, subdivEndPoint.Z / turnRadius, subdivEndYaw));
                            PosePath rsTempSampled = rsTemp.Sample(_sampleStep, turnRadius, subdivStartPose);

                            if (rsTemp.Count > 0 && PathIsValid(rsTempSampled, obstacles, arenaRadius))
                            {
                                subdividedRS.AddRange(rsTemp); 

                                if (i == subdiv - 1)
                                    break;

                                subdivStartPoint = subdivEndPoint;
                                subdivStartYaw = subdivEndYaw;
                                subdivStartPose = Pose.Create(subdivStartPoint.X, subdivStartPoint.Z, subdivStartYaw);

                                float t = (i + 2f) / NUM_SUBDIVISIONS;
                                subdivEndPoint = segStart.Lerp(target, t); 
                                subdivEndYaw = Math.Atan2((subdivEndPoint - segStart).Z, (subdivEndPoint - segStart).X);
                            } else
                            {
                                break;
                            }
                        }

                        subdiv++;
                    }
                    if (subdiv > 5)
                    {
                        GD.PrintErr("[HybridReedsSheppPlanner] Subdivision replanning failed: too many subdivisions.");
                        return null;
                    }

                } else
                {
                    mergedPath.AddRange(rsSegment);
                    prevYaw = targetYaw;
                    prevPoint = target;

                }

                index = farthestReachable;

            }
            
            if (mergedPath.Count == 0)
                return null;
            return mergedPath;
        }

        PosePath SampleSegment(ReedsSheppPath path, int startIdx, int endIdx,
                         double step, double turnRadius, Pose startPose)
        {
            var subpath = new ReedsSheppPath(path.Elements.Skip(startIdx).Take(endIdx - startIdx));
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
