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
            CustomMinimumSize = new Vector2(0, 80); // Don't set fixed width
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;
            
            // Professional, academic styling
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.16f, 0.18f, 0.22f, 0.94f);
            styleBox.BorderColor = new Color(color.R * 0.7f, color.G * 0.7f, color.B * 0.7f, 0.8f);
            styleBox.SetBorderWidthAll(1);
            styleBox.SetCornerRadiusAll(6);
            styleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.4f);
            styleBox.ShadowSize = 4;
            styleBox.ShadowOffset = new Vector2(0, 2);
            AddThemeStyleboxOverride("panel", styleBox);
        }

        public override void _Ready()
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            AddChild(margin);
            
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            margin.AddChild(vbox);
            
            // Professional name label
            _nameLabel = new Label
            {
                Text = $"Unit {_robotId}",
                Modulate = new Color(0.85f, 0.88f, 0.92f)
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 12);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(_nameLabel);
            
            // Professional payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(380, 16),
                ShowPercentage = false
            };
            
            var barStyleBox = new StyleBoxFlat();
            barStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.9f);
            barStyleBox.BorderColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
            barStyleBox.SetBorderWidthAll(1);
            barStyleBox.SetCornerRadiusAll(4);
            _payloadBar.AddThemeStyleboxOverride("background", barStyleBox);
            
            var barFillStyleBox = new StyleBoxFlat();
            barFillStyleBox.BgColor = new Color(_robotColor.R * 0.8f, _robotColor.G * 0.8f, _robotColor.B * 0.8f, 1.0f);
            barFillStyleBox.SetCornerRadiusAll(3);
            _payloadBar.AddThemeStyleboxOverride("fill", barFillStyleBox);
            
            vbox.AddChild(_payloadBar);
            
            // Professional status label
            _statusLabel = new Label
            {
                Text = "Status: Standby | Pos: (0.0, 0.0)",
                Modulate = Colors.White
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 10);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.73f, 0.80f));
            vbox.AddChild(_statusLabel);
        }

        public void UpdatePayload(float payloadPercent, string status, Vector3 position)
        {
            if (_payloadBar != null)
            {
                _payloadBar.Value = payloadPercent * 100f;
            }
            
            if (_statusLabel != null)
            {
                // Professional status formatting (no emojis)
                string statusText = status switch
                {
                    "ToDigSite" => "En Route to Site",
                    "Digging" => "Excavating",
                    "ToDumpSite" => "Transporting",
                    "Dumping" => "Unloading",
                    _ => "Standby"
                };
                
                _statusLabel.Text = $"{statusText} | Pos: ({position.X:F1}, {position.Z:F1}) | Load: {payloadPercent:P0}";
            }
        }
    }
}
