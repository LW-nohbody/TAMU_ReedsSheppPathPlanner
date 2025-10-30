using Godot;
using System.Collections.Generic;

namespace SimCore.Game
{
    /// <summary>
    /// Visualizes Reeds-Shepp paths for all robots
    /// </summary>
    public partial class PathVisualizer : Node3D
    {
        private Dictionary<int, PathData> _robotPaths = new();
        private MeshInstance3D _meshInstance = null!;
        private ImmediateMesh _mesh = null!;
        private bool _visible = true;

        private class PathData
        {
            public List<Vector3> Points = new();
            public Color Color;
            public float Alpha = 0.8f;
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

            // Material for paths
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                DisableReceiveShadows = true
            };
            _meshInstance.MaterialOverride = material;
        }

        /// <summary>
        /// Register a robot's path
        /// </summary>
        public void RegisterRobotPath(int robotId, Color color)
        {
            _robotPaths[robotId] = new PathData { Color = color };
        }

        /// <summary>
        /// Update a robot's current path
        /// </summary>
        public void UpdatePath(int robotId, List<Vector3> points)
        {
            if (!_robotPaths.ContainsKey(robotId))
                return;

            _robotPaths[robotId].Points = new List<Vector3>(points);
            RedrawPaths();
        }

        private void RedrawPaths()
        {
            if (_mesh == null) return;

            _mesh.ClearSurfaces();

            foreach (var kvp in _robotPaths)
            {
                var pathData = kvp.Value;
                if (pathData.Points.Count < 2) continue;

                _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

                for (int i = 0; i < pathData.Points.Count; i++)
                {
                    Color vertColor = pathData.Color with { A = pathData.Alpha };
                    _mesh.SurfaceSetColor(vertColor);
                    _mesh.SurfaceAddVertex(pathData.Points[i]);
                }

                _mesh.SurfaceEnd();
            }
        }

        public override void _Process(double delta)
        {
            if (_visible)
                RedrawPaths();
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
    }
}
