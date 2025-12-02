using Godot;
using System;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// Real-time robot status panel showing individual robot information
    /// Displays: Robot ID, Status, Payload, Digs Completed, Position
    /// </summary>
    public partial class RobotStatusPanel : CanvasLayer
    {
        private PanelContainer _panel = null!;
        private VBoxContainer _vbox = null!;
        private List<Label> _robotLabels = new();

        public override void _Ready()
        {
            // Create main panel - BOTTOM-RIGHT area (so it doesn't overlap with left panels)
            var viewportSize = GetViewport().GetVisibleRect().Size;
            _panel = new PanelContainer
            {
                Position = new Vector2(viewportSize.X - 370, viewportSize.Y - 420),
                CustomMinimumSize = new Vector2(360, 410),
                Modulate = new Color(1, 1, 1, 0.9f)
            };

            // Add theme
            var theme = new Theme();
            var font = ThemeDB.FallbackFont;
            theme.SetFontSize("font_size", "Label", 10);
            _panel.Theme = theme;

            // Create container for robot labels
            _vbox = new VBoxContainer
            {
                OffsetLeft = 5,
                OffsetTop = 5,
                OffsetRight = -5,
                OffsetBottom = -5
            };
            _vbox.AddThemeConstantOverride("separation", 2);
            _panel.AddChild(_vbox);

            // Title
            var title = new Label
            {
                Text = "🤖 ROBOT STATUS (Press I to toggle)",
                CustomMinimumSize = new Vector2(340, 22)
            };
            title.AddThemeFontSizeOverride("font_size", 11);
            _vbox.AddChild(title);

            // Create labels for each robot (show first 5)
            for (int i = 0; i < 5; i++)
            {
                var label = new Label
                {
                    Text = $"Robot {i}: Ready",
                    CustomMinimumSize = new Vector2(340, 64),
                    AutowrapMode = TextServer.AutowrapMode.Word
                };
                label.Theme = theme;
                label.AddThemeColorOverride("font_shadow_color", Colors.Black);
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);
                _vbox.AddChild(label);
                _robotLabels.Add(label);
            }

            AddChild(_panel);
            GD.Print("[RobotStatusPanel] ✅ Positioned at BOTTOM-RIGHT (Press I to toggle)");
        }

        public void UpdateRobotStatus(int robotId, string status, float payload, int digsCompleted, Vector3 position, float totalDug)
        {
            if (robotId < 0 || robotId >= _robotLabels.Count) return;

            var label = _robotLabels[robotId];
            label.Text = $@"[{robotId}] {status}
  Payload: {payload:F1}m³ | Digs: {digsCompleted} | Total: {totalDug:F1}m³
  Pos: ({position.X:F1}, {position.Z:F1})";

            // Color code based on status
            Color textColor = Colors.White;
            if (status.Contains("FULL"))
                textColor = Colors.OrangeRed;
            else if (status.Contains("Dumping"))
                textColor = Colors.LimeGreen;
            else if (status.Contains("Digging"))
                textColor = Colors.Cyan;
            else if (status.Contains("Waiting"))
                textColor = Colors.Yellow;
            else if (status.Contains("Error"))
                textColor = Colors.Red;

            label.AddThemeColorOverride("font_color", textColor);
        }

        public override void _Process(double delta)
        {
            // Toggle visibility with 'I' key
            if (Input.IsKeyPressed(Key.I))
            {
                _panel.Visible = !_panel.Visible;
            }
        }
    }
}
