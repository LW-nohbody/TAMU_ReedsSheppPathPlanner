using System.Collections.Generic;
using Godot;
using DigSim3D.App;

namespace DigSim3D.Domain
{
    /// <summary>
    /// Tank definition
    /// </summary>
    public sealed class WorldState
    {
        // Points of interest (for mission logic)
        public readonly List<Vector3> DigSites = new();
        public Vector3 DumpCenter;

        // Terrain reference
        public TerrainDisk Terrain = null!;

        // Obstacles in the world
        public List<Obstacle3D> Obstacles = new();
    }
}