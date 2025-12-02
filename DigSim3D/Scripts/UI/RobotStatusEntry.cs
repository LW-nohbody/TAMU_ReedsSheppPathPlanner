using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Agent status monitoring panel with payload and position tracking
    /// </summary>
    public partial class RobotStatusEntry : PanelContainer
    {
        private string _robotId;
        private Label _nameLabel = null!;
        private ProgressBar _payloadBar = null!;
        private Label _statusLabel = null!;
        private Color _robotColor;
        private MiniChart _chart = null!;

        public RobotStatusEntry(string id, Color color)
        {
            _robotId = id;
            _robotColor = color;
            CustomMinimumSize = new Vector2(380, 85);
            MouseFilter = MouseFilterEnum.Stop;
            
            // Dark theme panel with subtle border
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.16f, 0.17f, 0.19f, 1.0f); // Slightly lighter than main panel
            styleBox.BorderColor = new Color(0.25f, 0.27f, 0.30f, 1.0f); // Subtle border
            styleBox.SetBorderWidthAll(1);
            styleBox.SetCornerRadiusAll(5);
            styleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
            styleBox.ShadowSize = 4;
            styleBox.ShadowOffset = new Vector2(0, 2);
            AddThemeStyleboxOverride("panel", styleBox);
        }

        public override void _Ready()
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            AddChild(margin);
            
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 3);
            margin.AddChild(vbox);
            
            // Name label - orange accent
            _nameLabel = new Label
            {
                Text = $"Agent {_robotId}",
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 12);
            _nameLabel.AddThemeColorOverride("font_color", _robotColor); // Neon orange accent
            vbox.AddChild(_nameLabel);
            
            // Payload bar - dark theme
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 95,
                Value = 0,
                CustomMinimumSize = new Vector2(350, 16),
                ShowPercentage = false
            };
            
            var barStyleBox = new StyleBoxFlat();
            barStyleBox.BgColor = new Color(0.10f, 0.11f, 0.13f, 1.0f); // Very dark background
            barStyleBox.SetCornerRadiusAll(3);
            barStyleBox.BorderColor = new Color(0.22f, 0.24f, 0.27f, 1.0f);
            barStyleBox.SetBorderWidthAll(1);
            _payloadBar.AddThemeStyleboxOverride("background", barStyleBox);
            
            var barFillStyleBox = new StyleBoxFlat();
            barFillStyleBox.BgColor = _robotColor; // Keep robot's unique color
            barFillStyleBox.SetCornerRadiusAll(3);
            _payloadBar.AddThemeStyleboxOverride("fill", barFillStyleBox);
            
            vbox.AddChild(_payloadBar);
            
            // Status label - light gray
            _statusLabel = new Label
            {
                Text = "State: Idle | Position: (0.0, 0.0)"
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 9);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.72f, 1.0f)); // Light gray text
            vbox.AddChild(_statusLabel);
            
            // Mini chart
            _chart = new MiniChart();
            vbox.AddChild(_chart);
        }

        public void UpdatePayload(float payloadPercent, string status, Vector3 position)
        {
            if (_payloadBar != null)
            {
                _payloadBar.Value = payloadPercent * 100f;
            }
            
            if (_statusLabel != null)
            {
                _statusLabel.Text = $"State: {status} | Position: ({position.X:F1}, {position.Z:F1})";
            }
            
            if (_chart != null)
            {
                _chart.AddDataPoint(payloadPercent);
            }
        }

        public void UpdateRobotName(string name)
        {
            if (_nameLabel != null)
            {
                _nameLabel.Text = name;
            } else
            {
                GD.PushError("[RobotStatusEntry] Name label is not assigned.");
            }
        }
    }
}
