using Godot;

namespace DigSim3D.App
{
    /// <summary>
    /// Simple HUD overlay showing keyboard controls
    /// </summary>
    public partial class SimulationHUD : CanvasLayer
    {
        private Label _controlsLabel = null!;
        private Label _statsLabel = null!;
        private bool _visible = true;

        public override void _Ready()
        {
            // Create controls label (top-left, small)
            _controlsLabel = new Label
            {
                Position = new Vector2(10, 10),
                Text = GetControlsText(),
                Modulate = new Color(1, 1, 1, 0.85f)
            };
            
            // Style the label
            var theme = new Theme();
            var font = ThemeDB.FallbackFont;
            theme.SetFontSize("font_size", "Label", 13);
            _controlsLabel.Theme = theme;
            _controlsLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _controlsLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _controlsLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            
            AddChild(_controlsLabel);
            
            // Create stats label (top-right, small)
            _statsLabel = new Label
            {
                Position = new Vector2(10, 10),
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = new Color(1, 1, 1, 0.85f)
            };
            _statsLabel.Theme = theme;
            _statsLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _statsLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _statsLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            
            AddChild(_statsLabel);
        }

        public override void _Process(double delta)
        {
            // Toggle HUD visibility with F1
            if (Input.IsActionJustPressed("ui_cancel"))
            {
                _visible = !_visible;
                _controlsLabel.Visible = _visible;
                _statsLabel.Visible = _visible;
            }
            
            // Update stats position (keep it in top-right)
            var viewportSize = GetViewport().GetVisibleRect().Size;
            _statsLabel.Position = new Vector2(viewportSize.X - 350, 10);
        }

        public void UpdateStats(int vehicleCount, float totalDirt, bool heatMapOn, bool pathsOn, bool plannedPathsOn)
        {
            _statsLabel.Text = $@"Vehicles: {vehicleCount}
Dirt Extracted: {totalDirt:F1}m³
Heat Map: {(heatMapOn ? "ON" : "OFF")}
Traveled Paths: {(pathsOn ? "ON" : "OFF")}
Planned Paths: {(plannedPathsOn ? "ON" : "OFF")}";
        }

        private string GetControlsText()
        {
            return @"=== CONTROLS ===
H - Toggle Heat Map
P - Toggle Traveled Paths
L - Toggle Planned Paths
C - Clear Traveled Paths
F1 - Toggle HUD

Camera Controls:
TAB - Toggle Camera
Right Mouse - Rotate
Middle Mouse - Pan
Scroll - Zoom";
        }
    }
}
