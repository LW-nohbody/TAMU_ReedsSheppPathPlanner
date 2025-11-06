using Godot;
using System.Collections.Generic;
using DigSim3D.Domain;

namespace DigSim3D.UI
{
    /// <summary>
    /// Visualizes obstacle buffer zones and wall buffer zone in the arena.
    /// Shows where robots are NOT allowed to dig.
    /// </summary>
    public partial class BufferVisualizer : Node3D
    {
        private MeshInstance3D _bufferMesh = null!;
        private const float BufferHeight = 0.1f; // Height above ground for visualization

        public override void _Ready()
        {
            _bufferMesh = new MeshInstance3D { Name = "BufferMesh" };
            AddChild(_bufferMesh);
        }

        /// <summary>
        /// Initialize buffer visualization with obstacles and wall buffer.
        /// </summary>
        /// <param name="obstacles">List of obstacles in the scene</param>
        /// <param name="obstacleBufferMeters">Buffer distance around obstacles (default: 3.0m)</param>
        /// <param name="arenaRadius">Radius of the arena</param>
        /// <param name="wallBufferMeters">Buffer distance from wall (default: 2.5m)</param>
        public void Initialize(List<Obstacle3D> obstacles, float obstacleBufferMeters, float arenaRadius, float wallBufferMeters)
        {
            var im = new ImmediateMesh();
            _bufferMesh.Mesh = im;

            // Create semi-transparent red material for buffer zones
            var bufferMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.0f, 0.0f, 0.15f), // Semi-transparent red
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, // Show both sides
                NoDepthTest = true
            };

            im.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            // 1. Draw obstacle buffer zones (cylinders with buffer)
            foreach (var obs in obstacles)
            {
                if (obs is CylinderObstacle cyl)
                {
                    DrawCylinderBuffer(im, cyl.GlobalPosition, cyl.Radius, obstacleBufferMeters);
                }
            }

            // 2. Draw wall buffer zone (ring at the edge of arena)
            DrawWallBuffer(im, arenaRadius, wallBufferMeters);

            im.SurfaceEnd();
            _bufferMesh.SetSurfaceOverrideMaterial(0, bufferMaterial);

            GD.Print($"[BufferVisualizer] Initialized with {obstacles.Count} obstacles, " +
                     $"obstacle buffer: {obstacleBufferMeters}m, wall buffer: {wallBufferMeters}m");
        }

        /// <summary>
        /// Draw a cylindrical buffer zone around an obstacle.
        /// </summary>
        private void DrawCylinderBuffer(ImmediateMesh im, Vector3 center, float innerRadius, float bufferDistance)
        {
            float outerRadius = innerRadius + bufferDistance;
            int segments = 32;
            float y = BufferHeight;

            // Draw ring (donut shape) from innerRadius to outerRadius
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.Tau / segments;
                float angle2 = (i + 1) * Mathf.Tau / segments;

                // Inner ring points
                Vector3 inner1 = new Vector3(
                    center.X + innerRadius * Mathf.Cos(angle1),
                    y,
                    center.Z + innerRadius * Mathf.Sin(angle1)
                );
                Vector3 inner2 = new Vector3(
                    center.X + innerRadius * Mathf.Cos(angle2),
                    y,
                    center.Z + innerRadius * Mathf.Sin(angle2)
                );

                // Outer ring points
                Vector3 outer1 = new Vector3(
                    center.X + outerRadius * Mathf.Cos(angle1),
                    y,
                    center.Z + outerRadius * Mathf.Sin(angle1)
                );
                Vector3 outer2 = new Vector3(
                    center.X + outerRadius * Mathf.Cos(angle2),
                    y,
                    center.Z + outerRadius * Mathf.Sin(angle2)
                );

                // Draw two triangles for this segment
                // Triangle 1: inner1 -> outer1 -> inner2
                im.SurfaceAddVertex(inner1);
                im.SurfaceAddVertex(outer1);
                im.SurfaceAddVertex(inner2);

                // Triangle 2: inner2 -> outer1 -> outer2
                im.SurfaceAddVertex(inner2);
                im.SurfaceAddVertex(outer1);
                im.SurfaceAddVertex(outer2);
            }
        }

        /// <summary>
        /// Draw a ring buffer zone near the arena wall.
        /// </summary>
        private void DrawWallBuffer(ImmediateMesh im, float arenaRadius, float wallBufferMeters)
        {
            float innerRadius = arenaRadius - wallBufferMeters;
            float outerRadius = arenaRadius;
            int segments = 64; // More segments for smoother outer wall
            float y = BufferHeight;

            // Draw ring (donut shape) from innerRadius to outerRadius
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.Tau / segments;
                float angle2 = (i + 1) * Mathf.Tau / segments;

                // Inner ring points
                Vector3 inner1 = new Vector3(
                    innerRadius * Mathf.Cos(angle1),
                    y,
                    innerRadius * Mathf.Sin(angle1)
                );
                Vector3 inner2 = new Vector3(
                    innerRadius * Mathf.Cos(angle2),
                    y,
                    innerRadius * Mathf.Sin(angle2)
                );

                // Outer ring points
                Vector3 outer1 = new Vector3(
                    outerRadius * Mathf.Cos(angle1),
                    y,
                    outerRadius * Mathf.Sin(angle1)
                );
                Vector3 outer2 = new Vector3(
                    outerRadius * Mathf.Cos(angle2),
                    y,
                    outerRadius * Mathf.Sin(angle2)
                );

                // Draw two triangles for this segment
                // Triangle 1: inner1 -> outer1 -> inner2
                im.SurfaceAddVertex(inner1);
                im.SurfaceAddVertex(outer1);
                im.SurfaceAddVertex(inner2);

                // Triangle 2: inner2 -> outer1 -> outer2
                im.SurfaceAddVertex(inner2);
                im.SurfaceAddVertex(outer1);
                im.SurfaceAddVertex(outer2);
            }
        }
    }
}
