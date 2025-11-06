using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium robot status panel with mini chart
    /// </summary>
    public partial class PremiumRobotStatusEntry : PanelContainer
    {
        private int _robotId;
        private Label _nameLabel = null!;
        private ProgressBar _payloadBar = null!;
        private Label _statusLabel = null!;
        private Color _robotColor;
        private MiniChart _chart = null!;

        public PremiumRobotStatusEntry(int id, string name, Color color)
        {
            _robotId = id;
            _robotColor = color;
            CustomMinimumSize = new Vector2(380, 120);
            MouseFilter = MouseFilterEnum.Stop;
            
            // Style with glassmorphism
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            styleBox.BorderColor = color;
            styleBox.SetBorderWidthAll(2);
            styleBox.SetCornerRadiusAll(8);
            styleBox.ShadowColor = new Color(color.R, color.G, color.B, 0.3f);
            styleBox.ShadowSize = 4;
            AddThemeStyleboxOverride("panel", styleBox);
        }

        public override void _Ready()
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            AddChild(vbox);
            
            // Name with icon
            _nameLabel = new Label
            {
                Text = $"🤖 Robot {_robotId}",
                Modulate = _robotColor
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 14);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(_nameLabel);
            
            // Payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(350, 20),
                ShowPercentage = false
            };
            
            var barStyleBox = new StyleBoxFlat();
            barStyleBox.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.6f);
            barStyleBox.SetCornerRadiusAll(4);
            _payloadBar.AddThemeStyleboxOverride("background", barStyleBox);
            
            var barFillStyleBox = new StyleBoxFlat();
            barFillStyleBox.BgColor = _robotColor;
            barFillStyleBox.SetCornerRadiusAll(4);
            _payloadBar.AddThemeStyleboxOverride("fill", barFillStyleBox);
            
            vbox.AddChild(_payloadBar);
            
            // Status
            _statusLabel = new Label
            {
                Text = "Status: Idle | (0.0, 0.0)",
                Modulate = Colors.White
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 10);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
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
                _statusLabel.Text = $"{status} | ({position.X:F1}, {position.Z:F1})";
            }
            
            if (_chart != null)
            {
                _chart.AddDataPoint(payloadPercent);
            }
        }
    }
}
