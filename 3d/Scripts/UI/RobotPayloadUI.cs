using Godot;
using System.Collections.Generic;

namespace SimCore.UI
{
    /// <summary>
    /// UI panel showing robot payloads and status
    /// </summary>
    public partial class RobotPayloadUI : Control
    {
        private VBoxContainer _container = null!;
        private Dictionary<int, RobotPayloadPanel> _panels = new();
        private Label _heatMapStatusLabel = null!;
        private Label _remainingDirtLabel = null!;

        public override void _Ready()
        {
            // Create main panel
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(300, 0)
            };
            AddChild(panel);

            // Create container for robot panels
            _container = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            panel.AddChild(_container);

            // Create remaining dirt label
            _remainingDirtLabel = new Label
            {
                Text = "Remaining Dirt: 0.00 m³",
                CustomMinimumSize = new Vector2(280, 30)
            };
            _container.AddChild(_remainingDirtLabel);

            // Create heat map status label
            _heatMapStatusLabel = new Label
            {
                Text = "Heat Map: OFF",
                CustomMinimumSize = new Vector2(280, 40)
            };
            _container.AddChild(_heatMapStatusLabel);

            // Add separator
            var separator = new HSeparator();
            _container.AddChild(separator);
        }

        /// <summary>
        /// Update remaining dirt display
        /// </summary>
        public void UpdateRemainingDirt(float remainingVolume)
        {
            _remainingDirtLabel.Text = $"Remaining Dirt: {remainingVolume:F2} m³";
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
        /// Update robot payload
        /// </summary>
        public void UpdatePayload(int robotId, float percent, string status, Vector3 position)
        {
            if (_panels.TryGetValue(robotId, out var panel))
            {
                panel.UpdatePayload(percent, status, position);
            }
        }

        /// <summary>
        /// Update heat map status
        /// </summary>
        public void UpdateHeatMapStatus(bool enabled)
        {
            _heatMapStatusLabel.Text = $"Heat Map: {(enabled ? "ON" : "OFF")}";
        }
    }

    /// <summary>
    /// Individual robot panel
    /// </summary>
    public partial class RobotPayloadPanel : PanelContainer
    {
        private int _robotId;
        private ProgressBar _payloadBar = null!;
        private Label _statusLabel = null!;
        private Label _payloadLabel = null!;
        private Color _robotColor;

        public RobotPayloadPanel(int robotId, string name, Color color)
        {
            _robotId = robotId;
            _robotColor = color;
            CustomMinimumSize = new Vector2(280, 80);

            var vbox = new VBoxContainer();
            AddChild(vbox);

            // Robot name with color
            var nameLabel = new Label
            {
                Text = $"[{name}]",
                Modulate = color
            };
            vbox.AddChild(nameLabel);

            // Payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(260, 20)
            };
            vbox.AddChild(_payloadBar);

            // Payload label
            _payloadLabel = new Label
            {
                Text = "Payload: 0.0/0.5 m³"
            };
            vbox.AddChild(_payloadLabel);

            // Status label
            _statusLabel = new Label
            {
                Text = "Status: Idle"
            };
            vbox.AddChild(_statusLabel);
        }

        public void UpdatePayload(float percent, string status, Vector3 position)
        {
            _payloadBar.Value = percent;
            _statusLabel.Text = $"Status: {status}";
            _payloadLabel.Text = $"Payload: {(percent * 0.5f / 100f):F2}/0.5 m³";
        }
    }
}
