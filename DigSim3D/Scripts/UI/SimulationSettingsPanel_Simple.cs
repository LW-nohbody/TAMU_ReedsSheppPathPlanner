using Godot;
using System;
using System.Collections.Generic;
using DigSim3D.Domain;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Simple, working settings panel for DigSim3D
    /// </summary>
    public partial class SimulationSettingsPanel_Simple : Control
    {
        private DigConfig _digConfig = null!;
        private List<VehicleVisualizer> _vehicles = new();
        
        private HSlider _speedSlider = null!;
        private Label _speedLabel = null!;
        private HSlider _digDepthSlider = null!;
        private Label _digDepthLabel = null!;
        private HSlider _digRadiusSlider = null!;
        private Label _digRadiusLabel = null!;
        
        public override void _Ready()
        {
            GD.Print("[SettingsPanel] Initializing simple settings panel...");
            
            // Make sure we can interact with this panel
            MouseFilter = MouseFilterEnum.Stop;
            
            // Position at top-right
            AnchorLeft = 1.0f;
            AnchorTop = 0.0f;
            AnchorRight = 1.0f;
            AnchorBottom = 0.0f;
            OffsetLeft = -330;
            OffsetTop = 15;
            OffsetRight = -15;
            OffsetBottom = 315; // 300px height
            
            // Background panel
            var panel = new Panel
            {
                CustomMinimumSize = new Vector2(300, 300),
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            styleBox.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f);
            styleBox.SetBorderWidthAll(3);
            styleBox.SetCornerRadiusAll(8);
            panel.AddThemeStyleboxOverride("panel", styleBox);
            AddChild(panel);
            
            // Container
            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            vbox.AddThemeConstantOverride("separation", 10);
            
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 15);
            margin.AddThemeConstantOverride("margin_right", 15);
            margin.AddThemeConstantOverride("margin_top", 15);
            margin.AddThemeConstantOverride("margin_bottom", 15);
            panel.AddChild(margin);
            margin.AddChild(vbox);
            
            // Title
            var title = new Label
            {
                Text = "⚙️ Settings",
                Modulate = Colors.White
            };
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(title);
            
            vbox.AddChild(new HSeparator());
            
            // Robot Speed
            AddSetting(vbox, "🚗 Robot Speed", 0.1f, 5.0f, 0.6f, out _speedSlider, out _speedLabel, "m/s");
            _speedSlider.ValueChanged += OnSpeedChanged;
            
            // Dig Depth  
            AddSetting(vbox, "⛏️ Dig Depth", 0.05f, 1.0f, 0.3f, out _digDepthSlider, out _digDepthLabel, "m");
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            
            // Dig Radius
            AddSetting(vbox, "📏 Dig Radius", 0.5f, 5.0f, 2.5f, out _digRadiusSlider, out _digRadiusLabel, "m");
            _digRadiusSlider.ValueChanged += OnDigRadiusChanged;
            
            GD.Print("[SettingsPanel] ✅ Simple settings panel ready!");
        }
        
        private void AddSetting(VBoxContainer parent, string labelText, float min, float max, float defaultValue,
            out HSlider slider, out Label valueLabel, string unit)
        {
            // Label
            var label = new Label
            {
                Text = labelText,
                Modulate = Colors.White
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", Colors.White);
            parent.AddChild(label);
            
            // Slider + Value in HBox
            var hbox = new HBoxContainer();
            parent.AddChild(hbox);
            
            slider = new HSlider
            {
                MinValue = min,
                MaxValue = max,
                Value = defaultValue,
                Step = (max - min) / 100f,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(180, 24),
                MouseFilter = MouseFilterEnum.Stop
            };
            hbox.AddChild(slider);
            
            valueLabel = new Label
            {
                Text = $"{defaultValue:F2} {unit}",
                CustomMinimumSize = new Vector2(70, 0),
                Modulate = Colors.White
            };
            valueLabel.AddThemeFontSizeOverride("font_size", 12);
            valueLabel.AddThemeColorOverride("font_color", Colors.LightGreen);
            hbox.AddChild(valueLabel);
        }
        
        private void OnSpeedChanged(double value)
        {
            float speed = (float)value;
            _speedLabel.Text = $"{speed:F2} m/s";
            
            // Update all vehicles
            foreach (var vehicle in _vehicles)
            {
                vehicle.SpeedMps = speed;
            }
            
            GD.Print($"[Settings] Robot speed changed to {speed:F2} m/s");
        }
        
        private void OnDigDepthChanged(double value)
        {
            float depth = (float)value;
            _digDepthLabel.Text = $"{depth:F2} m";
            
            if (_digConfig != null)
            {
                _digConfig.DigDepth = depth;
            }
            
            GD.Print($"[Settings] Dig depth changed to {depth:F2} m");
        }
        
        private void OnDigRadiusChanged(double value)
        {
            float radius = (float)value;
            _digRadiusLabel.Text = $"{radius:F2} m";
            
            if (_digConfig != null)
            {
                _digConfig.DigRadius = radius;
            }
            
            GD.Print($"[Settings] Dig radius changed to {radius:F2} m");
        }
        
        public void SetDigConfig(DigConfig config)
        {
            _digConfig = config;
            
            if (_digDepthSlider != null)
            {
                _digDepthSlider.Value = config.DigDepth;
            }
            if (_digRadiusSlider != null)
            {
                _digRadiusSlider.Value = config.DigRadius;
            }
        }
        
        public void SetVehicles(List<VehicleVisualizer> vehicles)
        {
            _vehicles = vehicles;
            
            if (_speedSlider != null && vehicles.Count > 0)
            {
                _speedSlider.Value = vehicles[0].SpeedMps;
            }
        }
    }
}
