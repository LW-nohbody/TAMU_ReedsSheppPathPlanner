using Godot;
using System;
using System.Collections.Generic;

namespace SimCore.UI
{
    /// <summary>
    /// Simple settings panel for adjusting dig depth and robot speed in real-time
    /// </summary>
    public partial class SimulationSettingsPanel : Control
    {
        private HSlider _digDepthSlider = null!;
        private HSlider _speedSlider = null!;
        private Label _digDepthLabel = null!;
        private Label _speedLabel = null!;

        public override void _Ready()
        {
            GD.Print("[SimulationSettingsPanel] Creating UI...");

            // Setup this Control as the container
            AnchorLeft = 1.0f;
            AnchorTop = 0.0f;
            AnchorRight = 1.0f;
            AnchorBottom = 0.0f;
            OffsetLeft = -330;
            OffsetTop = 10;
            OffsetRight = -10;
            OffsetBottom = 260;
            CustomMinimumSize = new Vector2(320, 250);

            // Background panel
            var bgPanel = new Panel();
            bgPanel.AnchorLeft = 0;
            bgPanel.AnchorTop = 0;
            bgPanel.AnchorRight = 1;
            bgPanel.AnchorBottom = 1;
            AddChild(bgPanel);

            // VBox for spacing and layout
            var vbox = new VBoxContainer
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1
            };
            vbox.AddThemeConstantOverride("separation", 10);
            AddChild(vbox);

            // Title
            var titleLabel = new Label { Text = "⚙️ SIMULATION SETTINGS" };
            vbox.AddChild(titleLabel);

            // Separator
            vbox.AddChild(new HSeparator());

            // Dig Depth Section
            vbox.AddChild(new Label { Text = "Dig Depth per Operation:" });

            _digDepthSlider = new HSlider
            {
                MinValue = 0.02f,
                MaxValue = 0.20f,
                Value = SimCore.Core.SimpleDigLogic.DIG_AMOUNT,
                Step = 0.01f,
                CustomMinimumSize = new Vector2(300, 30)
            };
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            vbox.AddChild(_digDepthSlider);

            _digDepthLabel = new Label
            {
                Text = $"Current: {SimCore.Core.SimpleDigLogic.DIG_AMOUNT:F3}m"
            };
            vbox.AddChild(_digDepthLabel);

            // Separator
            vbox.AddChild(new HSeparator());

            // Speed Section
            vbox.AddChild(new Label { Text = "Robot Speed Multiplier:" });

            _speedSlider = new HSlider
            {
                MinValue = 0.5f,
                MaxValue = 3.0f,
                Value = VehicleAgent3D.GlobalSpeedMultiplier,
                Step = 0.1f,
                CustomMinimumSize = new Vector2(300, 30)
            };
            _speedSlider.ValueChanged += OnSpeedChanged;
            vbox.AddChild(_speedSlider);

            _speedLabel = new Label
            {
                Text = $"Current: {VehicleAgent3D.GlobalSpeedMultiplier:F1}x"
            };
            vbox.AddChild(_speedLabel);

            GD.Print("[SimulationSettingsPanel] UI created successfully!");
        }

        private void OnDigDepthChanged(double value)
        {
            float digDepth = (float)value;
            SimCore.Core.SimpleDigLogic.DIG_AMOUNT = digDepth;
            _digDepthLabel.Text = $"Current: {digDepth:F3}m";
            GD.Print($"[Settings] Dig depth: {digDepth:F3}m");
        }

        private void OnSpeedChanged(double value)
        {
            float speed = (float)value;
            VehicleAgent3D.GlobalSpeedMultiplier = speed;
            _speedLabel.Text = $"Current: {speed:F1}x";
            GD.Print($"[Settings] Robot speed: {speed:F1}x");
        }
    }
}
