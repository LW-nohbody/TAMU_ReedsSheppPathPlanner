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
        private DigConfig _digConfig;
        private float _totalTerrainVolumeRemoved = 0f;

        // OPTIMIZATION: Batch mesh updates instead of updating every dig operation
        private bool _terrainModifiedSinceLastUpdate = false;
        private float _timeSinceLastMeshUpdate = 0f;
        private const float MeshUpdateInterval = 0.1f; // Update mesh 10 times per second

        public DigService(TerrainDisk terrain, DigConfig config)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _digConfig = config ?? DigConfig.Default;
        }

        /// <summary>
        /// Call this each frame to handle batched mesh updates.
        /// </summary>
        public void Update(float deltaSeconds)
        {
            if (_terrainModifiedSinceLastUpdate)
            {
                _timeSinceLastMeshUpdate += deltaSeconds;
                if (_timeSinceLastMeshUpdate >= MeshUpdateInterval)
                {
                    _terrain.RebuildMeshOnly();
                    _terrainModifiedSinceLastUpdate = false;
                    _timeSinceLastMeshUpdate = 0f;
                }
            }
        }

        /// <summary>
        /// Perform excavation at the given world position for the given time duration.
        /// Returns a tuple: (swelled volume for payload, in-situ volume removed from terrain).
        /// OPTIMIZATION: Does NOT immediately rebuild mesh - call Update() to batch updates.
        /// </summary>
        public (float SwelledVolume, float InSituVolume) DigAtPosition(Vector3 pos, float deltaSeconds, float remainingCapacity)
        {
            float r = MathF.Max(0f, _digConfig.DigRadius);
            float depthRate = MathF.Max(0f, _digConfig.DepthRatePerSecond);
            float swell = MathF.Max(1f, _digConfig.SwellFactor);

            if (r <= 0f || depthRate <= 0f || remainingCapacity <= 0f || deltaSeconds <= 0f)
                return (0f, 0f);

            // How much vertical cut we try this frame
            float maxDepthThisFrame = depthRate * deltaSeconds;
            if (maxDepthThisFrame <= 0f)
                return (0f, 0f);

            // Capacity-based clamp: how much depth fits in remainingCapacity
            float effArea = MathF.PI * r * r;
            float capLimitedDepth = remainingCapacity / (swell * effArea);

            float depthToApply = MathF.Min(maxDepthThisFrame, capLimitedDepth);
            if (depthToApply <= 0f)
                return (0f, 0f);

            // Actually lower terrain and get REAL in-situ volume removed
            // OPTIMIZATION: Use new method that doesn't rebuild mesh immediately
            float removedInSitu = _terrain.LowerAreaWithoutMeshUpdate(pos, r, depthToApply);
            if (removedInSitu <= 0f)
                return (0f, 0f);

            _totalTerrainVolumeRemoved += removedInSitu;

            // Mark that terrain has been modified (mesh update will happen in Update())
            _terrainModifiedSinceLastUpdate = true;

            float carried = removedInSitu * swell;

            // Safety clamp for floating point
            if (carried > remainingCapacity)
                carried = remainingCapacity;

            return (carried, removedInSitu); // Return both swelled and in-situ volumes
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
