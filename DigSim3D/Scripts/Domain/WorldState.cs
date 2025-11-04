using System.Collections.Generic;
using Godot;
using DigSim3D.App;

namespace DigSim3D.Domain
{
    public sealed class WorldState
    {
        // Points of interest (for mission logic)
        public readonly List<Vector3> DigSites = new();
        public Vector3 DumpCenter;

        // Terrain reference
        public TerrainDisk Terrain = null!;

        // Obstacles in the world
        public List<Obstacle3D> Obstacles = new();

        // World size (meters)
        public float Extent = 50f;
        
        // Dirt extraction tracking
        public float TotalDirtExtracted = 0f;

        // Utility
        public bool HasTerrain => Terrain != null;
    }
}