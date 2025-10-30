using Godot;

namespace DigSim3D.Domain
{
    [Tool]
    public partial class CylinderObstacle : Node3D
    {
        [Export] public float Radius { get; set; } = 1.0f;
        [Export] public float Height { get; set; } = 2.0f;
        [Export] public Color DebugColor { get; set; } = new Color(1, 0, 0, 0.30f);

        private MeshInstance3D _editorMesh = null!;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                SetProcess(true);

            EnsureMesh();
        }

        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint()) return;
            EnsureMesh();
        }

        private void EnsureMesh()
        {
            if (_editorMesh == null)
                _editorMesh = GetNodeOrNull<MeshInstance3D>("EditorMesh");

            if (_editorMesh == null)
            {
                _editorMesh = new MeshInstance3D { Name = "EditorMesh" };
                AddChild(_editorMesh);

                // Make visible in editor
                var tree = GetTree();
                if (tree?.EditedSceneRoot != null)
                    _editorMesh.Owner = tree.EditedSceneRoot;
            }

            var mesh = new CylinderMesh
            {
                TopRadius = Radius,
                BottomRadius = Radius,
                Height = Height,
                RadialSegments = 32
            };

            var mat = new StandardMaterial3D
            {
                AlbedoColor = DebugColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            _editorMesh.Mesh = mesh;
            _editorMesh.MaterialOverride = mat;
            _editorMesh.Visible = true;
        }
    }
}