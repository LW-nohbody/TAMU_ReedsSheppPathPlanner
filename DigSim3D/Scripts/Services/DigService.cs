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

        public DigService(TerrainDisk terrain, DigConfig config)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _digConfig = config ?? DigConfig.Default;
        }

        /// <summary>
        /// Perform excavation at the given world position for the given time duration.
        /// Returns the volume of dirt excavated (m³).
        /// </summary>
        public float DigAtPosition(Vector3 pos, float deltaSeconds, float remainingCapacity)
        {
            float r = MathF.Max(0f, _digConfig.DigRadius);
            float depthRate = MathF.Max(0f, _digConfig.DepthRatePerSecond);
            float swell = MathF.Max(1f, _digConfig.SwellFactor);

            if (r <= 0f || depthRate <= 0f || remainingCapacity <= 0f || deltaSeconds <= 0f)
                return 0f;

            // How much vertical cut we try this frame
            float maxDepthThisFrame = depthRate * deltaSeconds;
            if (maxDepthThisFrame <= 0f)
                return 0f;

            // Capacity-based clamp: how much depth fits in remainingCapacity
            float effArea = MathF.PI * r * r;
            float capLimitedDepth = remainingCapacity / (swell * effArea);

            float depthToApply = MathF.Min(maxDepthThisFrame, capLimitedDepth);
            if (depthToApply <= 0f)
                return 0f;

            // Actually lower terrain and get REAL in-situ volume removed
            float removedInSitu = _terrain.LowerAreaAndReturnRemovedVolume(pos, r, depthToApply);
            if (removedInSitu <= 0f)
                return 0f;

            float carried = removedInSitu * swell;

            // Safety clamp for floating point
            if (carried > remainingCapacity)
                carried = remainingCapacity;

            return carried; // robot adds this to CurrentPayload
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
