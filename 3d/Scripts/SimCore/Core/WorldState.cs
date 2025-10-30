using System.Collections.Generic;
using Godot;

namespace SimCore.Core
{
    public sealed class WorldState
    {
        // Dig sites with detailed information (for your simple dig system)
        public readonly List<DigSite> DigSites = new();
        
        // Dump location
        public Vector3 DumpCenter;
        
        // Cumulative dirt removed (for your dig system)
        public float TotalDirtExtracted = 0f;

        // Terrain reference (from main branch)
        public TerrainDisk Terrain;

        // Obstacles in the world (from main branch)
        public List<Obstacle3D> Obstacles = new();

        // World size (meters) (from main branch)
        public float Extent = 50f;

        // Utility (from main branch)
        public bool HasTerrain => Terrain != null;
    }
}
