using System.Collections.Generic;
using Godot;

namespace SimCore.Core
{
    public sealed class WorldState
    {
        // Points of interest (for mission logic)
        public readonly List<Vector3> DigSites = new();
        public Vector3 DumpCenter;

        // Terrain reference
        public TerrainDisk Terrain;

        // Obstacles in the world
        public List<Obstacle3D> Obstacles = new();

        // World size (meters)
        public float Extent = 50f;

        // Utility
        public bool HasTerrain => Terrain != null;
    }
}
