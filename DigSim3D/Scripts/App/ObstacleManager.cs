using Godot;
using System.Collections.Generic;

using DigSim3D.Domain;

namespace DigSim3D.App
{
    /// <summary>
    /// Manages all obstacles defined in the Godot simulation
    /// </summary>
    public partial class ObstacleManager : Node3D
    {
        private List<Obstacle3D> obstacles = new List<Obstacle3D>();

        public override void _Ready()
        {
            // Gather all Obstacle3D nodes in this manager’s children
            foreach (Node child in GetChildren())
            {
                if (child is Obstacle3D obstacle)
                    obstacles.Add(obstacle);
            }

            GD.Print($"[ObstacleManager] Loaded {obstacles.Count} obstacles.");

            foreach (var obs in obstacles)
            {
                GD.Print($"Obstacle {obs.Name} - Global Position: {obs.GlobalPosition}");
            }

        }

        /// <summary>
        /// Checks if any obstacles intersect with given path
        /// </summary>
        /// <param name="pathPoints"></param>
        /// <returns></returns>
        public bool PathIsValid(List<Vector3> pathPoints)
        {
            if (pathPoints.Count < 2)
                return true;

            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector3 start = pathPoints[i];
                Vector3 end = pathPoints[i + 1];

                foreach (var obstacle in obstacles)
                {
                    if (obstacle.IntersectsSegment(start, end))
                        return false; // Collision detected
                }
            }

            return true; // No collisions along the path
        }

        /// <summary>
        /// Adds new obstacle to manager
        /// </summary>
        /// <param name="obstacle"></param>
        public void AddObstacle(Obstacle3D obstacle)
        {
            AddChild(obstacle);
            obstacles.Add(obstacle);
        }

        /// <summary>
        /// Clears all obstacles
        /// </summary>
        public void ClearObstacles()
        {
            foreach (var o in obstacles)
                o.QueueFree();
            obstacles.Clear();
        }

        /// <summary>
        /// Returns list of obstacles as Obstacle3D objects
        /// </summary>
        /// <returns></returns>
        public List<Obstacle3D> GetObstacles()
        {
            return obstacles;
        }

    }
}