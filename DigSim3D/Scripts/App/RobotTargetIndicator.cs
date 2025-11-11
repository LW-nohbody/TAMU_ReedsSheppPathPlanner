using Godot;

namespace DigSim3D.App
{
    /// <summary>
    /// Visual indicator showing where a robot is heading and its current status
    /// Displays as 3D world visualization
    /// </summary>
    public partial class RobotTargetIndicator : Node3D
    {
        private MeshInstance3D _targetRing = null!;
        private MeshInstance3D _directionArrow = null!;
        private Label3D _statusLabel = null!;
        private Color _robotColor = Colors.White;

        public void Initialize(Color robotColor)
        {
            _robotColor = robotColor;

            // Create target ring (shows where robot is going)
            _targetRing = new MeshInstance3D
            {
                Mesh = CreateRingMesh(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var ringMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(robotColor.R, robotColor.G, robotColor.B, 0.6f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true
            };
            _targetRing.SetSurfaceOverrideMaterial(0, ringMat);
            AddChild(_targetRing);

            // Create direction arrow (points from robot to target)
            _directionArrow = new MeshInstance3D
            {
                Mesh = CreateArrowMesh(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var arrowMat = new StandardMaterial3D
            {
                AlbedoColor = robotColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true
            };
            _directionArrow.SetSurfaceOverrideMaterial(0, arrowMat);
            AddChild(_directionArrow);

            // Create status label
            _statusLabel = new Label3D
            {
                Text = "Idle",
                FontSize = 32,
                Modulate = robotColor,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(0, 1.5f, 0)
            };
            AddChild(_statusLabel);
        }

        public void UpdateTarget(Vector3 targetPos, Vector3 robotPos, string status)
        {
            // Position ring at target
            _targetRing.GlobalPosition = new Vector3(targetPos.X, 0.1f, targetPos.Z);

            // Animate ring (pulse effect)
            float pulse = 1f + Mathf.Sin((float)GD.Randf() * Mathf.Pi) * 0.1f;
            _targetRing.Scale = Vector3.One * pulse;

            // Position arrow from robot to target
            Vector3 midPoint = (robotPos + targetPos) / 2f;
            _directionArrow.GlobalPosition = new Vector3(midPoint.X, 0.5f, midPoint.Z);
            
            Vector3 direction = targetPos - robotPos;
            if (direction.LengthSquared() > 0.01f)
            {
                float angle = Mathf.Atan2(direction.Z, direction.X);
                _directionArrow.Rotation = new Vector3(0, angle - Mathf.Pi / 2f, 0);
                _directionArrow.Visible = true;
            }
            else
            {
                _directionArrow.Visible = false;
            }

            // Update status label
            _statusLabel.Text = status;
            _statusLabel.GlobalPosition = new Vector3(targetPos.X, 1.5f, targetPos.Z);
        }

        public new void SetVisible(bool visible)
        {
            _targetRing.Visible = visible;
            _directionArrow.Visible = visible;
            _statusLabel.Visible = visible;
        }

        private Mesh CreateRingMesh()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            float innerRadius = 0.4f;
            float outerRadius = 0.6f;
            int segments = 32;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * Mathf.Tau;
                float angle2 = (float)(i + 1) / segments * Mathf.Tau;

                Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * innerRadius, 0, Mathf.Sin(angle1) * innerRadius);
                Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * outerRadius, 0, Mathf.Sin(angle1) * outerRadius);
                Vector3 inner2 = new Vector3(Mathf.Cos(angle2) * innerRadius, 0, Mathf.Sin(angle2) * innerRadius);
                Vector3 outer2 = new Vector3(Mathf.Cos(angle2) * outerRadius, 0, Mathf.Sin(angle2) * outerRadius);

                st.SetNormal(Vector3.Up);
                st.AddVertex(inner1);
                st.AddVertex(outer1);
                st.AddVertex(inner2);

                st.AddVertex(inner2);
                st.AddVertex(outer1);
                st.AddVertex(outer2);
            }

            st.Index();
            return st.Commit();
        }

        private Mesh CreateArrowMesh()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            // Arrow shaft
            float width = 0.1f;
            float length = 0.8f;
            
            st.SetNormal(Vector3.Up);
            st.AddVertex(new Vector3(-width, 0, 0));
            st.AddVertex(new Vector3(width, 0, 0));
            st.AddVertex(new Vector3(-width, 0, length));

            st.AddVertex(new Vector3(-width, 0, length));
            st.AddVertex(new Vector3(width, 0, 0));
            st.AddVertex(new Vector3(width, 0, length));

            // Arrow head
            float headWidth = 0.3f;
            float headLength = 0.3f;
            
            st.AddVertex(new Vector3(-headWidth, 0, length));
            st.AddVertex(new Vector3(headWidth, 0, length));
            st.AddVertex(new Vector3(0, 0, length + headLength));

            st.Index();
            return st.Commit();
        }
    }
}
