using Godot;
using System;

namespace SimCore.Game
{
    /// <summary>
    /// Handles terrain modification (digging) and mesh updates
    /// </summary>
    public partial class TerrainModifier : Node3D
    {
        private TerrainDisk _terrain = null!;

        public override void _Ready()
        {
            _terrain = GetParent<TerrainDisk>();
            if (_terrain == null)
            {
                GD.PushError("TerrainModifier must be child of TerrainDisk");
            }
        }

        /// <summary>
        /// Dig at a location and return amount extracted
        /// </summary>
        public float Dig(Vector3 worldPosition, float digRadius = 1.0f, float digDepth = 0.03f)
        {
            if (_terrain == null) return 0f;

            // Modify terrain height
            _terrain.LowerArea(worldPosition, digRadius, digDepth);

            // Calculate extracted volume (rough approximation)
            float extractedVolume = Mathf.Pi * digRadius * digRadius * digDepth;

            return extractedVolume;
        }

        /// <summary>
        /// Update terrain mesh after modifications
        /// </summary>
        public void UpdateMesh()
        {
            if (_terrain == null) return;

            // Trigger mesh rebuild in terrain
            _terrain.Rebuild();
        }

        /// <summary>
        /// Get current height at world position
        /// </summary>
        public float GetHeight(Vector3 worldPosition)
        {
            if (_terrain == null) return 0f;

            if (_terrain.SampleHeightNormal(worldPosition, out var hitPos, out var _))
            {
                return hitPos.Y;
            }

            return 0f;
        }
    }
}
