using Godot;
using System.Collections.Generic;

namespace RSCore
{
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
        }

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

        public void AddObstacle(Obstacle3D obstacle)
        {
            AddChild(obstacle);
            obstacles.Add(obstacle);
        }

        public void ClearObstacles()
        {
            foreach (var o in obstacles)
                o.QueueFree();
            obstacles.Clear();
        }
    }
}