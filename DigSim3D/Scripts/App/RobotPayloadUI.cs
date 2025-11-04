using Godot;
using System.Collections.Generic;

namespace DigSim3D.App
{
    /// <summary>
    /// UI panel showing robot payloads and status
    /// Positioned in LEFT SIDE with scrollable panels
    /// </summary>
    public partial class RobotPayloadUI : Control
    {
        private VBoxContainer _container = null!;
        private Dictionary<int, RobotPayloadPanel> _panels = new();
        private Label _heatMapStatusLabel = null!;
        private Label _extractedDirtLabel = null!;
        private ScrollContainer _scrollContainer = null!;

        public override void _Ready()
        {
            // Create main panel - BOTTOM-LEFT CORNER (minimal, compact)
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                CustomMinimumSize = new Vector2(280, 120),
                Position = new Vector2(10, GetViewport().GetVisibleRect().Size.Y - 140)
            };
            AddChild(panel);

            // Create VBox for content
            _container = new VBoxContainer
            {
                OffsetLeft = 8,
                OffsetTop = 8,
                OffsetRight = -8,
                OffsetBottom = -8
            };
            _container.AddThemeConstantOverride("separation", 3);
            panel.AddChild(_container);

            // Title label
            var titleLabel = new Label
            {
                Text = "📦 EXCAVATION STATUS",
                CustomMinimumSize = new Vector2(0, 16)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 10);
            _container.AddChild(titleLabel);

            // Heat map status label
            _heatMapStatusLabel = new Label
            {
                Text = "Heat Map: OFF",
                CustomMinimumSize = new Vector2(0, 14)
            };
            _heatMapStatusLabel.AddThemeFontSizeOverride("font_size", 9);
            _container.AddChild(_heatMapStatusLabel);

            // Extracted dirt label
            _extractedDirtLabel = new Label
            {
                Text = "Extracted: 0.00 m³",
                CustomMinimumSize = new Vector2(0, 14)
            };
            _extractedDirtLabel.AddThemeFontSizeOverride("font_size", 9);
            _container.AddChild(_extractedDirtLabel);

            GD.Print("[RobotPayloadUI] ✅ Initialized at bottom-left");
        }

        /// <summary>
        /// Update extracted dirt display
        /// </summary>
        public void UpdateExtractedDirt(float extractedVolume)
        {
            _extractedDirtLabel.Text = $"Extracted: {extractedVolume:F2} m³";
        }

        /// <summary>
        /// Update heatmap status
        /// </summary>
        public void UpdateHeatMapStatus(bool enabled)
        {
            _heatMapStatusLabel.Text = $"Heat Map: {(enabled ? "ON" : "OFF")}";
        }

        /// <summary>
        /// Add a robot to the UI
        /// </summary>
        public void AddRobot(int robotId, string name, Color color)
        {
            var robotPanel = new RobotPayloadPanel(robotId, name, color);
            _container.AddChild(robotPanel);
            _panels[robotId] = robotPanel;
        }

        /// <summary>
        /// Update robot payload and status
        /// </summary>
        public void UpdatePayload(int robotId, float percent, string status, Vector3 position)
        {
            if (_panels.TryGetValue(robotId, out var panel))
            {
                panel.UpdatePayload(percent, status, position);
            }
        }
    }

    /// <summary>
    /// Individual robot payload panel
    /// </summary>
    public partial class RobotPayloadPanel : PanelContainer
    {
        private int _robotId = 0;
        private ProgressBar _payloadBar = null!;
        private Label _statusLabel = null!;
        private Label _payloadLabel = null!;
        private Label _positionLabel = null!;
        private Color _robotColor = Colors.White;

        public RobotPayloadPanel(int robotId, string name, Color color)
        {
            _robotId = robotId;
            _robotColor = color;
            CustomMinimumSize = new Vector2(320, 100);

            var vbox = new VBoxContainer
            {
                OffsetLeft = 8,
                OffsetTop = 8,
                OffsetRight = -8,
                OffsetBottom = -8
            };
            vbox.AddThemeConstantOverride("separation", 4);
            AddChild(vbox);

            // Robot name with color
            var nameLabel = new Label
            {
                Text = $"[{name}]",
                Modulate = color,
                CustomMinimumSize = new Vector2(0, 18)
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(nameLabel);

            // Payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(0, 18)
            };
            vbox.AddChild(_payloadBar);

            // Payload label
            _payloadLabel = new Label
            {
                Text = "Payload: 0.0/2.0 m³",
                CustomMinimumSize = new Vector2(0, 14)
            };
            _payloadLabel.AddThemeFontSizeOverride("font_size", 9);
            vbox.AddChild(_payloadLabel);

            // Status label
            _statusLabel = new Label
            {
                Text = "Status: Idle",
                CustomMinimumSize = new Vector2(0, 14)
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 9);
            vbox.AddChild(_statusLabel);

            // Position label
            _positionLabel = new Label
            {
                Text = "Pos: (0.0, 0.0)",
                CustomMinimumSize = new Vector2(0, 14)
            };
            _positionLabel.AddThemeFontSizeOverride("font_size", 9);
            vbox.AddChild(_positionLabel);
        }

        public void UpdatePayload(float percent, string status, Vector3 position)
        {
            _payloadBar.Value = percent * 100f;
            _statusLabel.Text = $"Status: {status}";
            _positionLabel.Text = $"Pos: ({position.X:F1}, {position.Z:F1})";
        }
    }
}
