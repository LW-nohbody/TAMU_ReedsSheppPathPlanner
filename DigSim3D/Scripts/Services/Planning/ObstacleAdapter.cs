using Godot;
using System.Collections.Generic;
using DigSim3D.Domain;

namespace DigSim3D.Services
{
    public static class ObstacleAdapter
    {
        /// <summary>
        /// Scans the given node (usually "Obstacles") for CylinderObstacle and MeshInstance3D,
        /// and returns engine-agnostic Obstacle3D data.
        /// </summary>
        public static List<Obstacle3D> ReadFromScene(Node root)
        {
            var list = new List<Obstacle3D>();
            if (root == null) return list;
            Recurse(root, list);
            return list;
        }

        private static void Recurse(Node n, List<Obstacle3D> outList)
        {
            if (n is Node3D n3)
            {
                // Cylinder obstacles (data from the custom node)
                if (n3 is CylinderObstacle cyl)
                {
                    var center = n3.GlobalTransform.Origin;
                    outList.Add(Obstacle3D.FromCylinder(center, cyl.Radius, cyl.Height));
                }
                // Generic AABB from any MeshInstance3D
                else if (n3 is MeshInstance3D mi && mi.Mesh != null)
                {
                    outList.Add(Obstacle3D.FromAabb(GetWorldAabb(mi)));
                }
            }

            foreach (var c in n.GetChildren())
                Recurse(c as Node, outList);
        }

        private static Aabb GetWorldAabb(MeshInstance3D mi)
        {
            var local = mi.GetAabb();
            var xf    = mi.GlobalTransform;

            // 8 corners of local AABB
            var c = new Vector3[8];
            c[0] = local.Position;
            c[1] = local.Position + new Vector3(local.Size.X, 0, 0);
            c[2] = local.Position + new Vector3(0, local.Size.Y, 0);
            c[3] = local.Position + new Vector3(0, 0, local.Size.Z);
            c[4] = local.Position + local.Size;
            c[5] = local.Position + new Vector3(local.Size.X, local.Size.Y, 0);
            c[6] = local.Position + new Vector3(local.Size.X, 0, local.Size.Z);
            c[7] = local.Position + new Vector3(0, local.Size.Y, local.Size.Z);

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < 8; i++)
            {
                var w = xf * c[i];
                min = new Vector3(Mathf.Min(min.X, w.X), Mathf.Min(min.Y, w.Y), Mathf.Min(min.Z, w.Z));
                max = new Vector3(Mathf.Max(max.X, w.X), Mathf.Max(max.Y, w.Y), Mathf.Max(max.Z, w.Z));
            }

            return new Aabb(min, max - min);
        }
    }
}