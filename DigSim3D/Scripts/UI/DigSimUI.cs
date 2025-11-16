using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.Domain;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Professional UI system for autonomous excavation monitoring and control
    /// </summary>
    public partial class DigSimUI : Control
    {
        private VBoxContainer _leftPanelContainer = null!;
        private List<PremiumRobotStatusEntry> _robotEntries = new();
        private VBoxContainer _robotEntriesContainer = null!;
        // private Dictionary<int, PremiumRobotStatusEntry> _robotEntries = new();
        private AnimatedValueLabel _remainingDirtLabel = null!;
        private ProgressBar _overallProgressBar = null!;
        private AnimatedValueLabel _overallProgressLabel = null!;
        private AnimatedValueLabel _dirtRemainingLabel = null!;
        private ProgressBar _dirtRemainingBar = null!;  // Changed from thumbnail to progress bar
        private PremiumSlider _speedSlider = null!;
        private PremiumSlider _depthSlider = null!;
        private PremiumSlider _radiusSlider = null!;

        private bool _syncingFromConfig = false;

        private DigConfig _digConfig = null!;
        private float _initialTerrainVolume = 0f;
        private PremiumUIPanel _settingsPanel = null!;
        private List<VehicleVisualizer> _vehicles = new();

        // Draggable state
        private bool _isDraggingLeftPanel = false;
        private Vector2 _dragOffset = Vector2.Zero;
        private Control _leftPanel = null!;
        private Panel _mainPanel = null!; // Track the main panel for resizing
        
        // Animation
        private float _glowIntensity = 0f;
        private bool _glowIncreasing = true;

        public override void _Ready()
        {
            GD.Print("[DigSimUI] Initializing UI system...");

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
            
            GD.Print("[DigSimUI] ✅ UI system initialized");
        }

        private void CreateLeftPanel()
        {
            // Get viewport size for responsive sizing
            var viewportSize = GetViewport().GetVisibleRect().Size;
            
            // Calculate responsive dimensions with better proportions
            // Left panel: 28% of width (more space), 85% of height (not too tall)
            float panelWidth = Mathf.Clamp(viewportSize.X * 0.22f, 380, 480);
            float panelHeight = Mathf.Clamp(viewportSize.Y * 0.75f, 650, viewportSize.Y - 40);

            // Main left panel container (draggable)
            _leftPanel = new Control
            {
                MouseFilter = MouseFilterEnum.Stop
            };
            
            // Position at top-left with better margins
            _leftPanel.AnchorLeft = 0.0f;
            _leftPanel.AnchorTop = 0.0f;
            _leftPanel.AnchorRight = 0.0f;
            _leftPanel.AnchorBottom = 0.0f;
            _leftPanel.OffsetLeft = 20.0f;  // Increased margin
            _leftPanel.OffsetTop = 20.0f;   // Increased margin
            _leftPanel.OffsetRight = 20.0f + panelWidth;
            _leftPanel.OffsetBottom = 20.0f + panelHeight;
            
            AddChild(_leftPanel);

            // Professional dark theme panel with enhanced shadow
            var panelStyleBox = new StyleBoxFlat();
            panelStyleBox.BgColor = new Color(0.10f, 0.11f, 0.13f, 0.97f); // Slightly darker and more opaque
            panelStyleBox.BorderColor = new Color(0.25f, 0.27f, 0.30f, 1.0f); // More visible border
            panelStyleBox.SetBorderWidthAll(2); // Thicker border
            panelStyleBox.SetCornerRadiusAll(8); // More rounded corners
            panelStyleBox.SetExpandMarginAll(0);
            panelStyleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.6f); // Deeper shadow
            panelStyleBox.ShadowSize = 20; // Larger shadow
            panelStyleBox.ShadowOffset = new Vector2(0, 6); // More pronounced shadow offset
            
            _mainPanel = new Panel
            {
                CustomMinimumSize = new Vector2(panelWidth, panelHeight),
                MouseFilter = MouseFilterEnum.Stop
            };
            _mainPanel.AddThemeStyleboxOverride("panel", panelStyleBox);
            _leftPanel.AddChild(_mainPanel);
            
            // Title bar for dragging - responsive width with better styling
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(panelWidth, 50), // Taller title bar
                MouseFilter = MouseFilterEnum.Stop
            };
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.06f, 0.07f, 0.09f, 1.0f); // Even darker header
            titleStyleBox.SetCornerRadiusAll(8);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            _mainPanel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = "Autonomous Excavation System Monitor",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 18); // Larger title font
            titleLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.95f, 1.0f)); // Brighter text
            titleBar.AddChild(titleLabel);
            
            // Content container with better margins
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 25);  // More padding
            margin.AddThemeConstantOverride("margin_right", 25);
            margin.AddThemeConstantOverride("margin_top", 60);   // More top space
            margin.AddThemeConstantOverride("margin_bottom", 25);
            _mainPanel.AddChild(margin);
            
            // Main VBox for fixed content (stats) and scrollable content (agents)
            var mainVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            mainVBox.AddThemeConstantOverride("separation", 12); // More spacing
            margin.AddChild(mainVBox);
            
            // Container for fixed top content (progress, stats, separator)
            var fixedContentVBox = new VBoxContainer();
            fixedContentVBox.AddThemeConstantOverride("separation", 12); // More spacing
            mainVBox.AddChild(fixedContentVBox);
            
            _leftPanelContainer = fixedContentVBox;

            // Overall Progress with animation
            var progressHbox = new HBoxContainer();
            _leftPanelContainer.AddChild(progressHbox);

            _overallProgressLabel = new AnimatedValueLabel
            {
                Text = "Overall Dirt Removed Progress Bar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _overallProgressLabel.SetFontSize(15); 
            _overallProgressLabel.SetColor(new Color(0.85f, 0.85f, 1.0f)); 
            progressHbox.AddChild(_overallProgressLabel);

            // Progress bar 
            var progressBarWidth = panelWidth - 50; // Account for margins
            _overallProgressBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(progressBarWidth, 26),
                MouseFilter = MouseFilterEnum.Stop,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            var progressStyleBox = new StyleBoxFlat();
            progressStyleBox.BgColor = new Color(0.15f, 0.17f, 0.19f, 1.0f); // Darker background
            progressStyleBox.SetCornerRadiusAll(6); // More rounded
            progressStyleBox.BorderColor = new Color(0.28f, 0.30f, 0.33f, 1.0f);
            progressStyleBox.SetBorderWidthAll(1);
            _overallProgressBar.AddThemeStyleboxOverride("background", progressStyleBox);

            var progressFillStyleBox = new StyleBoxFlat();
            progressFillStyleBox.BgColor = new Color(0.20f, 0.90f, 0.40f, 1.0f); // Brighter neon green
            progressFillStyleBox.SetCornerRadiusAll(6); // More rounded
            _overallProgressBar.AddThemeStyleboxOverride("fill", progressFillStyleBox);

            _leftPanelContainer.AddChild(_overallProgressBar);

            // Remaining dirt label
            _remainingDirtLabel = new AnimatedValueLabel
            {
                Text = "Material Remaining: 0.00 m³"
            };
            _remainingDirtLabel.SetFontSize(13); // Slightly larger
            _remainingDirtLabel.SetColor(new Color(0.80f, 0.83f, 0.87f, 1.0f)); // Brighter light gray text
            _leftPanelContainer.AddChild(_remainingDirtLabel);
            
            // Add spacing before dirt remaining bar
            var spacer1 = new Control { CustomMinimumSize = new Vector2(0, 15) }; // More spacing
            _leftPanelContainer.AddChild(spacer1);

            _dirtRemainingLabel = new AnimatedValueLabel
            {
                Text = "Dirt Remaining Progress Bar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _dirtRemainingLabel.SetFontSize(15); // Larger font
            _dirtRemainingLabel.SetColor(new Color(0.85f, 0.85f, 1.0f)); // Brighter
            _leftPanelContainer.AddChild(_dirtRemainingLabel);
            
            // Dirt remaining progress bar with neon orange - responsive width
            _dirtRemainingBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                CustomMinimumSize = new Vector2(progressBarWidth, 26), // Taller
                MouseFilter = MouseFilterEnum.Stop,
                ShowPercentage = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            var dirtProgressStyleBox = new StyleBoxFlat();
            dirtProgressStyleBox.BgColor = new Color(0.15f, 0.17f, 0.19f, 1.0f); // Match other bar
            dirtProgressStyleBox.SetCornerRadiusAll(6);
            dirtProgressStyleBox.BorderColor = new Color(0.28f, 0.30f, 0.33f, 1.0f);
            dirtProgressStyleBox.SetBorderWidthAll(1);
            _dirtRemainingBar.AddThemeStyleboxOverride("background", dirtProgressStyleBox);

            var dirtProgressFillStyleBox = new StyleBoxFlat();
            dirtProgressFillStyleBox.BgColor = new Color(1.0f, 0.60f, 0.20f, 1.0f); // Brighter neon orange for dirt
            dirtProgressFillStyleBox.SetCornerRadiusAll(6);
            _dirtRemainingBar.AddThemeStyleboxOverride("fill", dirtProgressFillStyleBox);

            _leftPanelContainer.AddChild(_dirtRemainingBar);

            // Add spacing after dirt remaining bar
            var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 20) }; // More spacing
            _leftPanelContainer.AddChild(spacer2);

            // Dark theme separator with gradient effect
            var separator = new HSeparator();
            var sepStyleBox = new StyleBoxFlat();
            sepStyleBox.BgColor = new Color(0.30f, 0.32f, 0.35f, 1.0f); // More visible separator
            sepStyleBox.ContentMarginTop = 2;
            sepStyleBox.ContentMarginBottom = 2;
            separator.AddThemeStyleboxOverride("separator", sepStyleBox);
            _leftPanelContainer.AddChild(separator);

            // Add extra spacing after separator (before robot panels)
            var spacer3 = new Control { CustomMinimumSize = new Vector2(0, 20) }; // More spacing
            _leftPanelContainer.AddChild(spacer3);
            
            // Scrollable container for robot/agent panels - responsive sizing with better proportions
            var scrollHeight = panelHeight - 400; // Dynamic based on panel height
            var scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(progressBarWidth, Mathf.Max(350, scrollHeight)),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            
            mainVBox.AddChild(scrollContainer);
            
            // VBox container for robot entries inside scroll
            _robotEntriesContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            _robotEntriesContainer.AddThemeConstantOverride("separation", 10); // More spacing between robot panels
            scrollContainer.AddChild(_robotEntriesContainer);
            
            GD.Print($"[DigSimUI] Left panel created: {panelWidth}x{panelHeight}px with professional dark theme and scrollable agent list");
        }

        private void CreateSettingsPanel()
        {
            // Get viewport size for responsive sizing
            var viewportSize = GetViewport().GetVisibleRect().Size;
            
            // Calculate responsive dimensions with better proportions
            // Settings panel: 24% of width, better height ratio
            float settingsPanelWidth = Mathf.Clamp(viewportSize.X * 0.24f, 400, 500); // Min 400px, max 500px
            float settingsPanelHeight = Mathf.Clamp(viewportSize.Y * 0.55f, 550, 700); // Min 550px, max 700px
            
            _settingsPanel = new PremiumUIPanel
            {
                Title = "System Parameters",
                CustomMinimumSize = new Vector2(settingsPanelWidth, settingsPanelHeight)
            };
            
            // Position at top-right - responsive positioning with better margins
            _settingsPanel.AnchorLeft = 1.0f;
            _settingsPanel.AnchorTop = 0.0f;
            _settingsPanel.AnchorRight = 1.0f;
            _settingsPanel.AnchorBottom = 0.0f;
            _settingsPanel.OffsetLeft = -(settingsPanelWidth - 20);  // Account for PremiumUIPanel margins
            _settingsPanel.OffsetTop = 20;
            _settingsPanel.OffsetRight = 20;  // Extend beyond edge to compensate for panel margins
            _settingsPanel.OffsetBottom = 20 + settingsPanelHeight;
            
            AddChild(_settingsPanel);

            // Add settings content
            AddSettingsContent();
            
            GD.Print($"[DigSimUI] Settings panel created at top-right: {settingsPanelWidth}x{settingsPanelHeight}px");
        }

        private void AddSettingsContent()
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 18); // More spacing
            _settingsPanel.SetContent(vbox);

            // Speed control with preset buttons
            var speedLabel = new Label { Text = "Agent Velocity (m/s)", Modulate = new Color(0.90f, 0.92f, 0.95f, 1.0f) }; // Brighter
            speedLabel.AddThemeFontSizeOverride("font_size", 15); // Larger
            vbox.AddChild(speedLabel);

            var speedPresets = new PresetButtonGroup();
            speedPresets.AddPreset("Slow", 0.5f);
            speedPresets.AddPreset("Medium", 1.5f);
            speedPresets.AddPreset("Fast", 3.0f);
            speedPresets.AddPreset("Turbo", 5.0f);
            speedPresets.PresetSelected += OnSpeedPresetSelected;
            vbox.AddChild(speedPresets);

            _speedSlider = new PremiumSlider
            {
                MinValue = 0.1f,
                MaxValue = 5.0f,
                Value = 0.1f  // Start at minimum
            };
            _speedSlider.ValueChanged += (value) => OnSpeedChanged(value);
            vbox.AddChild(_speedSlider);

            // Dig depth
            var depthLabel = new Label { Text = "Excavation Depth (m)", Modulate = new Color(0.90f, 0.92f, 0.95f, 1.0f) }; // Brighter
            depthLabel.AddThemeFontSizeOverride("font_size", 15); // Larger
            vbox.AddChild(depthLabel);

            var depthPresets = new PresetButtonGroup();
            depthPresets.AddPreset("Shallow", 0.05f);  // Min value (matches slider min)
            depthPresets.AddPreset("Medium", 2.5f);    // Mid-range value (halfway between 0.05 and 5.0)
            depthPresets.AddPreset("Deep", 5.0f);      // Max value (matches slider max)
            depthPresets.PresetSelected += OnDepthPresetSelected;
            vbox.AddChild(depthPresets);

            _depthSlider = new PremiumSlider
            {
                MinValue = 0.05f,
                MaxValue = 1.0f,
                Value = 0.05f // placeholder; will be synced in SetDigConfig
            };
            _depthSlider.ValueChanged += (value) => OnDigDepthChanged(value);
            vbox.AddChild(_depthSlider);

            // Dig radius
            var radiusLabel = new Label { Text = "Tool Radius (m)", Modulate = new Color(0.90f, 0.92f, 0.95f, 1.0f) }; // Brighter
            radiusLabel.AddThemeFontSizeOverride("font_size", 15); // Larger
            vbox.AddChild(radiusLabel);

            _radiusSlider = new PremiumSlider
            {
                MinValue = 0.5f,
                MaxValue = 5.0f,
                Value = 0.5f // placeholder; will be synced in SetDigConfig
            };
            _radiusSlider.ValueChanged += (value) => OnDigRadiusChanged(value);
            vbox.AddChild(_radiusSlider);
        }

        public void AddRobot(int index, string robotId, Color color)
        {
            var robotPanel = new PremiumRobotStatusEntry(robotId, color);
            _robotEntriesContainer.AddChild(robotPanel);
            _robotEntries.Add(robotPanel);
            GD.Print($"[DigSimUI] Added agent status panel {robotId}");
        }

        public void UpdateRobotPayload(int index, float payloadPercent, Vector3 position, string status)
        {
            if (index >= 0 && index < _robotEntries.Count)
            {
                var entry = _robotEntries[index];
                entry.UpdatePayload(payloadPercent, status, position);
            }
            // {
            //     entry.UpdatePayload(payloadPercent, status, position);
            // }
        }

        public void UpdateTerrainProgress(float remainingVolume, float initialVolume)
        {
            _remainingDirtLabel.SetText($"Material Remaining: {remainingVolume:F2} m³");
            
            float progress = initialVolume > 0 ? ((initialVolume - remainingVolume) / initialVolume) * 100f : 0f;
            progress = Mathf.Clamp(progress, 0f, 100f);
            _overallProgressBar.Value = progress;
            
            // Update Dirt Remaining Progress Bar bar (100% to 0% - inverse of progress)
            float dirtRemaining = 100f - progress;
            _dirtRemainingBar.Value = dirtRemaining;
        }

        public void SetDigConfig(DigConfig config)
        {
            _digConfig = config;
            if (_digConfig == null) return;

            _syncingFromConfig = true;

            if (_depthSlider != null)
            {
                float d = _digConfig.DigDepth > 0 ? _digConfig.DigDepth : 0.3f;

                float min = 0.05f;
                float max = MathF.Max(5.0f, d * 1.5f); // up to you

                _depthSlider.Apply(min, max, d);
                _depthSlider.SetLabel("⛏️ Target depth per site (m)");
            }

            if (_radiusSlider != null)
            {
                float r = _digConfig.DigRadius > 0 ? _digConfig.DigRadius : 0.5f;

                float min = 0.1f;
                float max = MathF.Max(5.0f, r * 1.5f);

                _radiusSlider.Apply(min, max, r);
                _radiusSlider.SetLabel("📊 Excavation radius (m)");
            }

            _syncingFromConfig = false;
        }

        public void SetInitialVolume(float volume) => _initialTerrainVolume = volume;

        public void SetVehicles(List<VehicleVisualizer> vehicles) => _vehicles = vehicles;

        // Preset callbacks
        private void OnSpeedPresetSelected(float value)
        {
            if (_speedSlider == null) return;

            _syncingFromConfig = true;
            _speedSlider.Value = value;   // visually move the knob
            _syncingFromConfig = false;

            OnSpeedChanged((double)value);
            GD.Print($"[Settings] Speed preset selected: {value} m/s");
        }

        private void OnDepthPresetSelected(float value)
        {
            if (_depthSlider == null) return;

            _syncingFromConfig = true;
            _depthSlider.Value = value;   // visually move the knob
            _syncingFromConfig = false;

            OnDigDepthChanged(value);     // apply to config
            GD.Print($"[Settings] Depth preset selected: {value} m");
        }

        // Value change callbacks
        private void OnSpeedChanged(double value)
        {
            if (_syncingFromConfig) return;

            float speed = (float)value;
            foreach (var vehicle in _vehicles)
            {
                vehicle.SpeedMps = speed;
            }
            GD.Print($"[Parameters] Agent velocity set to {speed:F2} m/s");
        }

        // Value change callbacks
        private void OnDigDepthChanged(double value)
        {
            if (_syncingFromConfig || _digConfig == null) return;

            _digConfig.DigDepth = (float)value;
            GD.Print($"🔧🔧🔧 [Settings] UI CHANGED DigConfig.DigDepth to {_digConfig.DigDepth:F2}m (config object hash: {_digConfig.GetHashCode()})");
        }

        private void OnDigRadiusChanged(double value)
        {
            if (_syncingFromConfig || _digConfig == null) return;
            _digConfig.DigRadius = (float)value;
            GD.Print($"[Settings] 📏 Radius changed to {(float)value:F3} m");
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
        }
        
        public bool IsPointInUI(Vector2 point)
        {
            return _leftPanel.GetGlobalRect().HasPoint(point)
                || _settingsPanel.GetGlobalRect().HasPoint(point);
        }


    }
}
