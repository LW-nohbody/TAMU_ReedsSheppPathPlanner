// res://Scripts/Services/Planning/GridPlannerPersistent.cs
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
        private static int   _gridExtent;
        private static float _buffer;
        private static bool  _built;

        // debug capture of exactly-which cells were disabled
        public static IReadOnlyList<Vector2> LastBlockedCenters => _lastBlockedCenters;
        private static readonly List<Vector2> _lastBlockedCenters = new();

        public static bool IsBuilt => _built;

        public static void BuildGrid(
            IEnumerable<Obstacle3D> obstacles,
            float gridSize = 0.25f,          // <-- match 3D
            int   gridExtent = 60,           // <-- match 3D
            float obstacleBufferMeters = 0.50f)
        {
            _astar      = new AStar2D();
            _gridSize   = gridSize;
            _gridExtent = gridExtent;
            _buffer     = obstacleBufferMeters;
            _lastBlockedCenters.Clear();

            int dia = gridExtent * 2 + 1;
            _astar.ReserveSpace(dia * dia);

            // add points
            for (int gx = -gridExtent; gx <= gridExtent; gx++)
            for (int gz = -gridExtent; gz <= gridExtent; gz++)
                _astar.AddPoint(ToId(gx, gz, gridExtent),
                    new Vector2(gx * gridSize, gz * gridSize));

            // 8-connected
            for (int gx = -gridExtent; gx <= gridExtent; gx++)
            for (int gz = -gridExtent; gz <= gridExtent; gz++)
            {
                long id = ToId(gx, gz, gridExtent);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = gx + dx, nz = gz + dz;
                    if (Math.Abs(nx) <= gridExtent && Math.Abs(nz) <= gridExtent)
                        _astar.ConnectPoints(id, ToId(nx, nz, gridExtent), false);
                }
            }

            // disable blocked
            if (obstacles != null)
            {
                foreach (var id in _astar.GetPointIds())
                {
                    var c = _astar.GetPointPosition(id);
                    if (CellBlocked(c, obstacles))
                    {
                        _astar.SetPointDisabled(id, true);
                        _lastBlockedCenters.Add(c);
                    }
                }
            }

            _built = true;
            GD.Print($"[GridPlannerPersistent] Built grid ({_gridSize}m, extent {_gridExtent}), blocked={_lastBlockedCenters.Count}");
        }

        public static List<Vector3> Plan2DPath(Vector3 start, Vector3 goal)
        {
            if (!_built) { GD.PushWarning("Grid not built!"); return new(); }

            long s = _astar.GetClosestPoint(new Vector2(start.X, start.Z));
            long g = _astar.GetClosestPoint(new Vector2(goal.X,  goal.Z));
            if (s == -1 || g == -1) return new();

            var path2 = _astar.GetPointPath(s, g);
            var path3 = new List<Vector3>(path2.Length);
            for (int i = 0; i < path2.Length; i++)
                path3.Add(new Vector3(path2[i].X, 0, path2[i].Y));
            return path3;
        }

        private static bool CellBlocked(Vector2 c, IEnumerable<Obstacle3D> obs)
        {
            // match 3D: circle test for cylinders, AABB for boxes
            // tiny conservative inflation by half-diagonal of a cell
            float halfDiag = (float)(Math.Sqrt(2.0) * _gridSize * 0.5);

            foreach (var o in obs)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    var center2 = new Vector2(o.Center.X, o.Center.Z);
                    float eff = o.Radius + _buffer + halfDiag;
                    if (c.DistanceTo(center2) <= eff) return true;
                }
                else
                {
                    float hx = Math.Max(0f, o.Extents.X) + _buffer;
                    float hz = Math.Max(0f, o.Extents.Z) + _buffer;
                    if (c.X >= o.Center.X - hx && c.X <= o.Center.X + hx &&
                        c.Y >= o.Center.Z - hz && c.Y <= o.Center.Z + hz)
                        return true;
                }
            }
            return false;
        }

        private static long ToId(int gx, int gz, int extent)
            => ((long)(gx + extent) * (extent * 2 + 1)) + (gz + extent);
    }
}