using Godot;
using System;

namespace SimCore.UI
{
    /// <summary>
    /// Simple UI panel for adjusting dig depth and robot speed
    /// Positioned in top-right corner, fully visible and functional
    /// </summary>
    public partial class SimulationSettingsUI : CanvasLayer
    {
        private Control _panel = null!;
        private Label _digDepthValueLabel = null!;
        private Label _robotSpeedValueLabel = null!;
        private HSlider _digDepthSlider = null!;
        private HSlider _robotSpeedSlider = null!;

        public override void _Ready()
        {
            GD.Print("[SimulationSettingsUI] Initializing UI panel...");

            // Main panel container - positioned in top-right
            _panel = new Control
            {
                CustomMinimumSize = new Vector2(350, 280),
                AnchorLeft = 1.0f,
                AnchorTop = 0.0f,
                AnchorRight = 1.0f,
                AnchorBottom = 0.0f,
                OffsetLeft = -360,
                OffsetTop = 10,
                OffsetRight = -10,
                OffsetBottom = 290
            };
            AddChild(_panel);

            // Background panel
            var bgPanel = new Panel();
            bgPanel.AnchorLeft = 0;
            bgPanel.AnchorTop = 0;
            bgPanel.AnchorRight = 1;
            bgPanel.AnchorBottom = 1;
            _panel.AddChild(bgPanel);

            // VBox for spacing and layout
            var vbox = new VBoxContainer
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 12,
                OffsetTop = 12,
                OffsetRight = -12,
                OffsetBottom = -12
            };
            vbox.AddThemeConstantOverride("separation", 10);
            _panel.AddChild(vbox);

            // ===== TITLE =====
            var titleLabel = new Label
            {
                Text = "⚙️  SIMULATION SETTINGS",
                CustomMinimumSize = new Vector2(0, 28)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(titleLabel);

            // ===== DIG DEPTH SECTION =====
            var digLabel = new Label
            {
                Text = "DIG DEPTH (meters):",
                CustomMinimumSize = new Vector2(0, 20)
            };
            digLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(digLabel);

            _digDepthSlider = new HSlider
            {
                MinValue = 0.02,
                MaxValue = 0.20,
                Value = SimCore.Core.SimpleDigLogic.DIG_AMOUNT,
                Step = 0.01,
                CustomMinimumSize = new Vector2(0, 24)
            };
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            vbox.AddChild(_digDepthSlider);

            _digDepthValueLabel = new Label
            {
                Text = $"→ {SimCore.Core.SimpleDigLogic.DIG_AMOUNT:F3}m",
                CustomMinimumSize = new Vector2(0, 18)
            };
            _digDepthValueLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_digDepthValueLabel);

            // Spacer
            vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

            // ===== ROBOT SPEED SECTION =====
            var speedLabel = new Label
            {
                Text = "ROBOT SPEED:",
                CustomMinimumSize = new Vector2(0, 20)
            };
            speedLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(speedLabel);

            _robotSpeedSlider = new HSlider
            {
                MinValue = 0.5,
                MaxValue = 3.0,
                Value = VehicleAgent3D.GlobalSpeedMultiplier,
                Step = 0.1,
                CustomMinimumSize = new Vector2(0, 24)
            };
            _robotSpeedSlider.ValueChanged += OnRobotSpeedChanged;
            vbox.AddChild(_robotSpeedSlider);

            _robotSpeedValueLabel = new Label
            {
                Text = $"→ {VehicleAgent3D.GlobalSpeedMultiplier:F1}x",
                CustomMinimumSize = new Vector2(0, 18)
            };
            _robotSpeedValueLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_robotSpeedValueLabel);

            GD.Print("[SimulationSettingsUI] ✅ UI panel created successfully!");
            GD.Print($"[SimulationSettingsUI] Dig depth: {SimCore.Core.SimpleDigLogic.DIG_AMOUNT:F3}m");
            GD.Print($"[SimulationSettingsUI] Robot speed: {VehicleAgent3D.GlobalSpeedMultiplier:F1}x");
        }

        private void OnDigDepthChanged(double value)
        {
            float digDepth = (float)value;
            SimCore.Core.SimpleDigLogic.DIG_AMOUNT = digDepth;
            _digDepthValueLabel.Text = $"→ {digDepth:F3}m";
            GD.Print($"[⛏️ Settings] Dig depth: {digDepth:F3}m");
        }

        private void OnRobotSpeedChanged(double value)
        {
            float speedMultiplier = (float)value;
            VehicleAgent3D.GlobalSpeedMultiplier = speedMultiplier;
            _robotSpeedValueLabel.Text = $"→ {speedMultiplier:F1}x";
            GD.Print($"[🚗 Settings] Robot speed: {speedMultiplier:F1}x");
        }
    }
}
