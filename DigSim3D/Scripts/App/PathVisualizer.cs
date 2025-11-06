using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Visualizes vehicle paths as trails using ImmediateMesh
    /// </summary>
    public partial class PathVisualizer : Node3D
    {
        private readonly Dictionary<int, List<Vector3>> _vehiclePaths = new();
        private readonly Dictionary<int, Color> _vehicleColors = new();
        private MeshInstance3D _meshInstance = null!;
        private ImmediateMesh _mesh = null!;
        private StandardMaterial3D _material = null!;
        private bool _visible = true;
        
        private const int MaxPathPoints = 1000; // Limit trail length
        private const float MinDistanceBetweenPoints = 0.5f; // Smoothness
        
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

            // Create material for paths
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
            if (!_vehiclePaths.ContainsKey(vehicleId))
            {
                _vehiclePaths[vehicleId] = new List<Vector3>();
                _vehicleColors[vehicleId] = color;
            }
        }

        /// <summary>
        /// Add a point to a vehicle's path
        /// </summary>
        public void AddPoint(int vehicleId, Vector3 position)
        {
            if (!_vehiclePaths.ContainsKey(vehicleId))
                return;

            var path = _vehiclePaths[vehicleId];
            
            // Only add if far enough from last point (smoothness)
            if (path.Count > 0)
            {
                float dist = position.DistanceTo(path[path.Count - 1]);
                if (dist < MinDistanceBetweenPoints)
                    return;
            }

            path.Add(position);

            // Limit path length
            if (path.Count > MaxPathPoints)
                path.RemoveAt(0);

            // Trigger redraw
            QueueRedraw();
        }

        /// <summary>
        /// Clear all paths
        /// </summary>
        public void ClearAllPaths()
        {
            foreach (var path in _vehiclePaths.Values)
                path.Clear();
            QueueRedraw();
        }

        /// <summary>
        /// Clear a specific vehicle's path
        /// </summary>
        public void ClearPath(int vehicleId)
        {
            if (_vehiclePaths.ContainsKey(vehicleId))
            {
                _vehiclePaths[vehicleId].Clear();
                QueueRedraw();
            }
        }

        private void QueueRedraw()
        {
            CallDeferred(nameof(RedrawPaths));
        }

        private void RedrawPaths()
        {
            if (_mesh == null || !_visible) return;

            _mesh.ClearSurfaces();

            // Draw each vehicle's path
            foreach (var kvp in _vehiclePaths)
            {
                int vehicleId = kvp.Key;
                var path = kvp.Value;
                
                if (path.Count < 2) continue;

                Color color = _vehicleColors.ContainsKey(vehicleId) 
                    ? _vehicleColors[vehicleId] 
                    : Colors.White;

                // Make path semi-transparent and fade along length
                _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

                for (int i = 0; i < path.Count; i++)
                {
                    // Fade from transparent at start to more opaque at end
                    float alpha = Mathf.Lerp(0.2f, 0.8f, (float)i / path.Count);
                    Color vertColor = color with { A = alpha };
                    
                    _mesh.SurfaceSetColor(vertColor);
                    _mesh.SurfaceAddVertex(path[i]);
                }

                _mesh.SurfaceEnd();
            }
        }

        public override void _Process(double delta)
        {
            // Continuously update to keep trails visible
            if (_visible)
                RedrawPaths();
        }
    }
}
