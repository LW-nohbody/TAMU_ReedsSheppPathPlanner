using Godot;
using System;
using DigSim3D.Config;

namespace DigSim3D.App
{
    /// <summary>
    /// Enhanced UI panel for adjusting dig depth, max speed, and robot load capacity
    /// Positioned in top-right corner with modern design, clean styling, and visual hierarchy
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
        
        // Theme colors for modern look
        private static readonly Color AccentColor = new Color(0.2f, 0.7f, 0.9f);  // Cyan/Blue
        private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.2f);  // Dark blue-gray
        private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.15f);  // Very dark
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f);  // Off-white
        private static readonly Color LabelColor = new Color(0.75f, 0.75f, 0.8f);  // Light gray

        public override void _Ready()
        {
            GD.Print("[SimulationSettingsUI] Initializing modern UI panel...");

            // Main panel container - positioned in top-right
            _panel = new Control
            {
                CustomMinimumSize = new Vector2(380, 520),
                AnchorLeft = 1.0f,
                AnchorTop = 0.0f,
                AnchorRight = 1.0f,
                AnchorBottom = 0.0f,
                OffsetLeft = -390,
                OffsetTop = 10,
                OffsetRight = -10,
                OffsetBottom = 530
            };
            AddChild(_panel);

            // Background panel with modern styling
            var bgPanel = new Panel();
            bgPanel.AnchorLeft = 0;
            bgPanel.AnchorTop = 0;
            bgPanel.AnchorRight = 1;
            bgPanel.AnchorBottom = 1;
            
            // Dark panel background
            var stylebox = new StyleBoxFlat 
            { 
                BgColor = PanelColor,
                BorderColor = new Color(0.3f, 0.6f, 0.85f, 0.6f)  // Cyan border
            };
            bgPanel.AddThemeStyleboxOverride("panel", stylebox);
            _panel.AddChild(bgPanel);

            // VBox for spacing and layout
            var vbox = new VBoxContainer
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 16,
                OffsetTop = 14,
                OffsetRight = -16,
                OffsetBottom = -14
            };
            vbox.AddThemeConstantOverride("separation", 14);
            _panel.AddChild(vbox);

            // ===== TITLE HEADER =====
            var titleLabel = new Label
            {
                Text = "⚙️  SETTINGS",
                CustomMinimumSize = new Vector2(0, 32)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 18);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.95f));  // Bright cyan
            vbox.AddChild(titleLabel);

            // Separator line
            var sep1 = new HSeparator { CustomMinimumSize = new Vector2(0, 1) };
            vbox.AddChild(sep1);

            // ===== DIG DEPTH SECTION =====
            var digLabel = new Label
            {
                Text = "⛏️  Dig Depth (m)",
                CustomMinimumSize = new Vector2(0, 22)
            };
            digLabel.AddThemeFontSizeOverride("font_size", 12);
            digLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));  // Warm yellow
            vbox.AddChild(digLabel);

            var digHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
            digHbox.AddThemeConstantOverride("separation", 8);
            
            _digDepthSlider = new HSlider
            {
                MinValue = 0.02,
                MaxValue = 0.20,
                Value = SimulationConfig.MaxDigDepth,
                Step = 0.01,
                CustomMinimumSize = new Vector2(200, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            digHbox.AddChild(_digDepthSlider);

            _digDepthValueLabel = new Label
            {
                Text = $"{SimulationConfig.MaxDigDepth:F3}",
                CustomMinimumSize = new Vector2(50, 24)
            };
            _digDepthValueLabel.AddThemeFontSizeOverride("font_size", 11);
            _digDepthValueLabel.AddThemeColorOverride("font_color", TextColor);
            digHbox.AddChild(_digDepthValueLabel);
            vbox.AddChild(digHbox);

            // Max value input for dig depth
            var digMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            digMaxHbox.AddThemeConstantOverride("separation", 6);
            var digMaxLabel = new Label 
            { 
                Text = "Range:", 
                CustomMinimumSize = new Vector2(50, 22)
            };
            digMaxLabel.AddThemeColorOverride("font_color", LabelColor);
            digMaxLabel.AddThemeFontSizeOverride("font_size", 10);
            
            _digDepthMaxInput = new SpinBox
            {
                MinValue = 0.05,
                MaxValue = 0.50,
                Value = 0.20,
                Step = 0.01,
                CustomMinimumSize = new Vector2(80, 22)
            };
            _digDepthMaxInput.ValueChanged += OnDigDepthMaxChanged;
            digMaxHbox.AddChild(digMaxLabel);
            digMaxHbox.AddChild(_digDepthMaxInput);
            vbox.AddChild(digMaxHbox);

            // Spacer
            vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

            // ===== MAX SPEED SECTION =====
            var maxSpeedLabel = new Label
            {
                Text = "🚀  Robot Speed (x)",
                CustomMinimumSize = new Vector2(0, 22)
            };
            maxSpeedLabel.AddThemeFontSizeOverride("font_size", 12);
            maxSpeedLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.5f));  // Green
            vbox.AddChild(maxSpeedLabel);

            var speedHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
            speedHbox.AddThemeConstantOverride("separation", 8);
            
            _maxSpeedSlider = new HSlider
            {
                MinValue = 0.5,
                MaxValue = 4.0,
                Value = SimulationConfig.MaxRobotSpeed,
                Step = 0.1,
                CustomMinimumSize = new Vector2(200, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _maxSpeedSlider.ValueChanged += OnMaxSpeedChanged;
            speedHbox.AddChild(_maxSpeedSlider);

            _maxSpeedValueLabel = new Label
            {
                Text = $"{SimulationConfig.MaxRobotSpeed:F1}x",
                CustomMinimumSize = new Vector2(50, 24)
            };
            _maxSpeedValueLabel.AddThemeFontSizeOverride("font_size", 11);
            _maxSpeedValueLabel.AddThemeColorOverride("font_color", TextColor);
            speedHbox.AddChild(_maxSpeedValueLabel);
            vbox.AddChild(speedHbox);

            // Max value input for speed
            var speedMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            speedMaxHbox.AddThemeConstantOverride("separation", 6);
            var speedMaxLabel = new Label 
            { 
                Text = "Range:", 
                CustomMinimumSize = new Vector2(50, 22)
            };
            speedMaxLabel.AddThemeColorOverride("font_color", LabelColor);
            speedMaxLabel.AddThemeFontSizeOverride("font_size", 10);
            
            _maxSpeedMaxInput = new SpinBox
            {
                MinValue = 1.0,
                MaxValue = 10.0,
                Value = 4.0,
                Step = 0.5,
                CustomMinimumSize = new Vector2(80, 22)
            };
            _maxSpeedMaxInput.ValueChanged += OnMaxSpeedMaxChanged;
            speedMaxHbox.AddChild(speedMaxLabel);
            speedMaxHbox.AddChild(_maxSpeedMaxInput);
            vbox.AddChild(speedMaxHbox);

            // Spacer
            vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

            // ===== ROBOT LOAD CAPACITY SECTION =====
            var loadLabel = new Label
            {
                Text = "📦  Load Capacity (m³)",
                CustomMinimumSize = new Vector2(0, 22)
            };
            loadLabel.AddThemeFontSizeOverride("font_size", 12);
            loadLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.4f));  // Orange/coral
            vbox.AddChild(loadLabel);

            var loadHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
            loadHbox.AddThemeConstantOverride("separation", 8);
            
            _loadCapacitySlider = new HSlider
            {
                MinValue = 0.1,
                MaxValue = 2.0,
                Value = SimulationConfig.RobotLoadCapacity,
                Step = 0.05,
                CustomMinimumSize = new Vector2(200, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _loadCapacitySlider.ValueChanged += OnLoadCapacityChanged;
            loadHbox.AddChild(_loadCapacitySlider);

            _loadCapacityValueLabel = new Label
            {
                Text = $"{SimulationConfig.RobotLoadCapacity:F2}",
                CustomMinimumSize = new Vector2(50, 24)
            };
            _loadCapacityValueLabel.AddThemeFontSizeOverride("font_size", 11);
            _loadCapacityValueLabel.AddThemeColorOverride("font_color", TextColor);
            loadHbox.AddChild(_loadCapacityValueLabel);
            vbox.AddChild(loadHbox);

            // Max value input for load capacity
            var loadMaxHbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
            loadMaxHbox.AddThemeConstantOverride("separation", 6);
            var loadMaxLabel = new Label 
            { 
                Text = "Range:", 
                CustomMinimumSize = new Vector2(50, 22)
            };
            loadMaxLabel.AddThemeColorOverride("font_color", LabelColor);
            loadMaxLabel.AddThemeFontSizeOverride("font_size", 10);
            
            _loadCapacityMaxInput = new SpinBox
            {
                MinValue = 0.5,
                MaxValue = 5.0,
                Value = 2.0,
                Step = 0.1,
                CustomMinimumSize = new Vector2(80, 22)
            };
            _loadCapacityMaxInput.ValueChanged += OnLoadCapacityMaxChanged;
            loadMaxHbox.AddChild(loadMaxLabel);
            loadMaxHbox.AddChild(_loadCapacityMaxInput);
            vbox.AddChild(loadMaxHbox);

            GD.Print("[SimulationSettingsUI] ✅ Modern UI panel created successfully!");
            GD.Print($"[SimulationSettingsUI] Dig depth: {SimulationConfig.MaxDigDepth:F3}m");
            GD.Print($"[SimulationSettingsUI] Max speed: {SimulationConfig.MaxRobotSpeed:F1}x");
            GD.Print($"[SimulationSettingsUI] Load capacity: {SimulationConfig.RobotLoadCapacity:F2}m³");
        }

        private void OnDigDepthChanged(double value)
        {
            float digDepth = (float)value;
            SimulationConfig.MaxDigDepth = digDepth;
            _digDepthValueLabel.Text = $"{digDepth:F3}";
            GD.Print($"[⛏️  Settings] Dig depth: {digDepth:F3}m");
        }

        private void OnMaxSpeedChanged(double value)
        {
            float maxSpeed = (float)value;
            SimulationConfig.MaxRobotSpeed = maxSpeed;
            _maxSpeedValueLabel.Text = $"{maxSpeed:F1}x";
            GD.Print($"[🚀 Settings] Max robot speed: {maxSpeed:F1}x");
        }

        private void OnLoadCapacityChanged(double value)
        {
            float loadCapacity = (float)value;
            SimulationConfig.RobotLoadCapacity = loadCapacity;
            _loadCapacityValueLabel.Text = $"{loadCapacity:F2}";
            GD.Print($"[📦 Settings] Robot load capacity: {loadCapacity:F2}m³");
        }

        private void OnDigDepthMaxChanged(double value)
        {
            float maxDigDepth = (float)value;
            _digDepthSlider.MaxValue = maxDigDepth;
            GD.Print($"[⛏️  Settings] Dig depth max: {maxDigDepth:F3}m");
        }

        private void OnMaxSpeedMaxChanged(double value)
        {
            float maxSpeed = (float)value;
            _maxSpeedSlider.MaxValue = maxSpeed;
            GD.Print($"[🚀 Settings] Max robot speed limit: {maxSpeed:F1}x");
        }

        private void OnLoadCapacityMaxChanged(double value)
        {
            float maxLoad = (float)value;
            _loadCapacitySlider.MaxValue = maxLoad;
            GD.Print($"[📦 Settings] Load capacity max: {maxLoad:F2}m³");
        }
    }
}
