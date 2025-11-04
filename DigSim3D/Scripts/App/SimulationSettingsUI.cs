using Godot;
using System;
using DigSim3D.Config;

namespace DigSim3D.App
{
    /// <summary>
    /// Advanced UI panel for adjusting dig depth, max speed, and robot load capacity
    /// Positioned in top-right corner, fully visible and functional
    /// Provides real-time runtime control of simulation parameters
    /// </summary>
    public partial class SimulationSettingsUI : CanvasLayer
    {
        private Control _panel = null!;
        private Label _digDepthValueLabel = null!;
        private Label _maxSpeedValueLabel = null!;
        private Label _loadCapacityValueLabel = null!;
        private HSlider _digDepthSlider = null!;
        private HSlider _maxSpeedSlider = null!;
        private HSlider _loadCapacitySlider = null!;
        private SpinBox _digDepthMaxInput = null!;
        private SpinBox _maxSpeedMaxInput = null!;
        private SpinBox _loadCapacityMaxInput = null!;

        public override void _Ready()
        {
            GD.Print("[SimulationSettingsUI] Initializing UI panel...");

            // Main panel container - positioned in top-right
            _panel = new Control
            {
                CustomMinimumSize = new Vector2(420, 580),
                AnchorLeft = 1.0f,
                AnchorTop = 0.0f,
                AnchorRight = 1.0f,
                AnchorBottom = 0.0f,
                OffsetLeft = -430,
                OffsetTop = 10,
                OffsetRight = -10,
                OffsetBottom = 590
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
            vbox.AddThemeConstantOverride("separation", 8);
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
                CustomMinimumSize = new Vector2(0, 18)
            };
            digLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(digLabel);

            _digDepthSlider = new HSlider
            {
                MinValue = 0.02,
                MaxValue = 0.20,
                Value = SimulationConfig.MaxDigDepth,
                Step = 0.01,
                CustomMinimumSize = new Vector2(0, 20)
            };
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            vbox.AddChild(_digDepthSlider);

            _digDepthValueLabel = new Label
            {
                Text = $"→ {SimulationConfig.MaxDigDepth:F3}m",
                CustomMinimumSize = new Vector2(0, 16)
            };
            _digDepthValueLabel.AddThemeFontSizeOverride("font_size", 10);
            vbox.AddChild(_digDepthValueLabel);

            // Max value input for dig depth
            var digMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            var digMaxLabel = new Label { Text = "Max:", CustomMinimumSize = new Vector2(40, 22) };
            _digDepthMaxInput = new SpinBox
            {
                MinValue = 0.05,
                MaxValue = 0.50,
                Value = 0.20,
                Step = 0.01,
                CustomMinimumSize = new Vector2(60, 22)
            };
            _digDepthMaxInput.ValueChanged += OnDigDepthMaxChanged;
            digMaxHbox.AddChild(digMaxLabel);
            digMaxHbox.AddChild(_digDepthMaxInput);
            vbox.AddChild(digMaxHbox);

            // Spacer
            vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

            // ===== MAX SPEED SECTION =====
            var maxSpeedLabel = new Label
            {
                Text = "MAX ROBOT SPEED:",
                CustomMinimumSize = new Vector2(0, 18)
            };
            maxSpeedLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(maxSpeedLabel);

            _maxSpeedSlider = new HSlider
            {
                MinValue = 0.5,
                MaxValue = 4.0,
                Value = SimulationConfig.MaxRobotSpeed,
                Step = 0.1,
                CustomMinimumSize = new Vector2(0, 20)
            };
            _maxSpeedSlider.ValueChanged += OnMaxSpeedChanged;
            vbox.AddChild(_maxSpeedSlider);

            _maxSpeedValueLabel = new Label
            {
                Text = $"→ {SimulationConfig.MaxRobotSpeed:F1}x",
                CustomMinimumSize = new Vector2(0, 16)
            };
            _maxSpeedValueLabel.AddThemeFontSizeOverride("font_size", 10);
            vbox.AddChild(_maxSpeedValueLabel);

            // Max value input for speed
            var speedMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            var speedMaxLabel = new Label { Text = "Max:", CustomMinimumSize = new Vector2(40, 22) };
            _maxSpeedMaxInput = new SpinBox
            {
                MinValue = 1.0,
                MaxValue = 10.0,
                Value = 4.0,
                Step = 0.5,
                CustomMinimumSize = new Vector2(60, 22)
            };
            _maxSpeedMaxInput.ValueChanged += OnMaxSpeedMaxChanged;
            speedMaxHbox.AddChild(speedMaxLabel);
            speedMaxHbox.AddChild(_maxSpeedMaxInput);
            vbox.AddChild(speedMaxHbox);

            // Spacer
            vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

            // ===== ROBOT LOAD CAPACITY SECTION =====
            var loadLabel = new Label
            {
                Text = "ROBOT LOAD (m³):",
                CustomMinimumSize = new Vector2(0, 18)
            };
            loadLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(loadLabel);

            _loadCapacitySlider = new HSlider
            {
                MinValue = 0.1,
                MaxValue = 2.0,
                Value = SimulationConfig.RobotLoadCapacity,
                Step = 0.05,
                CustomMinimumSize = new Vector2(0, 20)
            };
            _loadCapacitySlider.ValueChanged += OnLoadCapacityChanged;
            vbox.AddChild(_loadCapacitySlider);

            _loadCapacityValueLabel = new Label
            {
                Text = $"→ {SimulationConfig.RobotLoadCapacity:F2}m³",
                CustomMinimumSize = new Vector2(0, 16)
            };
            _loadCapacityValueLabel.AddThemeFontSizeOverride("font_size", 10);
            vbox.AddChild(_loadCapacityValueLabel);

            // Max value input for load capacity
            var loadMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            var loadMaxLabel = new Label { Text = "Max:", CustomMinimumSize = new Vector2(40, 22) };
            _loadCapacityMaxInput = new SpinBox
            {
                MinValue = 0.5,
                MaxValue = 5.0,
                Value = 2.0,
                Step = 0.1,
                CustomMinimumSize = new Vector2(60, 22)
            };
            _loadCapacityMaxInput.ValueChanged += OnLoadCapacityMaxChanged;
            loadMaxHbox.AddChild(loadMaxLabel);
            loadMaxHbox.AddChild(_loadCapacityMaxInput);
            vbox.AddChild(loadMaxHbox);

            GD.Print("[SimulationSettingsUI] ✅ UI panel created successfully!");
            GD.Print($"[SimulationSettingsUI] Dig depth: {SimulationConfig.MaxDigDepth:F3}m");
            GD.Print($"[SimulationSettingsUI] Max speed: {SimulationConfig.MaxRobotSpeed:F1}x");
            GD.Print($"[SimulationSettingsUI] Load capacity: {SimulationConfig.RobotLoadCapacity:F2}m³");
        }

