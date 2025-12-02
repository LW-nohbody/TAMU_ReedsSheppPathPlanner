using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Visualizes planned paths for vehicles (the path they WILL take)
    /// </summary>
    public partial class PlannedPathVisualizer : Node3D
    {
        private readonly Dictionary<int, PathData> _plannedPaths = new();
        private readonly Dictionary<int, Color> _vehicleColors = new();
        private MeshInstance3D _meshInstance = null!;
        private ImmediateMesh _mesh = null!;
        private StandardMaterial3D _material = null!;
        private bool _visible = true;
        
        private class PathData
        {
            public List<Vector3> Points = new();
            public List<int> Gears = new(); // Forward/reverse indicators
        }
        
        public new bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (_meshInstance != null)
                    _meshInstance.Visible = value;
            }
        }

        public override void _Ready()
        {
            _mesh = new ImmediateMesh();
            _meshInstance = new MeshInstance3D
            {
                Mesh = _mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_meshInstance);

            // Create material for paths - slightly transparent, drawn on top
            _material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true, // Draw on top
                DisableReceiveShadows = true,
                AlbedoColor = Colors.White
            };
            _meshInstance.MaterialOverride = _material;
        }

        /// <summary>
        /// Register a vehicle with a specific color
        /// </summary>
        public void RegisterVehicle(int vehicleId, Color color)
        {
            if (!_plannedPaths.ContainsKey(vehicleId))
            {
                _plannedPaths[vehicleId] = new PathData();
                _vehicleColors[vehicleId] = color;
            }
        }

        /// <summary>
        /// Update a vehicle's planned path
        /// </summary>
        public void UpdatePath(int vehicleId, List<Vector3> points, List<int> gears)
        {
            if (!_plannedPaths.ContainsKey(vehicleId))
                return;

            var pathData = _plannedPaths[vehicleId];
            pathData.Points = new List<Vector3>(points);
            pathData.Gears = new List<int>(gears);

            // Trigger redraw
            QueueRedraw();
        }

        /// <summary>
        /// Clear a vehicle's planned path
        /// </summary>
        public void ClearPath(int vehicleId)
        {
            if (_plannedPaths.ContainsKey(vehicleId))
            {
                _plannedPaths[vehicleId].Points.Clear();
                _plannedPaths[vehicleId].Gears.Clear();
                QueueRedraw();
            }
        }

        /// <summary>
        /// Clear all planned paths
        /// </summary>
        public void ClearAllPaths()
        {
            foreach (var path in _plannedPaths.Values)
            {
                path.Points.Clear();
                path.Gears.Clear();
            }
            QueueRedraw();
        }

        private void QueueRedraw()
        {
            CallDeferred(nameof(RedrawPaths));
        }

        /// <summary>
        /// Clears mesh and draws each vehicle's path color-coding based on gear
        /// </summary>
        private void RedrawPaths()
        {
            if (_mesh == null || !_visible) return;

            _mesh.ClearSurfaces();

            // Draw each vehicle's planned path
            foreach (var kvp in _plannedPaths)
            {
                int vehicleId = kvp.Key;
                var pathData = kvp.Value;
                
                if (pathData.Points.Count < 2) continue;

                Color color = _vehicleColors.ContainsKey(vehicleId) 
                    ? _vehicleColors[vehicleId] 
                    : Colors.White;

                // Draw path as a thick line strip
                _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

                for (int i = 0; i < pathData.Points.Count; i++)
                {
                    // Color based on gear (forward = bright, reverse = dimmer)
                    float brightness = 1.0f;
                    if (i < pathData.Gears.Count && pathData.Gears[i] < 0)
                    {
                        brightness = 0.6f; // Dimmer for reverse
                    }
                    
                    Color vertColor = color * brightness;
                    vertColor.A = 0.85f;
                    
                    _mesh.SurfaceSetColor(vertColor);
                    _mesh.SurfaceAddVertex(pathData.Points[i]);
                }

                _mesh.SurfaceEnd();
            }
        }

        public override void _Process(double delta)
        {
            // Continuously update to keep paths visible
            if (_visible)
                RedrawPaths();
        }
    }
}
