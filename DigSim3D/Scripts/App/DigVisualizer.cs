using Godot;
using DigSim3D.Domain;

namespace DigSim3D.App
{
    /// <summary>
    /// Visualizes dig activity: shows dig cones, payload indicators, and status.
    /// </summary>
    public partial class DigVisualizer : Node3D
    {
        [Export] public bool ShowDigCones = true;
        [Export] public bool ShowPayloadBars = true;
        [Export] public Color DigConeColor = new(1.0f, 0.5f, 0f, 0.3f);  // Orange, semi-transparent
        [Export] public Color PayloadBarColor = new(0.2f, 1.0f, 0.2f, 1f);  // Bright green
        [Export] public float DigConeHeight = 1.5f;

        public override void _Ready()
        {
            // Debug draw setup would go here
        }

        /// <summary>
        /// Draw a debug cone at the dig site (to show where robot is digging).
        /// </summary>
        public void DrawDigCone(Vector3 centerPos, float radius, float depth, Color color)
        {
            if (!ShowDigCones) return;

            // Draw a simple cylinder as dig cone using debug draw
            DebugDraw.Sphere(centerPos, radius, color);
        }

        /// <summary>
        /// Draw a payload indicator bar above the robot.
        /// </summary>
        public void DrawPayloadBar(Vector3 robotPos, float currentPayload, float maxPayload)
        {
            if (!ShowPayloadBars) return;

            float barWidth = 1.5f;
            float barHeight = 0.3f;
            float payloadRatio = Mathf.Clamp(currentPayload / maxPayload, 0f, 1f);

            // Position bar above robot
            Vector3 barCenter = robotPos + Vector3.Up * 1.5f;

            // Draw background bar (unfilled)
            DebugDraw.Box(barCenter, new Vector3(barWidth, barHeight, 0.1f), new Color(0.3f, 0.3f, 0.3f, 0.7f));

            // Draw filled portion
            Vector3 filledSize = new Vector3(barWidth * payloadRatio, barHeight, 0.11f);
            Vector3 filledCenter = barCenter + new Vector3(-barWidth * (1f - payloadRatio) * 0.5f, 0, 0.05f);
            DebugDraw.Box(filledCenter, filledSize, PayloadBarColor);

            // Draw percentage text
            string payloadText = $"{(payloadRatio * 100f):F0}%";
            // TODO: Draw text above bar if DebugDraw supports it
        }
    }

    /// <summary>
    /// Simple debug drawing utilities (using DebugDraw3D-like pattern).
    /// </summary>
    public static class DebugDraw
    {
        public static void Sphere(Vector3 center, float radius, Color color)
        {
            // Draw a circle in XZ plane
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * Mathf.Tau;
                float angle2 = (float)(i + 1) / segments * Mathf.Tau;

                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                DebugGeometry.DrawLine(p1, p2, color);
            }
        }

        public static void Box(Vector3 center, Vector3 size, Color color)
        {
            Vector3 half = size / 2f;
            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-half.X, -half.Y, -half.Z),
                center + new Vector3(half.X, -half.Y, -half.Z),
                center + new Vector3(half.X, half.Y, -half.Z),
                center + new Vector3(-half.X, half.Y, -half.Z),
                center + new Vector3(-half.X, -half.Y, half.Z),
                center + new Vector3(half.X, -half.Y, half.Z),
                center + new Vector3(half.X, half.Y, half.Z),
                center + new Vector3(-half.X, half.Y, half.Z),
            };

            // Draw box edges
            int[] edges = 
            { 
                0, 1,  1, 2,  2, 3,  3, 0,  // Front face
                4, 5,  5, 6,  6, 7,  7, 4,  // Back face
                0, 4,  1, 5,  2, 6,  3, 7   // Connecting edges
            };

            for (int i = 0; i < edges.Length; i += 2)
            {
                DebugGeometry.DrawLine(corners[edges[i]], corners[edges[i + 1]], color);
            }
        }

        public static void Line(Vector3 from, Vector3 to, Color color)
        {
            DebugGeometry.DrawLine(from, to, color);
        }
    }

    /// <summary>
    /// Wrapper for simple line drawing (uses DebugDraw3D if available, otherwise no-op).
    /// </summary>
    public static class DebugGeometry
    {
        public static void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            // In Godot 4.x, we could use DebugDraw3D addon or just print for now
            // For now, this is a placeholder that does nothing
            // In a real implementation, you'd use Godot's debug drawing capabilities
        }
    }
}
