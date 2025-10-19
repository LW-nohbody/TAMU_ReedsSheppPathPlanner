using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RSCore
{
    public static class GridPlanner
    {
        public static List<Vector3> Plan2DPath(
            Vector3 start,
            Vector3 goal,
            List<CylinderObstacle> obstacles,
            float gridSize = 0.5f,
            int gridExtent = 40,
            float obstacleBufferMeters = 0.0f)
        {
            var astar = new AStar2D();
            int diameter = gridExtent * 2 + 1;
            astar.ReserveSpace(diameter * diameter);

            // 1) Create grid points
            for (int gx = -gridExtent; gx <= gridExtent; gx++)
            {
                for (int gz = -gridExtent; gz <= gridExtent; gz++)
                {
                    long id = ToId(gx, gz, gridExtent);
                    Vector2 pos2 = new Vector2(gx * gridSize, gz * gridSize);
                    astar.AddPoint(id, pos2);
                }
            }

            // 2) Connect neighbors (8-way)
            for (int gx = -gridExtent; gx <= gridExtent; gx++)
            {
                for (int gz = -gridExtent; gz <= gridExtent; gz++)
                {
                    long id = ToId(gx, gz, gridExtent);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = gx + dx, nz = gz + dz;
                            if (Math.Abs(nx) <= gridExtent && Math.Abs(nz) <= gridExtent)
                            {
                                long nid = ToId(nx, nz, gridExtent);
                                astar.ConnectPoints(id, nid, false);
                            }
                        }
                    }
                }
            }

            // 3) Disable grid cells that collide with obstacles
            var blockedCenters = new List<Vector2>();

            if (obstacles != null && obstacles.Count > 0)
            {
                float cellHalfDiag = (float)(Math.Sqrt(2.0) * gridSize * 0.5);
                int blockedCount = 0;

                for (int gx = -gridExtent; gx <= gridExtent; gx++)
                {
                    for (int gz = -gridExtent; gz <= gridExtent; gz++)
                    {
                        long id = ToId(gx, gz, gridExtent);
                        Vector2 cellCenter = astar.GetPointPosition(id);

                        bool disabled = false;
                        foreach (var obs in obstacles)
                        {
                            Vector2 obs2 = new Vector2(obs.GlobalPosition.X, obs.GlobalPosition.Z);
                            float effectiveRadius = obs.Radius + obstacleBufferMeters + cellHalfDiag;

                            if (cellCenter.DistanceTo(obs2) <= effectiveRadius)
                            {
                                disabled = true;
                                break;
                            }
                        }

                        if (disabled)
                        {
                            astar.SetPointDisabled(id, true);
                            blockedCount++;
                            blockedCenters.Add(cellCenter);
                        }
                    }
                }

                GD.Print($"GridPlanner: disabled {blockedCount} cells for {obstacles.Count} obstacles (gridSize={gridSize}, gridExtent={gridExtent}).");
            }

            // 4) Find closest valid nodes for start and goal
            var start2 = new Vector2(start.X, start.Z);
            var goal2 = new Vector2(goal.X, goal.Z);

            long startId = astar.GetClosestPoint(start2);
            long goalId = astar.GetClosestPoint(goal2);

            if (startId == -1 || goalId == -1)
                return new List<Vector3>();

            var pts2 = astar.GetPointPath(startId, goalId);

            var path3 = new List<Vector3>();
            if (pts2 != null && pts2.Length > 0)
            {
                foreach (var p in pts2)
                    path3.Add(new Vector3(p.X, 0f, p.Y));
            }

#if DEBUG
            DrawDebugGridAndPath(blockedCenters, path3, gridSize, gridExtent);
#endif

            return path3;
        }

        // Debug visualization using ImmediateMesh (efficient, safe)
        private static void DrawDebugGridAndPath(List<Vector2> blockedCenters, List<Vector3> path3, float gridSize, int gridExtent)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var scene = tree?.CurrentScene;
            if (scene == null) return;

            // Clean previous debug nodes
            foreach (var node in scene.GetChildren())
            {
                if (node is Node n)
                    {
                        var nm = n.Name.ToString();
                        if (nm.StartsWith("DebugGrid", StringComparison.Ordinal) || nm.StartsWith("DebugPath", StringComparison.Ordinal))
                            n.QueueFree();
                    }

            }

            // Draw blocked cells (semi-transparent red)
            if (blockedCenters.Count > 0)
            {
                var gridMeshInst = new MeshInstance3D { Name = "DebugGrid" };
                var gridIm = new ImmediateMesh();
                gridIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);

                float half = gridSize * 0.45f;
                float y = 0.02f;

                foreach (var c in blockedCenters)
                {
                    var a = new Vector3(c.X - half, y, c.Y - half);
                    var b = new Vector3(c.X + half, y, c.Y - half);
                    var c2 = new Vector3(c.X + half, y, c.Y + half);
                    var d = new Vector3(c.X - half, y, c.Y + half);

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

            // Draw path (blue line)
            if (path3.Count > 1)
            {
                var pathMeshInst = new MeshInstance3D { Name = "DebugPath" };
                var im = new ImmediateMesh();
                im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
                foreach (var p in path3)
                    im.SurfaceAddVertex(new Vector3(p.X, 0.06f, p.Z));
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

        // Helper to make grid cell IDs unique
        private static long ToId(int gx, int gz, int extent)
        {
            return ((long)(gx + extent) * (extent * 2 + 1)) + (gz + extent);
        }

        // Reeds-Shepp computation (unchanged)
        public static Vector3[] ComputePath3D(
            Vector3 startPos, double startYawRad,
            Vector3 goalPos, double goalYawRad,
            double turnRadiusMeters,
            double sampleStepMeters = 0.25)
        {
            var sM = ToMath3D(startPos, startYawRad);
            var gM = ToMath3D(goalPos, goalYawRad);

            double R = turnRadiusMeters;
            var sN = (sM.x / R, sM.y / R, sM.th);
            var gN = (gM.x / R, gM.y / R, gM.th);

            var best = ReedsSheppPaths.GetOptimalPath(sN, gN);
            if (best == null || best.Count == 0)
                return Array.Empty<Vector3>();

            var ptsLocalNorm = RsSampler.SamplePolylineExact((0.0, 0.0, 0.0), best, 1.0, sampleStepMeters / R);

            var list3 = new List<Vector3>(ptsLocalNorm.Length);
            double c0 = Math.Cos(sM.th), s0 = Math.Sin(sM.th);
            foreach (var p in ptsLocalNorm)
            {
                double sx = p.X * R, sy = p.Y * R;
                double wx = sM.x + (sx * c0 - sy * s0);
                double wy = sM.y + (sx * s0 + sy * c0);
                list3.Add(new Vector3((float)wx, 0f, (float)wy));
            }

            return list3.ToArray();
        }

        // Simple helper for math-space conversion
        private static (double x, double y, double th) ToMath3D(Vector3 pos, double yawRad)
        {
            return (pos.X, pos.Z, yawRad);
        }
    }
}
