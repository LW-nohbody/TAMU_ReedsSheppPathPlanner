using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.Domain;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium DigSim3D UI with glassmorphism, animations, and advanced features
    /// </summary>
    public partial class DigSimUI : Control
    {
        private VBoxContainer _leftPanelContainer = null!;
        private Dictionary<int, PremiumRobotStatusEntry> _robotEntries = new();
        private AnimatedValueLabel _remainingDirtLabel = null!;
        private ProgressBar _overallProgressBar = null!;
        private AnimatedValueLabel _overallProgressLabel = null!;
        private Label _heatMapStatusLabel = null!;
        private ProgressBar _dirtRemainingBar = null!;  // Changed from thumbnail to progress bar

        private DigConfig _digConfig = null!;
        private float _initialTerrainVolume = 0f;
        private PremiumUIPanel _settingsPanel = null!;
        private List<VehicleVisualizer> _vehicles = new();
        
        // Draggable state
        private bool _isDraggingLeftPanel = false;
        private Vector2 _dragOffset = Vector2.Zero;
        private Control _leftPanel = null!;
        
        // Animation
        private float _glowIntensity = 0f;
        private bool _glowIncreasing = true;

        public override void _Ready()
        {
            GD.Print("[DigSimUI] Initializing premium UI...");

            // Root setup
            Visible = true;
            Modulate = new Color(1, 1, 1, 1);
            ZIndex = 100;
            MouseFilter = MouseFilterEnum.Ignore; // Let clicks pass through to children
            
            // Fill entire viewport
            AnchorRight = 1.0f;
            AnchorBottom = 1.0f;

            CreateLeftPanel();
            CreateSettingsPanel();
            
            GD.Print("[DigSimUI] ✅ Premium UI initialized!");
        }

        private void CreateLeftPanel()
        {
            // Main left panel container (draggable)
            _leftPanel = new Control
            {
                MouseFilter = MouseFilterEnum.Stop
            };
            
            // Position at top-left
            _leftPanel.AnchorLeft = 0.0f;
            _leftPanel.AnchorTop = 0.0f;
            _leftPanel.AnchorRight = 0.0f;
            _leftPanel.AnchorBottom = 0.0f;
            _leftPanel.OffsetLeft = 15.0f;
            _leftPanel.OffsetTop = 15.0f;
            _leftPanel.OffsetRight = 435.0f; // 420px width
            _leftPanel.OffsetBottom = 715.0f; // 700px height
            
            AddChild(_leftPanel);

            // Glassmorphism panel with blur effect
            var panelStyleBox = new StyleBoxFlat();
            panelStyleBox.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f); // Dark with transparency
            panelStyleBox.BorderColor = new Color(0.4f, 0.6f, 1.0f, 0.8f); // Blue glow border
            panelStyleBox.SetBorderWidthAll(2);
            panelStyleBox.SetCornerRadiusAll(12); // More rounded
            panelStyleBox.SetExpandMarginAll(2); // Glow effect
            panelStyleBox.ShadowColor = new Color(0.4f, 0.6f, 1.0f, 0.4f);
            panelStyleBox.ShadowSize = 8;
            panelStyleBox.ShadowOffset = new Vector2(0, 2);
            
            var panel = new Panel
            {
                CustomMinimumSize = new Vector2(420, 700),
                MouseFilter = MouseFilterEnum.Stop
            };
            panel.AddThemeStyleboxOverride("panel", panelStyleBox);
            _leftPanel.AddChild(panel);
            
            // Title bar for dragging
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(420, 40),
                MouseFilter = MouseFilterEnum.Stop
            };
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            titleStyleBox.SetCornerRadiusAll(12);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            panel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = "🚀 DigSim3D - Robot Status",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.7f, 0.9f, 1.0f, 1.0f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 18);
            titleLabel.AddThemeColorOverride("font_color", Colors.White);
            titleBar.AddChild(titleLabel);
            
            // Content container
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 50);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            panel.AddChild(margin);
            
            _leftPanelContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _leftPanelContainer.AddThemeConstantOverride("separation", 10);
            margin.AddChild(_leftPanelContainer);

            // Overall Progress with animation
            var progressHbox = new HBoxContainer();
            _leftPanelContainer.AddChild(progressHbox);
            
            _overallProgressLabel = new AnimatedValueLabel
            {
                Text = "Progress: 0%",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _overallProgressLabel.SetFontSize(14);
            _overallProgressLabel.SetColor(new Color(0.7f, 1.0f, 0.8f));
            progressHbox.AddChild(_overallProgressLabel);

            // Progress bar with gradient
            _overallProgressBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(380, 20),
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var progressStyleBox = new StyleBoxFlat();
            progressStyleBox.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.6f);
            progressStyleBox.SetCornerRadiusAll(6);
            _overallProgressBar.AddThemeStyleboxOverride("background", progressStyleBox);
            
            var progressFillStyleBox = new StyleBoxFlat();
            progressFillStyleBox.BgColor = new Color(0.3f, 0.8f, 0.5f, 1.0f); // Green
            progressFillStyleBox.SetCornerRadiusAll(6);
            _overallProgressBar.AddThemeStyleboxOverride("fill", progressFillStyleBox);
            
            _leftPanelContainer.AddChild(_overallProgressBar);

            // Remaining dirt with animation
            _remainingDirtLabel = new AnimatedValueLabel
            {
                Text = "Remaining: 0.00 m³"
            };
            _remainingDirtLabel.SetFontSize(12);
            _remainingDirtLabel.SetColor(new Color(1.0f, 0.9f, 0.7f));
            _leftPanelContainer.AddChild(_remainingDirtLabel);

            // Heat map status with icon
            _heatMapStatusLabel = new Label
            {
                Text = "🌡️ Heat Map: OFF",
                Modulate = Colors.White
            };
            _heatMapStatusLabel.AddThemeFontSizeOverride("font_size", 11);
            _heatMapStatusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1.0f));
            _leftPanelContainer.AddChild(_heatMapStatusLabel);
            
            // Add spacing before dirt remaining bar
            var spacer1 = new Control { CustomMinimumSize = new Vector2(0, 10) };
            _leftPanelContainer.AddChild(spacer1);
            
            // Dirt remaining progress bar (starts at 100%, decreases to 0%)
            _dirtRemainingBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,  // Start at 100%
                CustomMinimumSize = new Vector2(380, 20),
                MouseFilter = MouseFilterEnum.Stop,
                ShowPercentage = true
            };
            
            var dirtProgressStyleBox = new StyleBoxFlat();
            dirtProgressStyleBox.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.6f);
            dirtProgressStyleBox.SetCornerRadiusAll(6);
            _dirtRemainingBar.AddThemeStyleboxOverride("background", dirtProgressStyleBox);
            
            var dirtProgressFillStyleBox = new StyleBoxFlat();
            dirtProgressFillStyleBox.BgColor = new Color(0.8f, 0.5f, 0.3f, 1.0f); // Orange/brown for dirt
            dirtProgressFillStyleBox.SetCornerRadiusAll(6);
            _dirtRemainingBar.AddThemeStyleboxOverride("fill", dirtProgressFillStyleBox);
            
            _leftPanelContainer.AddChild(_dirtRemainingBar);

            // Add spacing after dirt remaining bar
            var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 15) };
            _leftPanelContainer.AddChild(spacer2);

            // Separator with gradient
            var separator = new HSeparator();
            var sepStyleBox = new StyleBoxFlat();
            sepStyleBox.BgColor = new Color(0.4f, 0.6f, 1.0f, 0.3f);
            sepStyleBox.ContentMarginTop = 1;
            sepStyleBox.ContentMarginBottom = 1;
            separator.AddThemeStyleboxOverride("separator", sepStyleBox);
            _leftPanelContainer.AddChild(separator);
            
            // Add extra spacing after separator (before robot panels)
            var spacer3 = new Control { CustomMinimumSize = new Vector2(0, 15) };
            _leftPanelContainer.AddChild(spacer3);
            
            GD.Print("[DigSimUI] Left panel created with glassmorphism");
        }

        private void CreateSettingsPanel()
        {
            _settingsPanel = new PremiumUIPanel
            {
                Title = "⚙️ Advanced Settings",
                CustomMinimumSize = new Vector2(380, 500)
            };
            
            // Position at top-right
            _settingsPanel.AnchorLeft = 1.0f;
            _settingsPanel.AnchorTop = 0.0f;
            _settingsPanel.AnchorRight = 1.0f;
            _settingsPanel.AnchorBottom = 0.0f;
            _settingsPanel.OffsetLeft = -395; // -width - margin
            _settingsPanel.OffsetTop = 15;
            _settingsPanel.OffsetRight = -15;
            _settingsPanel.OffsetBottom = 515;
            
            AddChild(_settingsPanel);
            
            // Add settings content
            AddSettingsContent();
            
            GD.Print("[DigSimUI] Settings panel created at top-right");
        }

        private void AddSettingsContent()
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 15);
            _settingsPanel.SetContent(vbox);
            
            // Speed control with preset buttons
            var speedLabel = new Label { Text = "🚗 Robot Speed", Modulate = Colors.White };
            speedLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(speedLabel);
            
            var speedPresets = new PresetButtonGroup();
            speedPresets.AddPreset("Slow", 0.5f);
            speedPresets.AddPreset("Medium", 1.5f);
            speedPresets.AddPreset("Fast", 3.0f);
            speedPresets.AddPreset("Turbo", 5.0f);
            speedPresets.PresetSelected += OnSpeedPresetSelected;
            vbox.AddChild(speedPresets);
            
            var speedSlider = new PremiumSlider
            {
                MinValue = 0.1f,
                MaxValue = 5.0f,
                Value = 0.1f  // Start at minimum
            };
            speedSlider.ValueChanged += (value) => OnSpeedChanged(value);
            vbox.AddChild(speedSlider);
            
            // Dig depth
            var depthLabel = new Label { Text = "⛏️ Excavation Depth", Modulate = Colors.White };
            depthLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(depthLabel);
            
            var depthPresets = new PresetButtonGroup();
            depthPresets.AddPreset("Shallow", 0.1f);
            depthPresets.AddPreset("Medium", 0.3f);
            depthPresets.AddPreset("Deep", 0.6f);
            depthPresets.PresetSelected += OnDepthPresetSelected;
            vbox.AddChild(depthPresets);
            
            var depthSlider = new PremiumSlider
            {
                MinValue = 0.05f,
                MaxValue = 1.0f,
                Value = 0.05f  // Start at minimum
            };
            depthSlider.ValueChanged += (value) => OnDigDepthChanged(value);
            vbox.AddChild(depthSlider);
            
            // Dig radius
            var radiusLabel = new Label { Text = "📊 Excavation Radius", Modulate = Colors.White };
            radiusLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(radiusLabel);
            
            var radiusSlider = new PremiumSlider
            {
                MinValue = 0.5f,
                MaxValue = 5.0f,
                Value = 0.5f  // Start at minimum
            };
            radiusSlider.ValueChanged += (value) => OnDigRadiusChanged(value);
            vbox.AddChild(radiusSlider);
        }

        public void AddRobot(int robotId, string name, Color color)
        {
            // Add spacing before each robot panel
            var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
            _leftPanelContainer.AddChild(spacer);
            
            var robotPanel = new PremiumRobotStatusEntry(robotId, name, color);
            _leftPanelContainer.AddChild(robotPanel);
            _robotEntries[robotId] = robotPanel;
            GD.Print($"[DigSimUI] Added premium robot panel {robotId}: {name}");
        }

        public void UpdateRobotPayload(int robotId, float payloadPercent, Vector3 position, string status)
        {
            if (_robotEntries.TryGetValue(robotId, out var entry))
            {
                entry.UpdatePayload(payloadPercent, status, position);
            }
        }

        public void UpdateTerrainProgress(float remainingVolume, float initialVolume)
        {
            _remainingDirtLabel.SetText($"Remaining: {remainingVolume:F2} m³");
            
            float progress = initialVolume > 0 ? ((initialVolume - remainingVolume) / initialVolume) * 100f : 0f;
            progress = Mathf.Clamp(progress, 0f, 100f);
            
            // Update overall progress bar (0% to 100%)
            _overallProgressBar.Value = progress;
            _overallProgressLabel.SetText($"Progress: {progress:F0}%");
            
            // Update dirt remaining bar (100% to 0% - inverse of progress)
            float dirtRemaining = 100f - progress;
            _dirtRemainingBar.Value = dirtRemaining;
        }

        public void SetDigConfig(DigConfig config) => _digConfig = config;
        
        public void SetHeatMapStatus(bool enabled)
        {
            string icon = enabled ? "🔥" : "🌡️";
            string status = enabled ? "ON" : "OFF";
            _heatMapStatusLabel.Text = $"{icon} Heat Map: {status}";
        }

        public void SetInitialVolume(float volume) => _initialTerrainVolume = volume;
        
        public void SetVehicles(List<VehicleVisualizer> vehicles) => _vehicles = vehicles;

        // Preset callbacks
        private void OnSpeedPresetSelected(float value)
        {
            OnSpeedChanged((double)value);
            GD.Print($"[Settings] Speed preset selected: {value} m/s");
        }
        
        private void OnDepthPresetSelected(float value)
        {
            OnDigDepthChanged((double)value);
            GD.Print($"[Settings] Depth preset selected: {value} m");
        }

        // Value change callbacks
        private void OnSpeedChanged(double value)
        {
            float speed = (float)value;
            foreach (var vehicle in _vehicles)
            {
                vehicle.SpeedMps = speed;
            }
            GD.Print($"[Settings] ⚡ Robot speed changed to {speed:F2} m/s");
        }

        private void OnDigDepthChanged(double value)
        {
            float depth = (float)value;
            if (_digConfig != null)
            {
                _digConfig.DigDepth = depth;
            }
            GD.Print($"[Settings] ⛏️ Dig depth changed to {depth:F2} m");
        }

        private void OnDigRadiusChanged(double value)
        {
            float radius = (float)value;
            if (_digConfig != null)
            {
                _digConfig.DigRadius = radius;
            }
            GD.Print($"[Settings] 📊 Dig radius changed to {radius:F2} m");
        }

        public override void _Process(double delta)
        {
            // Animate glow effect
            float dt = (float)delta;
            if (_glowIncreasing)
            {
                _glowIntensity += dt * 0.5f;
                if (_glowIntensity >= 1.0f)
                {
                    _glowIntensity = 1.0f;
                    _glowIncreasing = false;
                }
            }
            else
            {
                _glowIntensity -= dt * 0.5f;
                if (_glowIntensity <= 0.3f)
                {
                    _glowIntensity = 0.3f;
                    _glowIncreasing = true;
                }
            }
            
            // Update border glow
            // Note: In production you'd update the StyleBox color here
        }
    }
}
