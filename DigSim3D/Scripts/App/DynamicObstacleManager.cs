using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Tracks moving obstacles such as vehicles or tools for dynamic path avoidance.
    /// Each dynamic obstacle reports its current world position.
    /// </summary>
    public partial class DynamicObstacleManager : Node
    {
        private List<Node3D> dynamicObstacles = new List<Node3D>();

        [Export] public float AvoidanceRadius = 1.5f; // meters

        public override void _Ready()
        {
            GD.Print($"[DynamicObstacleManager] Ready. Avoidance Radius = {AvoidanceRadius}");
        }

        /// <summary>
        /// Register a moving obstacle (e.g., a VehicleVisualizer).
        /// </summary>
        public void RegisterDynamicObstacle(Node3D obstacle)
        {
            if (!dynamicObstacles.Contains(obstacle))
            {
                dynamicObstacles.Add(obstacle);
                GD.Print($"[DynamicObstacleManager] Registered dynamic obstacle: {obstacle.Name}");
            }
        }

        /// <summary>
        /// Unregister a dynamic obstacle (if it’s removed or destroyed).
        /// </summary>
        public void UnregisterDynamicObstacle(Node3D obstacle)
        {
            if (dynamicObstacles.Contains(obstacle))
            {
                dynamicObstacles.Remove(obstacle);
                GD.Print($"[DynamicObstacleManager] Unregistered obstacle: {obstacle.Name}");
            }
        }

        /// <summary>
        /// Returns the world positions of all tracked dynamic obstacles.
        /// </summary>
        public List<Vector3> GetDynamicPositions()
        {
            List<Vector3> positions = new List<Vector3>();
            foreach (var obs in dynamicObstacles)
            {
                if (IsInstanceValid(obs))
                    positions.Add(obs.GlobalPosition);
            }
            return positions;
        }

        /// <summary>
        /// Checks if a given position is within avoidance radius of any dynamic obstacle.
        /// </summary>
        public bool IsNearDynamicObstacle(Vector3 position, Node3D ignore)
        {
            foreach (var obs in dynamicObstacles)
            {
                if (!IsInstanceValid(obs) || obs == ignore)
                    continue;

                if (position.DistanceTo(obs.GlobalPosition) < AvoidanceRadius)
                    return true;
            }
            return false;
        }
    }
}
