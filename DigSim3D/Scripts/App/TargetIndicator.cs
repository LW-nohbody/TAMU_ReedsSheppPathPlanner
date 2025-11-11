using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Manages visual target indicators (colored spheres) at claimed dig sites.
    /// Each robot gets a sphere in its sector color to show where it's working.
    /// </summary>
    public partial class TargetIndicator : Node3D
    {
        private readonly Dictionary<int, MeshInstance3D> _indicators = new();
        private TerrainDisk _terrain = null!;

        [Export] public float IndicatorRadius { get; set; } = 0.3f;
        [Export] public float HeightAboveTerrain { get; set; } = 0.5f;

        public void Initialize(TerrainDisk terrain)
        {
            _terrain = terrain;
        }

        /// <summary>
        /// Show or update indicator for a robot at a dig site
        /// </summary>
        public void ShowIndicator(int robotId, Vector3 position, Color color)
        {
            if (!_indicators.TryGetValue(robotId, out var indicator))
            {
                // Create new indicator
                indicator = new MeshInstance3D
                {
                    Name = $"Indicator_{robotId}",
                    Mesh = new SphereMesh { Radius = IndicatorRadius, Height = IndicatorRadius * 2 }
                };

                var material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                };
                
                // Make it slightly transparent
                material.AlbedoColor = new Color(color.R, color.G, color.B, 0.7f);
                
                indicator.SetSurfaceOverrideMaterial(0, material);
                AddChild(indicator);
                _indicators[robotId] = indicator;
            }

            // Update position (project to terrain height)
            Vector3 pos = position;
            if (_terrain != null && _terrain.SampleHeightNormal(position, out var hitPos, out var _))
            {
                pos = new Vector3(position.X, hitPos.Y + HeightAboveTerrain, position.Z);
            }

            indicator.GlobalPosition = pos;
            indicator.Visible = true;
        }

        /// <summary>
        /// Hide indicator for a robot
        /// </summary>
        public void HideIndicator(int robotId)
        {
            if (_indicators.TryGetValue(robotId, out var indicator))
            {
                indicator.Visible = false;
            }
        }

        /// <summary>
        /// Remove indicator for a robot completely
        /// </summary>
        public void RemoveIndicator(int robotId)
        {
            if (_indicators.TryGetValue(robotId, out var indicator))
            {
                indicator.QueueFree();
                _indicators.Remove(robotId);
            }
        }

        /// <summary>
        /// Clear all indicators
        /// </summary>
        public void ClearAll()
        {
            foreach (var indicator in _indicators.Values)
            {
                indicator.QueueFree();
            }
            _indicators.Clear();
        }
    }
}
