using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Visualizes robot sectors as radial lines from center.
    /// Each robot is assigned a sector (radial slice) to work in.
    /// </summary>
    public partial class SectorVisualizer : MeshInstance3D
    {
        private int _numSectors = 1;
        private float _arenaRadius = 15f;
        private float _height = 0.1f;  // Height above ground for visibility
        private List<Color> _sectorColors = new();

        public override void _Ready()
        {
            // Default initialization
            GenerateSectorLines(1);
        }

        /// <summary>
        /// Initialize sector visualizer with number of robots and arena radius.
        /// </summary>
        public void Initialize(int numSectors, float arenaRadius)
        {
            _numSectors = Mathf.Max(1, numSectors);
            _arenaRadius = arenaRadius;

            // Generate distinct colors for each sector line
            _sectorColors.Clear();
            for (int i = 0; i < _numSectors; i++)
            {
                float hue = (float)i / _numSectors;
                _sectorColors.Add(Color.FromHsv(hue, 0.8f, 1.0f, 1.0f)); // Bright, fully opaque
            }

            GenerateSectorLines(_numSectors);
        }

        /// <summary>
        /// Generate lines that divide the arena into sectors.
        /// </summary>
        private void GenerateSectorLines(int numSectors)
        {
            if (numSectors <= 0) return;

            var immediateGeometry = new ImmediateMesh();
            immediateGeometry.SurfaceBegin(Mesh.PrimitiveType.Lines);

            float angleStep = Mathf.Tau / numSectors;

            // Draw radial lines from center to edge for each sector boundary
            for (int sector = 0; sector < numSectors; sector++)
            {
                float angle = sector * angleStep;
                
                Color lineColor = _sectorColors.Count > sector ? _sectorColors[sector] : Colors.White;

                // Start point at center (slightly above ground)
                Vector3 startPoint = new Vector3(0, _height, 0);
                
                // End point at arena radius
                Vector3 endPoint = new Vector3(
                    Mathf.Cos(angle) * _arenaRadius,
                    _height,
                    Mathf.Sin(angle) * _arenaRadius
                );

                // Add line
                immediateGeometry.SurfaceSetColor(lineColor);
                immediateGeometry.SurfaceAddVertex(startPoint);
                
                immediateGeometry.SurfaceSetColor(lineColor);
                immediateGeometry.SurfaceAddVertex(endPoint);
            }

            immediateGeometry.SurfaceEnd();
            Mesh = immediateGeometry;

            // Create bright unshaded material
            var material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true  // Always visible on top
            };
            SetSurfaceOverrideMaterial(0, material);
        }

        /// <summary>
        /// Toggle visibility of sector visualization.
        /// </summary>
        public new void SetVisible(bool visible)
        {
            Visible = visible;
        }

        /// <summary>
        /// Update sector colors (if needed dynamically).
        /// </summary>
        public void UpdateSectorColor(int sectorIndex, Color color)
        {
            if (sectorIndex >= 0 && sectorIndex < _sectorColors.Count)
            {
                _sectorColors[sectorIndex] = color;
                GenerateSectorLines(_numSectors);
            }
        }
    }
}
