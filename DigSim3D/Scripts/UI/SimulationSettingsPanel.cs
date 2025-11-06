using Godot;
using System;
using System.Collections.Generic;
using DigSim3D.Domain;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Settings panel for adjusting simulation parameters in real-time
    /// </summary>
    public partial class SimulationSettingsPanel : Control
    {
        private DigConfig _digConfig = null!;
        private List<VehicleVisualizer> _vehicles = new();
        
        // UI Elements
        private VBoxContainer _container = null!;
        private HSlider _speedSlider = null!;
        private HSlider _digDepthSlider = null!;
        private HSlider _digRadiusSlider = null!;
        private HSlider _payloadSlider = null!;
        
        private Label _speedValueLabel = null!;
        private Label _digDepthValueLabel = null!;
        private Label _digRadiusValueLabel = null!;
        private Label _payloadValueLabel = null!;

        public override void _Ready()
        {
            GD.Print("[SettingsPanel] Initializing settings panel...");

            // CRITICAL: Ensure panel is fully interactive
            Visible = true;
            Modulate = Colors.White;
            MouseFilter = MouseFilterEnum.Stop; // Block mouse from passing through
            
            // Anchors already set in DigSimUIv2, but ensure minimum size
            CustomMinimumSize = new Vector2(320, 400);

            // Create main panel with dark background
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.92f);
            styleBox.BorderColor = new Color(0.3f, 0.7f, 0.4f, 1.0f); // Green border
            styleBox.SetBorderWidthAll(3);
            styleBox.SetCornerRadiusAll(8);
            panel.AddThemeStyleboxOverride("panel", styleBox);
            AddChild(panel);

            // Create container with margin
            var marginContainer = new MarginContainer();
            marginContainer.AddThemeConstantOverride("margin_left", 15);
            marginContainer.AddThemeConstantOverride("margin_right", 15);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            marginContainer.MouseFilter = MouseFilterEnum.Stop;
            panel.AddChild(marginContainer);

            _container = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore // Let events pass to children
            };
            _container.AddThemeConstantOverride("separation", 12);
            marginContainer.AddChild(_container);

            // Title
            var titleLabel = new Label
            {
                Text = "⚙️ Simulation Settings",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 18);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 0.4f));
            _container.AddChild(titleLabel);

            var separator1 = new HSeparator();
            _container.AddChild(separator1);

            // Speed setting
            AddSettingControl("🚗 Robot Speed (m/s)", 0.1f, 5.0f, 0.6f, 
                out _speedSlider, out _speedValueLabel, OnSpeedChanged);

            // Dig depth setting
            AddSettingControl("⛏️ Dig Depth (m)", 0.05f, 1.0f, 0.15f, 
                out _digDepthSlider, out _digDepthValueLabel, OnDigDepthChanged);

            // Dig radius setting
            AddSettingControl("📏 Dig Radius (m)", 0.2f, 3.0f, 0.65f, 
                out _digRadiusSlider, out _digRadiusValueLabel, OnDigRadiusChanged);

            // Payload setting
            AddSettingControl("🪣 Payload Capacity (m³)", 0.01f, 0.5f, 0.075f, 
                out _payloadSlider, out _payloadValueLabel, OnPayloadChanged);

            var separator2 = new HSeparator();
            _container.AddChild(separator2);

            // Reset button
            var resetButton = new Button
            {
                Text = "🔄 Reset to Defaults",
                CustomMinimumSize = new Vector2(280, 40),
                MouseFilter = MouseFilterEnum.Stop
            };
            resetButton.Pressed += OnResetPressed;
            _container.AddChild(resetButton);

            GD.Print("[SettingsPanel] ✅ Settings panel initialized!");
        }

        private void AddSettingControl(string labelText, float minValue, float maxValue, float defaultValue,
            out HSlider slider, out Label valueLabel, Action<double> onValueChanged)
        {
            // Label
            var label = new Label 
            { 
                Text = labelText,
                MouseFilter = MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(label);

            // Value display
            valueLabel = new Label 
            { 
                Text = defaultValue.ToString("F2"),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            valueLabel.AddThemeFontSizeOverride("font_size", 16);
            valueLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.5f));
            _container.AddChild(valueLabel);

            // Slider
            slider = new HSlider
            {
                MinValue = minValue,
                MaxValue = maxValue,
                Value = defaultValue,
                Step = (maxValue - minValue) / 100f,
                CustomMinimumSize = new Vector2(280, 32),
                MouseFilter = MouseFilterEnum.Stop, // CRITICAL: Ensure slider captures mouse
                FocusMode = FocusModeEnum.Click
            };
            
            // Connect value changed event
            slider.ValueChanged += onValueChanged;
            
            _container.AddChild(slider);

            // Add spacer
            var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
            _container.AddChild(spacer);
        }

        private void OnSpeedChanged(double value)
        {
            _speedValueLabel.Text = value.ToString("F2");
            
            // Update all vehicles
            foreach (var vehicle in _vehicles)
            {
                vehicle.SpeedMps = (float)value;
            }
            
            GD.Print($"[Settings] Robot speed changed to {value:F2} m/s");
        }

        private void OnDigDepthChanged(double value)
        {
            _digDepthValueLabel.Text = value.ToString("F2");
            
            if (_digConfig != null)
            {
                _digConfig.DigDepth = (float)value;
            }
            
            GD.Print($"[Settings] Dig depth changed to {value:F2} m");
        }

        private void OnDigRadiusChanged(double value)
        {
            _digRadiusValueLabel.Text = value.ToString("F2");
            
            if (_digConfig != null)
            {
                _digConfig.DigRadiusMeters = (float)value;
            }
            
            GD.Print($"[Settings] Dig radius changed to {value:F2} m");
        }

        private void OnPayloadChanged(double value)
        {
            _payloadValueLabel.Text = value.ToString("F3");
            
            if (_digConfig != null)
            {
                _digConfig.PayloadCapacityM3 = (float)value;
            }
            
            GD.Print($"[Settings] Payload capacity changed to {value:F3} m³");
        }

        private void OnResetPressed()
        {
            GD.Print("[Settings] Resetting to defaults");
            
            _speedSlider.Value = 0.6;
            _digDepthSlider.Value = 0.15;
            _digRadiusSlider.Value = 0.65;
            _payloadSlider.Value = 0.075;
        }

        public void SetDigConfig(DigConfig config)
        {
            _digConfig = config;
            
            if (_digDepthSlider != null)
            {
                _digDepthSlider.Value = config.DigDepth;
                _digRadiusSlider.Value = config.DigRadiusMeters;
                _payloadSlider.Value = config.PayloadCapacityM3;
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
