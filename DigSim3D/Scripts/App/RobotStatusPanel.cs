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
        private const int MAX_ROBOTS_TO_SHOW = 8;

        public override void _Ready()
        {
            // Create main panel
            _panel = new PanelContainer
            {
                Position = new Vector2(10, 80),
                CustomMinimumSize = new Vector2(350, 400),
                Modulate = new Color(1, 1, 1, 0.9f)
            };

            // Add theme
            var theme = new Theme();
            var font = ThemeDB.FallbackFont;
            theme.SetFontSize("font_size", "Label", 12);
            _panel.Theme = theme;

            // Create container for robot labels
            _vbox = new VBoxContainer
            {
                Position = new Vector2(5, 5)
            };
            _panel.AddChild(_vbox);

            // Create labels for each robot
            for (int i = 0; i < MAX_ROBOTS_TO_SHOW; i++)
            {
                var label = new Label
                {
                    Text = $"Robot {i}: Initializing...",
                    CustomMinimumSize = new Vector2(340, 40),
                    Modulate = new Color(1, 1, 1, 0.95f)
                };
                label.Theme = theme;
                label.AddThemeColorOverride("font_shadow_color", Colors.Black);
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);
                _vbox.AddChild(label);
                _robotLabels.Add(label);
            }

            AddChild(_panel);
            GD.Print("[RobotStatusPanel] ✅ Robot Status Panel initialized");
        }

        public void UpdateRobotStatus(int robotId, string status, float payload, int digsCompleted, Vector3 position, float totalDug)
        {
            if (robotId < 0 || robotId >= _robotLabels.Count) return;

            var label = _robotLabels[robotId];
            label.Text = $@"[{robotId}] {status}
  Payload: {payload:F2}m³ | Digs: {digsCompleted} | Total: {totalDug:F2}m³
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
