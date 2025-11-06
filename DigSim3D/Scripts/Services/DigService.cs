using System;
using Godot;
using DigSim3D.App;
using DigSim3D.Domain;

namespace DigSim3D.Services
{
    /// <summary>
    /// Manages terrain digging and deformation.
    /// Handles excavation circles and real-time mesh updates.
    /// </summary>
    public sealed class DigService
    {
        private TerrainDisk _terrain;
        private DigConfig _config;
        private float _totalTerrainVolumeRemoved = 0f;

        public DigService(TerrainDisk terrain, DigConfig config)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _config = config ?? DigConfig.Default;
        }

        /// <summary>
        /// Perform excavation at the given world position for the given time duration.
        /// Returns the volume of dirt excavated (m³).
        /// </summary>
        public float DigAtPosition(Vector3 worldPos, float digDurationSeconds)
        {
            if (_terrain == null) return 0f;

            // Calculate volume based on dig rate and duration
            float volumePerSecond = _config.DigRatePerSecond;
            float volumeToRemove = volumePerSecond * digDurationSeconds;

            // Convert volume to depth (assuming circular excavation)
            // Volume = π * r² * depth
            // depth = Volume / (π * r²)
            float digRadius = _config.DigRadius;
            float depthToRemove = volumeToRemove / (Mathf.Pi * digRadius * digRadius);

            // Lower the terrain at this position (this calls RebuildMeshOnly internally)
            _terrain.LowerArea(worldPos, digRadius, depthToRemove);

            _totalTerrainVolumeRemoved += volumeToRemove;
            return volumeToRemove;
        }

        /// <summary>
        /// Get total terrain volume that has been excavated.
        /// </summary>
        public float GetTotalVolumeRemoved() => _totalTerrainVolumeRemoved;

        /// <summary>
        /// Reset dig statistics.
        /// </summary>
        public void ResetStats()
        {
            _totalTerrainVolumeRemoved = 0f;
        }
    }
}
