using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

using DigSim3D.Domain;

namespace DigSim3D.Services
{
    public static class GridPlannerPersistent
    {
        private static AStar2D _astar = null!;
        private static float _gridSize;
        private static int _gridExtent;
        private static float _obstacleBuffer;
        private static bool _built = false;
        public static bool IsBuilt => _built;



        private static readonly List<Vector2> _lastBlockedCenters = new();
        public static IReadOnlyList<Vector2> LastBlockedCenters => _lastBlockedCenters;


        /// <summary>
        /// Builds A* grid around obstacles
        /// </summary>
        /// <param name="obstacles"></param>
        /// <param name="gridSize"></param>
        /// <param name="gridExtent"></param>
        /// <param name="obstacleBufferMeters"></param>
        // --- Replace BuildGrid with this version (only additions are GD.Print lines) ---
        public static void BuildGrid(
            IEnumerable<Obstacle3D> obstacles,
            float gridSize = 0.5f,
            int gridExtent = 40,
            float obstacleBufferMeters = 0.0f)
        {
            _astar = new AStar2D();
            _gridSize = gridSize;
            _gridExtent = gridExtent;
            _obstacleBuffer = obstacleBufferMeters;

            int diameter = gridExtent * 2 + 1;
            _astar.ReserveSpace(diameter * diameter);

            _lastBlockedCenters.Clear();

            // 1) Create grid points
            for (int gx = -gridExtent; gx <= gridExtent; gx++)
            {
                for (int gz = -gridExtent; gz <= gridExtent; gz++)
                {
                    long id = ToId(gx, gz, gridExtent);
                    Vector2 pos2 = new Vector2(gx * gridSize, gz * gridSize);
                    _astar.AddPoint(id, pos2);
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
                                _astar.ConnectPoints(id, nid, false);
                            }
                        }
                    }
                }
            }

            // 3) Disable cells colliding with obstacles
            if (obstacles != null && obstacles.Count() > 0)
            {
                float cellHalfDiag = (float)(Math.Sqrt(2.0) * gridSize * 0.5);
                int blockedCount = 0;

                // Debug: print grid parameters and obstacle overview
                GD.Print($"[GridPlannerPersistent] BuildGrid: gridSize={gridSize}, gridExtent={gridExtent}, obstacleBuffer={obstacleBufferMeters}");
                GD.Print($"[GridPlannerPersistent] BuildGrid: obstacles.Count()={obstacles.Count()}");
                int oi = 0;
                foreach (var obs in obstacles)
                {
                    if (obs is CylinderObstacle cyl)
                        GD.Print($"  obs[{oi}] cyl at {cyl.GlobalPosition} r={cyl.Radius}");
                    else
                        GD.Print($"  obs[{oi}] type={obs?.GetType().Name}");
                    oi++;
                }

                for (int gx = -gridExtent; gx <= gridExtent; gx++)
                {
                    for (int gz = -gridExtent; gz <= gridExtent; gz++)
                    {
                        long id = ToId(gx, gz, gridExtent);
                        Vector2 cellCenter = _astar.GetPointPosition(id);

                        bool disabled = false;
                        foreach (var obs in obstacles)
                        {
                            // 🔸 handle cylinders
                            if (obs is CylinderObstacle cyl)
                            {
                                Vector2 obs2 = new Vector2(cyl.GlobalPosition.X, cyl.GlobalPosition.Z);
                                float effectiveRadius = cyl.Radius + obstacleBufferMeters + cellHalfDiag;
                                if (cellCenter.DistanceTo(obs2) <= effectiveRadius)
                                {
                                    disabled = true;
                                    break;
                                }
                            }

                            // 🔸 placeholder for other types (future extension)
                            // else if (obs is BoxObstacle box) { ... }
                        }

                        if (disabled)
                        {
                            _astar.SetPointDisabled(id, true);
                            blockedCount++;
                            _lastBlockedCenters.Add(cellCenter);
                        }
                    }
                }

                GD.Print($"GridPlannerPersistent: disabled {blockedCount} cells for {obstacles.Count()} obstacles.");
                // Print a few blocked centers for verification
                for (int k = 0; k < Math.Min(8, _lastBlockedCenters.Count); k++)
                    GD.Print($"  blocked[{k}] = {_lastBlockedCenters[k]}");
            }

            _built = true;
            GD.Print("GridPlannerPersistent: grid built and cached.");
        }


        /// <summary>
        /// Plans the A* path around the obstacles, then returns that path
        /// </summary>
        /// <param name="start"></param>
        /// <param name="goal"></param>
        /// <returns></returns>
        // --- Replace Plan2DPath with this version (adds pre/post prints and simple consistency checks) ---
        public static List<Vector3> Plan2DPath(Vector3 start, Vector3 goal)
        {
            if (!_built)
            {
                GD.PushWarning("GridPlannerPersistent: Grid not built yet! Call BuildGrid() first.");
                return new List<Vector3>();
            }

            GD.Print($"[GridPlannerPersistent] Plan2DPath: start=({start.X:F2},{start.Z:F2}) goal=({goal.X:F2},{goal.Z:F2})");
            GD.Print($"[GridPlannerPersistent] Plan2DPath: gridSize={_gridSize} gridExtent={_gridExtent} blockedCells={_lastBlockedCenters.Count}");

            long startId = _astar.GetClosestPoint(new Vector2(start.X, start.Z));
            long goalId = _astar.GetClosestPoint(new Vector2(goal.X, goal.Z));

            GD.Print($"[GridPlannerPersistent] Plan2DPath: startId={startId} goalId={goalId}");

            if (startId == -1 || goalId == -1)
            {
                GD.PrintErr("[GridPlannerPersistent] Plan2DPath: closest point lookup failed for start or goal.");
                return new List<Vector3>();
            }

            var pts2 = _astar.GetPointPath(startId, goalId);
            if (pts2 == null)
            {
                GD.PrintErr("[GridPlannerPersistent] Plan2DPath: GetPointPath returned null.");
                return new List<Vector3>();
            }

            GD.Print($"[GridPlannerPersistent] Plan2DPath: pts2.Length = {pts2.Length}");
            for (int k = 0; k < Math.Min(8, pts2.Length); k++)
                GD.Print($"  path2[{k}] = {pts2[k]}");

            var path3 = pts2.Select(p => new Vector3(p.X, 0f, p.Y)).ToList();

            // quick sanity check: ensure path3 doesn't pass through blocked centers
            if (_lastBlockedCenters.Count > 0)
            {
                for (int pi = 0; pi < path3.Count; pi++)
                {
                    var p = path3[pi];
                    for (int bi = 0; bi < _lastBlockedCenters.Count; bi++)
                    {
                        var bc = _lastBlockedCenters[bi];
                        float dx = p.X - bc.X;
                        float dz = p.Z - bc.Y;
                        float d2 = dx * dx + dz * dz;
                        if (d2 < (_gridSize * _gridSize * 0.9f)) // close enough to warn
                        {
                            GD.PrintErr($"[GridPlannerPersistent] WARNING: planned path point {pi} {p} is within grid cell {bc}");
                            // break early to avoid spamming
                            bi = _lastBlockedCenters.Count;
                            pi = path3.Count;
                        }
                    }
                }
            }

            return path3;
        }


        /// <summary>
        /// Rebuilds the cached grid manually (for dynamic worlds).
        /// </summary>
        /// <param name="obstacles"></param>
        public static void Rebuild(List<Obstacle3D> obstacles)
        {
            BuildGrid(obstacles, _gridSize, _gridExtent, _obstacleBuffer);
        }

        /// <summary>
        /// Gets id of object of location gx, gz
        /// </summary>
        /// <param name="gx"></param>
        /// <param name="gz"></param>
        /// <param name="extent"></param>
        /// <returns></returns>
        private static long ToId(int gx, int gz, int extent)
        {
            return ((long)(gx + extent) * (extent * 2 + 1)) + (gz + extent);
        }

        /// <summary>
        /// Returns true if the grid cell under this world-space XZ is disabled
        /// (i.e., falls inside an obstacle + the buffer passed to BuildGrid).
        /// </summary>
        /// <param name="xz"></param>
        /// <returns></returns>
        public static bool IsCellBlocked(Vector3 xz)
        {
            if (!_built) return false;

            int gx = Mathf.RoundToInt(xz.X / _gridSize);
            int gz = Mathf.RoundToInt(xz.Z / _gridSize);

            // Treat out-of-bounds as blocked so schedulers avoid outside the baked grid.
            if (Math.Abs(gx) > _gridExtent || Math.Abs(gz) > _gridExtent)
                return true;

            long id = ToId(gx, gz, _gridExtent);
            return _astar.IsPointDisabled(id);
        }

        /// <summary>
        /// Optional inflated check – looks at neighboring cells within a circular radius
        /// measured in meters (useful if a wider keep-out than BuildGrid is needed).
        /// </summary>
        /// <param name="xz"></param>
        /// <param name="extraInflationMeters"></param>
        /// <returns></returns>
        public static bool IsCellBlocked(Vector3 xz, float extraInflationMeters)
        {
            if (!_built) return false;

            int cx = Mathf.RoundToInt(xz.X / _gridSize);
            int cz = Mathf.RoundToInt(xz.Z / _gridSize);

            int rCells = Mathf.CeilToInt(extraInflationMeters / Math.Max(_gridSize, 1e-4f));
            int r2 = rCells * rCells;

            for (int dz = -rCells; dz <= rCells; dz++)
                for (int dx = -rCells; dx <= rCells; dx++)
                {
                    if (dx * dx + dz * dz > r2) continue;

                    int gx = cx + dx;
                    int gz = cz + dz;

                    if (Math.Abs(gx) > _gridExtent || Math.Abs(gz) > _gridExtent)
                        return true; // treat off-grid as blocked

                    long id = ToId(gx, gz, _gridExtent);
                    if (_astar.IsPointDisabled(id)) return true;
                }
            return false;
        }
    }
}