// res://Scripts/Domain/Obstacle3D.cs
using Godot;

namespace DigSim3D.Domain
{
    public enum ObstacleShape { Cylinder, AABB }

    /// <summary>
    /// Engine-agnostic obstacle description used by planners.
    /// - Cylinder: uses Center (XZ), Radius, Height (optional)
    /// - AABB:     uses Center and Extents (half-extents in XYZ)
    /// </summary>
    public sealed class Obstacle3D
    {
        public ObstacleShape Shape;
        public Vector3 Center;   // world-space center
        public float   Radius;   // cylinder radius in XZ
        public float   Height;   // cylinder height (optional for planners)
        public Vector3 Extents;  // AABB half-extents

        // -------- Static factories expected by ObstacleAdapter --------
        public static Obstacle3D FromCylinder(Vector3 center, float radius, float height = 0f)
        {
            return new Obstacle3D
            {
                Shape  = ObstacleShape.Cylinder,
                Center = center,
                Radius = radius,
                Height = height,
                Extents = Vector3.Zero
            };
        }

        public static Obstacle3D FromAabb(Aabb worldAabb)
        {
            // Convert AABB (min + size) -> center + half-extents
            var half   = worldAabb.Size * 0.5f;
            var center = worldAabb.Position + half;

            return new Obstacle3D
            {
                Shape   = ObstacleShape.AABB,
                Center  = center,
                Extents = half,
                Radius  = 0f,
                Height  = 0f
            };
        }

        // -------- Optional convenience (kept if you used earlier) --------
        public static Obstacle3D Box(Vector3 center, Vector3 halfExtents)
        {
            return new Obstacle3D
            {
                Shape   = ObstacleShape.AABB,
                Center  = center,
                Extents = halfExtents
            };
        }
    }
}