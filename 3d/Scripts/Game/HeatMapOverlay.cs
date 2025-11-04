using Godot;
using System.Collections.Generic;

namespace SimCore.Game
{
    /// <summary>
    /// Heat map overlay system - shows terrain height with color gradient
    /// </summary>
    public partial class HeatMapOverlay : Node3D
    {
        private TerrainDisk _terrain = null!;
        private bool _enabled = false;
        private float _minHeight = float.MaxValue;
        private float _maxHeight = float.MinValue;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    ApplyHeatMap();
                }
            }
        }

        public override void _Ready()
        {
            _terrain = GetParent<TerrainDisk>();
            if (_terrain == null)
            {
                GD.PushError("HeatMapOverlay must be child of TerrainDisk");
            }
        }

        /// <summary>
        /// Apply or remove heat map coloring
        /// </summary>
        public void ApplyHeatMap()
        {
            if (_terrain == null) return;

            if (_enabled)
            {
                // Calculate height bounds
                ScanHeights();
                // Apply color gradient
                ColorizeByHeight();
            }
            else
            {
                // Reset to natural colors
                ResetToNatural();
            }
        }

        private void ScanHeights()
        {
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;

            // This would need access to terrain heights
            // For now, we'll use a reasonable range
            _minHeight = -0.2f;
            _maxHeight = 0.4f;
        }

        private void ColorizeByHeight()
        {
            // This will be implemented in TerrainDisk
            // For now, just flag the terrain for update
            _terrain.Rebuild();
        }

        private void ResetToNatural()
        {
            _terrain.Rebuild();
        }

        public override void _Process(double delta)
        {
            // Toggle heat map with 'H' key
            if (Input.IsKeyPressed(Key.H))
            {
                Enabled = !Enabled;
                GD.Print($"Heat Map: {(Enabled ? "ON" : "OFF")}");
            }
        }
    }
}
