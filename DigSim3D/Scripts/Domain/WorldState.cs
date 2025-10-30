using System.Collections.Generic;
using Godot;
using DigSim3D.App;

namespace DigSim3D.Domain
{
    /// Snapshot the scheduler/planner can read.
    public sealed class WorldState
    {
        // Where material is dumped (you can move/update this at runtime)
        public Vector3 DumpCenter { get; set; } = Vector3.Zero;

        // Candidate dig spots (seed these from terrain or your grid)
        public List<Vector3> DigSites { get; } = new();

        // Optional: give services access to terrain sampling
        public TerrainDisk? Terrain { get; set; }
        
        // Enhanced dig system tracking
        public float TotalDirtExtracted { get; set; } = 0f;
        
        // Obstacles for path planning
        public List<Obstacle3D> Obstacles { get; set; } = new();
    }
}