        private void OnDigDepthChanged(double value)
        {
            float digDepth = (float)value;
            SimulationConfig.MaxDigDepth = digDepth;
            _digDepthValueLabel.Text = $"→ {digDepth:F3}m";
            GD.Print($"[⛏️ Settings] Dig depth: {digDepth:F3}m");
        }

        private void OnMaxSpeedChanged(double value)
        {
            float maxSpeed = (float)value;
            SimulationConfig.MaxRobotSpeed = maxSpeed;
            _maxSpeedValueLabel.Text = $"→ {maxSpeed:F1}x";
            GD.Print($"[🚗 Settings] Max robot speed: {maxSpeed:F1}x");
        }

        private void OnLoadCapacityChanged(double value)
        {
            float loadCapacity = (float)value;
            SimulationConfig.RobotLoadCapacity = loadCapacity;
            _loadCapacityValueLabel.Text = $"→ {loadCapacity:F2}m³";
            GD.Print($"[📦 Settings] Robot load capacity: {loadCapacity:F2}m³");
        }

        private void OnDigDepthMaxChanged(double value)
        {
            float maxDigDepth = (float)value;
            _digDepthSlider.MaxValue = maxDigDepth;
            GD.Print($"[⛏️ Settings] Dig depth max: {maxDigDepth:F3}m");
        }

        private void OnMaxSpeedMaxChanged(double value)
        {
            float maxSpeed = (float)value;
            _maxSpeedSlider.MaxValue = maxSpeed;
            GD.Print($"[🚗 Settings] Max robot speed limit: {maxSpeed:F1}x");
        }

        private void OnLoadCapacityMaxChanged(double value)
        {
            float maxLoad = (float)value;
            _loadCapacitySlider.MaxValue = maxLoad;
            GD.Print($"[📦 Settings] Load capacity max: {maxLoad:F2}m³");
        }
    }
}
