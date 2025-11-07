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
        private Control _settingsPanel = null!;
        private List<VehicleVisualizer> _vehicles = new();
        
        // Settings panel sections
        private VBoxContainer _currentSettingsContent = null!;
        private Label _activeRobotsLabel = null!;
        private Label _avgSpeedLabel = null!;
        private Label _totalDistanceLabel = null!;
        private Label _simulationTimeLabel = null!;
        private float _simulationTime = 0f;
        
        // Current values for delta indicators
        private float _baselineSpeed = 0.6f;
        private float _baselineDepth = 0.3f;
        private float _baselineRadius = 1.2f;
        private float _baselineDigRate = 2.0f;
        
        // Value display labels
        private Label _speedValueLabel = null!;
        private Label _depthValueLabel = null!;
        private Label _radiusValueLabel = null!;
        private Label _digRateValueLabel = null!;
        
        // Draggable state
        private bool _isDraggingLeftPanel = false;
        private Vector2 _dragOffset = Vector2.Zero;
        private Control _leftPanel = null!;
        private Panel _mainPanel = null!;  // Store reference for dynamic resizing
        
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

            // Wait one frame for viewport to be ready
            CallDeferred(nameof(CreatePanels));
            
            GD.Print("[DigSimUI] ✅ Premium UI initialized!");
        }
        
        private void CreatePanels()
        {
            CreateLeftPanel();
            CreateSettingsPanel();
        }

        private void CreateLeftPanel()
        {
            // Get viewport size for responsive sizing
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            float panelWidth = Mathf.Min(500f, viewportSize.X * 0.25f); // 25% of screen width, max 500px
            float initialHeight = Mathf.Min(600f, viewportSize.Y * 0.6f); // 60% of screen height, max 600px
            
            // Main left panel container (draggable)
            _leftPanel = new Control
            {
                MouseFilter = MouseFilterEnum.Stop
            };
            
            // Position at top-left with responsive size
            _leftPanel.AnchorLeft = 0.0f;
            _leftPanel.AnchorTop = 0.0f;
            _leftPanel.AnchorRight = 0.0f;
            _leftPanel.AnchorBottom = 0.0f;
            _leftPanel.OffsetLeft = 16.0f;
            _leftPanel.OffsetTop = 16.0f;
            _leftPanel.OffsetRight = 16.0f + panelWidth;
            _leftPanel.OffsetBottom = 16.0f + initialHeight;
            
            AddChild(_leftPanel);

            // Professional, academic-style panel with subtle sophistication
            var panelStyleBox = new StyleBoxFlat();
            panelStyleBox.BgColor = new Color(0.12f, 0.13f, 0.15f, 0.96f); // Dark slate for professionalism
            panelStyleBox.BorderColor = new Color(0.25f, 0.28f, 0.35f, 0.9f); // Muted steel blue
            panelStyleBox.SetBorderWidthAll(2);
            panelStyleBox.SetCornerRadiusAll(8); // Professional rounded corners
            panelStyleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);
            panelStyleBox.ShadowSize = 16;
            panelStyleBox.ShadowOffset = new Vector2(0, 4);
            
            _mainPanel = new Panel
            {
                CustomMinimumSize = new Vector2(panelWidth, 300),
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mainPanel.AddThemeStyleboxOverride("panel", panelStyleBox);
            
            // Fill the parent control
            _mainPanel.AnchorRight = 1.0f;
            _mainPanel.AnchorBottom = 1.0f;
            _mainPanel.OffsetRight = 0;
            _mainPanel.OffsetBottom = 0;
            
            _leftPanel.AddChild(_mainPanel);
            
            // Professional title bar with clean design
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(panelWidth, 52),
                MouseFilter = MouseFilterEnum.Stop
            };
            titleBar.AnchorRight = 1.0f;
            titleBar.OffsetRight = 0;
            
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.98f); // Professional dark blue-grey
            titleStyleBox.BorderColor = new Color(0.28f, 0.32f, 0.38f, 0.8f);
            titleStyleBox.SetBorderWidthAll(0);
            titleStyleBox.SetBorderWidth(Side.Bottom, 1);
            titleStyleBox.SetCornerRadiusAll(8);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            _mainPanel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = "DIGSIM3D - FLEET STATUS",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.85f, 0.88f, 0.92f, 1.0f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            titleLabel.AddThemeColorOverride("font_color", Colors.White);
            titleBar.AddChild(titleLabel);
            
            // Content container with professional spacing
            var margin = new MarginContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            margin.AnchorRight = 1.0f;
            margin.AnchorBottom = 1.0f;
            margin.OffsetRight = 0;
            margin.OffsetBottom = 0;
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 62);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            _mainPanel.AddChild(margin);
            
            // Add ScrollContainer to handle overflow
            var scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            margin.AddChild(scrollContainer);
            
            _leftPanelContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            _leftPanelContainer.AddThemeConstantOverride("separation", 10);
            scrollContainer.AddChild(_leftPanelContainer);

            // Mission Progress Section Header (Academic style)
            var missionHeader = new Label
            {
                Text = "Mission Overview",
                Modulate = new Color(0.7f, 0.75f, 0.82f, 1.0f)
            };
            missionHeader.AddThemeFontSizeOverride("font_size", 13);
            missionHeader.AddThemeColorOverride("font_color", new Color(0.65f, 0.70f, 0.78f));
            _leftPanelContainer.AddChild(missionHeader);

            // Overall Progress with clean professional styling
            var progressHbox = new HBoxContainer();
            progressHbox.AddThemeConstantOverride("separation", 10);
            _leftPanelContainer.AddChild(progressHbox);
            
            _overallProgressLabel = new AnimatedValueLabel
            {
                Text = "Progress: 0%",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _overallProgressLabel.SetFontSize(14);
            _overallProgressLabel.SetColor(new Color(0.82f, 0.85f, 0.90f));
            progressHbox.AddChild(_overallProgressLabel);

            // Clean, professional progress bar
            _overallProgressBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 24),
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var progressStyleBox = new StyleBoxFlat();
            progressStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.9f);
            progressStyleBox.BorderColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
            progressStyleBox.SetBorderWidthAll(1);
            progressStyleBox.SetCornerRadiusAll(4);
            _overallProgressBar.AddThemeStyleboxOverride("background", progressStyleBox);
            
            var progressFillStyleBox = new StyleBoxFlat();
            progressFillStyleBox.BgColor = new Color(0.35f, 0.75f, 0.45f, 1.0f); // Professional green
            progressFillStyleBox.SetCornerRadiusAll(3);
            _overallProgressBar.AddThemeStyleboxOverride("fill", progressFillStyleBox);
            
            _leftPanelContainer.AddChild(_overallProgressBar);

            // Professional remaining dirt display
            _remainingDirtLabel = new AnimatedValueLabel
            {
                Text = "Volume Remaining: 0.00 m³"
            };
            _remainingDirtLabel.SetFontSize(12);
            _remainingDirtLabel.SetColor(new Color(0.75f, 0.78f, 0.85f));
            _leftPanelContainer.AddChild(_remainingDirtLabel);

            // Heat map status - academic style
            _heatMapStatusLabel = new Label
            {
                Text = "Terrain Analysis: Inactive",
                Modulate = Colors.White
            };
            _heatMapStatusLabel.AddThemeFontSizeOverride("font_size", 11);
            _heatMapStatusLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.75f));
            _leftPanelContainer.AddChild(_heatMapStatusLabel);
            
            // Add spacing before dirt remaining bar
            var spacer1 = new Control { CustomMinimumSize = new Vector2(0, 6) };
            _leftPanelContainer.AddChild(spacer1);
            
            // Professional dirt remaining progress bar
            _dirtRemainingBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,  // Start at 100%
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                MouseFilter = MouseFilterEnum.Stop,
                ShowPercentage = true
            };
            
            var dirtProgressStyleBox = new StyleBoxFlat();
            dirtProgressStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.9f);
            dirtProgressStyleBox.BorderColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
            dirtProgressStyleBox.SetBorderWidthAll(1);
            dirtProgressStyleBox.SetCornerRadiusAll(4);
            _dirtRemainingBar.AddThemeStyleboxOverride("background", dirtProgressStyleBox);
            
            var dirtProgressFillStyleBox = new StyleBoxFlat();
            dirtProgressFillStyleBox.BgColor = new Color(0.78f, 0.55f, 0.28f, 1.0f); // Professional amber
            dirtProgressFillStyleBox.SetCornerRadiusAll(3);
            _dirtRemainingBar.AddThemeStyleboxOverride("fill", dirtProgressFillStyleBox);
            
            _leftPanelContainer.AddChild(_dirtRemainingBar);

            // Add spacing after dirt remaining bar
            var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 14) };
            _leftPanelContainer.AddChild(spacer2);

            // Professional separator
            var separator = new HSeparator();
            var sepStyleBox = new StyleBoxFlat();
            sepStyleBox.BgColor = new Color(0.28f, 0.32f, 0.38f, 0.6f);
            sepStyleBox.ContentMarginTop = 1;
            sepStyleBox.ContentMarginBottom = 1;
            separator.AddThemeStyleboxOverride("separator", sepStyleBox);
            _leftPanelContainer.AddChild(separator);
            
            // Robot Fleet Section Header (Academic style)
            var fleetHeader = new Label
            {
                Text = "Active Units",
                Modulate = new Color(0.7f, 0.75f, 0.82f, 1.0f)
            };
            fleetHeader.AddThemeFontSizeOverride("font_size", 13);
            fleetHeader.AddThemeColorOverride("font_color", new Color(0.65f, 0.70f, 0.78f));
            _leftPanelContainer.AddChild(fleetHeader);
            
            // Add spacing after header
            var spacer3 = new Control { CustomMinimumSize = new Vector2(0, 6) };
            _leftPanelContainer.AddChild(spacer3);
            
            GD.Print("[DigSimUI] Professional left panel created with dynamic sizing");
        }

        private void CreateSettingsPanel()
        {
            // Get viewport size for responsive sizing
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            float panelWidth = Mathf.Min(500f, viewportSize.X * 0.3f); // 30% of screen width, max 500px
            float panelHeight = viewportSize.Y - 32f; // Full height minus margins
            
            // Create custom settings panel instead of PremiumUIPanel
            _settingsPanel = new Control
            {
                MouseFilter = MouseFilterEnum.Stop
            };
            
            // Position at top-right with professional spacing
            _settingsPanel.AnchorLeft = 1.0f;
            _settingsPanel.AnchorTop = 0.0f;
            _settingsPanel.AnchorRight = 1.0f;
            _settingsPanel.AnchorBottom = 1.0f;
            _settingsPanel.OffsetLeft = -panelWidth - 16; // -width - margin
            _settingsPanel.OffsetTop = 16;
            _settingsPanel.OffsetRight = -16;
            _settingsPanel.OffsetBottom = -16;
            
            AddChild(_settingsPanel);
            
            // Professional panel background
            var panelStyleBox = new StyleBoxFlat();
            panelStyleBox.BgColor = new Color(0.12f, 0.13f, 0.15f, 0.96f);
            panelStyleBox.BorderColor = new Color(0.25f, 0.28f, 0.35f, 0.9f);
            panelStyleBox.SetBorderWidthAll(2);
            panelStyleBox.SetCornerRadiusAll(8);
            panelStyleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);
            panelStyleBox.ShadowSize = 16;
            panelStyleBox.ShadowOffset = new Vector2(0, 4);
            
            var mainPanel = new Panel
            {
                CustomMinimumSize = new Vector2(panelWidth, panelHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            mainPanel.AddThemeStyleboxOverride("panel", panelStyleBox);
            
            // Fill the parent control
            mainPanel.AnchorRight = 1.0f;
            mainPanel.AnchorBottom = 1.0f;
            mainPanel.OffsetRight = 0;
            mainPanel.OffsetBottom = 0;
            
            _settingsPanel.AddChild(mainPanel);
            
            // Title bar
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(panelWidth, 52),
                MouseFilter = MouseFilterEnum.Stop
            };
            titleBar.AnchorRight = 1.0f;
            titleBar.OffsetRight = 0;
            
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.98f);
            titleStyleBox.BorderColor = new Color(0.28f, 0.32f, 0.38f, 0.8f);
            titleStyleBox.SetBorderWidthAll(0);
            titleStyleBox.SetBorderWidth(Side.Bottom, 1);
            titleStyleBox.SetCornerRadiusAll(8);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            mainPanel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = "SIMULATION PARAMETERS",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.85f, 0.88f, 0.92f, 1.0f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            titleLabel.AddThemeColorOverride("font_color", Colors.White);
            titleBar.AddChild(titleLabel);
            
            // Add settings content
            AddSettingsContent(mainPanel);
            
            GD.Print("[DigSimUI] Professional settings panel created");
        }

        private void AddSettingsContent(Panel parentPanel)
        {
            // ScrollContainer for overflow
            var margin = new MarginContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            margin.AnchorRight = 1.0f;
            margin.AnchorBottom = 1.0f;
            margin.OffsetRight = 0;
            margin.OffsetBottom = 0;
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 62);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            parentPanel.AddChild(margin);
            
            var scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            margin.AddChild(scrollContainer);
            
            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            vbox.AddThemeConstantOverride("separation", 18);
            scrollContainer.AddChild(vbox);
            
            // === SPEED CARD ===
            _speedValueLabel = CreateSettingCard(vbox, "Robot Velocity", "m/s", 
                new Color(0.3f, 0.6f, 0.9f), // Blue accent
                0.1f, 5.0f, 0.6f, 
                new[] { ("Slow", 0.5f), ("Medium", 1.5f), ("Fast", 3.0f), ("Max", 5.0f) },
                OnSpeedChanged, false, new[] { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f, 5.0f });
            
            // === DEPTH CARD ===
            _depthValueLabel = CreateSettingCard(vbox, "Excavation Depth", "m",
                new Color(0.9f, 0.5f, 0.2f), // Orange accent
                0.05f, 1.0f, 0.3f,
                new[] { ("Shallow", 0.1f), ("Standard", 0.3f), ("Deep", 0.6f) },
                OnDigDepthChanged, false, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.8f, 1.0f });
            
            // === RADIUS CARD ===
            _radiusValueLabel = CreateSettingCard(vbox, "Excavation Radius", "m",
                new Color(0.7f, 0.4f, 0.9f), // Purple accent
                0.5f, 5.0f, 1.2f,
                new[] { ("Small", 0.8f), ("Medium", 1.5f), ("Large", 3.0f) },
                OnDigRadiusChanged, false, new[] { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f, 5.0f });
            
            // === DIG RATE CARD (with logarithmic scale for wider range) ===
            _digRateValueLabel = CreateSettingCard(vbox, "Excavation Rate", "m³/s",
                new Color(0.4f, 0.8f, 0.5f), // Green accent
                0.5f, 10.0f, 2.0f,
                new[] { ("Slow", 1.0f), ("Normal", 2.0f), ("Fast", 5.0f), ("Rapid", 8.0f) },
                OnDigRateChanged, true, new[] { 0.5f, 1.0f, 2.0f, 3.0f, 5.0f, 7.0f, 10.0f });
        }
        
        private Label CreateSettingCard(VBoxContainer parent, string title, string unit, Color accentColor,
            float minValue, float maxValue, float initialValue, (string, float)[] presets,
            Action<double> onValueChanged, bool useLogScale = false, float[] detents = null)
        {
            // Declare the label that will be returned
            Label valueLabel;
            
            // Card container with elevation
            var cardContainer = new PanelContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(0.16f, 0.17f, 0.20f, 0.95f);
            cardStyle.BorderColor = new Color(0.22f, 0.24f, 0.28f, 0.9f);
            cardStyle.SetBorderWidthAll(1);
            cardStyle.SetCornerRadiusAll(6);
            cardStyle.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.3f);
            cardStyle.ShadowSize = 6;
            cardStyle.ShadowOffset = new Vector2(0, 3);
            
            // Add colored accent bar on left
            cardStyle.SetBorderWidth(Side.Left, 4);
            cardStyle.BorderColor = accentColor;
            
            cardContainer.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(cardContainer);
            
            var cardMargin = new MarginContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            cardMargin.AddThemeConstantOverride("margin_left", 16);
            cardMargin.AddThemeConstantOverride("margin_right", 16);
            cardMargin.AddThemeConstantOverride("margin_top", 12);
            cardMargin.AddThemeConstantOverride("margin_bottom", 12);
            cardContainer.AddChild(cardMargin);
            
            var cardVbox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            cardVbox.AddThemeConstantOverride("separation", 10);
            cardMargin.AddChild(cardVbox);
            
            // Header row with title and large value display
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 12);
            cardVbox.AddChild(headerRow);
            
            var titleLabel = new Label
            {
                Text = title,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Modulate = new Color(0.75f, 0.78f, 0.85f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 13);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
            headerRow.AddChild(titleLabel);
            
            // Large value display
            valueLabel = new Label
            {
                Text = $"{initialValue:F2} {unit}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = accentColor
            };
            valueLabel.AddThemeFontSizeOverride("font_size", 18);
            valueLabel.AddThemeColorOverride("font_color", Colors.White);
            headerRow.AddChild(valueLabel);
            
            // Delta indicator (initially hidden, will show on change)
            var deltaLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = new Color(0.6f, 0.9f, 0.7f)
            };
            deltaLabel.AddThemeFontSizeOverride("font_size", 11);
            deltaLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.7f));
            cardVbox.AddChild(deltaLabel);
            
            // Preset buttons
            if (presets != null && presets.Length > 0)
            {
                var presetContainer = new HBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                presetContainer.AddThemeConstantOverride("separation", 8);
                cardVbox.AddChild(presetContainer);
                
                foreach (var (label, value) in presets)
                {
                    var btn = new Button
                    {
                        Text = label,
                        CustomMinimumSize = new Vector2(60, 32),
                        SizeFlagsHorizontal = SizeFlags.ExpandFill
                    };
                    
                    var btnStyle = new StyleBoxFlat();
                    btnStyle.BgColor = new Color(0.20f, 0.22f, 0.26f, 0.9f);
                    btnStyle.BorderColor = new Color(0.28f, 0.32f, 0.38f, 0.8f);
                    btnStyle.SetBorderWidthAll(1);
                    btnStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("normal", btnStyle);
                    
                    var btnHoverStyle = new StyleBoxFlat();
                    btnHoverStyle.BgColor = new Color(0.25f, 0.28f, 0.32f, 1.0f);
                    btnHoverStyle.BorderColor = accentColor;
                    btnHoverStyle.SetBorderWidthAll(2);
                    btnHoverStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("hover", btnHoverStyle);
                    
                    btn.Pressed += () => onValueChanged(value);
                    presetContainer.AddChild(btn);
                }
            }
            
            // Slider row with quick adjust buttons
            var sliderRow = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            sliderRow.AddThemeConstantOverride("separation", 6);
            cardVbox.AddChild(sliderRow);
            
            // Enhanced slider with optional logarithmic scale (declare before quick buttons)
            var slider = new HSlider
            {
                MinValue = useLogScale ? Mathf.Log(minValue) : minValue,
                MaxValue = useLogScale ? Mathf.Log(maxValue) : maxValue,
                Value = useLogScale ? Mathf.Log(initialValue) : initialValue,
                Step = (maxValue - minValue) / 1000f, // Fine-grained for detent snapping
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(100, 28)
            };
            
            // Quick adjust buttons (after slider is declared)
            CreateQuickButton(sliderRow, "-10%", () => AdjustSliderValue(minValue, maxValue, valueLabel, unit, deltaLabel, onValueChanged, initialValue, -0.10f, slider, useLogScale));
            CreateQuickButton(sliderRow, "-1%", () => AdjustSliderValue(minValue, maxValue, valueLabel, unit, deltaLabel, onValueChanged, initialValue, -0.01f, slider, useLogScale));
            
            // Styled slider
            var sliderBg = new StyleBoxFlat();
            sliderBg.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.9f);
            sliderBg.BorderColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
            sliderBg.SetBorderWidthAll(1);
            sliderBg.SetCornerRadiusAll(4);
            slider.AddThemeStyleboxOverride("slider", sliderBg);
            
            var grabberStyle = new StyleBoxFlat();
            grabberStyle.BgColor = accentColor;
            grabberStyle.SetCornerRadiusAll(6);
            slider.AddThemeStyleboxOverride("grabber_area", grabberStyle);
            
            var grabberHighlightStyle = new StyleBoxFlat();
            grabberHighlightStyle.BgColor = new Color(accentColor.R * 1.3f, accentColor.G * 1.3f, accentColor.B * 1.3f);
            grabberHighlightStyle.SetCornerRadiusAll(6);
            grabberHighlightStyle.ShadowColor = accentColor;
            grabberHighlightStyle.ShadowSize = 4;
            slider.AddThemeStyleboxOverride("grabber_area_highlight", grabberHighlightStyle);
            
            // Detent snapping threshold (5% of range)
            float detentThreshold = (maxValue - minValue) * 0.05f;
            
            slider.ValueChanged += (rawValue) =>
            {
                // Convert from log scale if needed
                float actualValue = useLogScale ? Mathf.Exp((float)rawValue) : (float)rawValue;
                
                // Apply detent snapping if detents are defined
                if (detents != null && detents.Length > 0)
                {
                    float closestDetent = actualValue;
                    float minDistance = float.MaxValue;
                    
                    foreach (float detent in detents)
                    {
                        float distance = Mathf.Abs(actualValue - detent);
                        if (distance < minDistance && distance < detentThreshold)
                        {
                            minDistance = distance;
                            closestDetent = detent;
                        }
                    }
                    
                    // Snap to closest detent if within threshold
                    if (minDistance < detentThreshold)
                    {
                        actualValue = closestDetent;
                        // Update slider to snapped value
                        slider.Value = useLogScale ? Mathf.Log(actualValue) : actualValue;
                    }
                }
                
                valueLabel.Text = $"{actualValue:F2} {unit}";
                float delta = actualValue - initialValue;
                string arrow = delta > 0 ? "↑" : delta < 0 ? "↓" : "=";
                deltaLabel.Text = delta != 0 ? $"{arrow} {Math.Abs(delta):F2} {unit}" : "";
                deltaLabel.Modulate = delta > 0 ? new Color(0.6f, 0.9f, 0.7f) : delta < 0 ? new Color(0.9f, 0.7f, 0.6f) : new Color(0.7f, 0.7f, 0.7f);
                onValueChanged(actualValue);
            };
            
            sliderRow.AddChild(slider);
            
            // Add visual detent markers if detents are defined
            if (detents != null && detents.Length > 0)
            {
                var detentOverlay = new Control
                {
                    MouseFilter = MouseFilterEnum.Ignore,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(100, 28)
                };
                detentOverlay.Position = slider.Position;
                detentOverlay.Size = slider.Size;
                
                detentOverlay.Draw += () =>
                {
                    float sliderWidth = detentOverlay.Size.X;
                    float sliderHeight = detentOverlay.Size.Y;
                    float trackY = sliderHeight / 2f;
                    
                    foreach (float detentValue in detents)
                    {
                        float normalizedPos = (detentValue - minValue) / (maxValue - minValue);
                        float xPos = normalizedPos * sliderWidth;
                        
                        // Draw detent marker (small vertical line)
                        detentOverlay.DrawLine(
                            new Vector2(xPos, trackY - 4),
                            new Vector2(xPos, trackY + 4),
                            new Color(accentColor.R * 0.7f, accentColor.G * 0.7f, accentColor.B * 0.7f, 0.6f),
                            2f
                        );
                    }
                };
                
                sliderRow.AddChild(detentOverlay);
            }
            
            CreateQuickButton(sliderRow, "+1%", () => AdjustSliderValue(minValue, maxValue, valueLabel, unit, deltaLabel, onValueChanged, initialValue, 0.01f, slider, useLogScale));
            CreateQuickButton(sliderRow, "+10%", () => AdjustSliderValue(minValue, maxValue, valueLabel, unit, deltaLabel, onValueChanged, initialValue, 0.10f, slider, useLogScale));
            
            // Recommended range indicator
            var rangeLabel = new Label
            {
                Text = GetRecommendedRangeText(title, minValue, maxValue),
                Modulate = new Color(0.6f, 0.65f, 0.72f, 0.9f)
            };
            rangeLabel.AddThemeFontSizeOverride("font_size", 10);
            rangeLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.68f, 0.75f));
            cardVbox.AddChild(rangeLabel);
            
            // Optional: Add a collapsible dual-thumb range slider for defining acceptable bounds
            var advancedToggle = new Button
            {
                Text = "⚙ Advanced Range",
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 24)
            };
            
            var toggleStyle = new StyleBoxFlat();
            toggleStyle.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.7f);
            toggleStyle.SetCornerRadiusAll(3);
            advancedToggle.AddThemeStyleboxOverride("normal", toggleStyle);
            advancedToggle.AddThemeFontSizeOverride("font_size", 10);
            
            var togglePressedStyle = new StyleBoxFlat();
            togglePressedStyle.BgColor = new Color(0.22f, 0.25f, 0.30f, 0.9f);
            togglePressedStyle.BorderColor = accentColor;
            togglePressedStyle.SetBorderWidthAll(1);
            togglePressedStyle.SetCornerRadiusAll(3);
            advancedToggle.AddThemeStyleboxOverride("pressed", togglePressedStyle);
            
            cardVbox.AddChild(advancedToggle);
            
            // Range slider container (initially hidden)
            var rangeContainer = new VBoxContainer
            {
                Visible = false
            };
            rangeContainer.AddThemeConstantOverride("separation", 6);
            cardVbox.AddChild(rangeContainer);
            
            var rangeLabel2 = new Label
            {
                Text = "Acceptable Range:",
                Modulate = new Color(0.65f, 0.68f, 0.75f)
            };
            rangeLabel2.AddThemeFontSizeOverride("font_size", 10);
            rangeContainer.AddChild(rangeLabel2);
            
            var dualSlider = new DualThumbSlider
            {
                MinValue = minValue,
                MaxValue = maxValue,
                CurrentMin = minValue + (maxValue - minValue) * 0.2f,
                CurrentMax = minValue + (maxValue - minValue) * 0.8f,
                AccentColor = accentColor
            };
            rangeContainer.AddChild(dualSlider);
            
            var rangeInfoLabel = new Label
            {
                Text = $"Acceptable: {dualSlider.CurrentMin:F2} - {dualSlider.CurrentMax:F2} {unit}",
                Modulate = new Color(0.6f, 0.8f, 0.6f)
            };
            rangeInfoLabel.AddThemeFontSizeOverride("font_size", 9);
            rangeContainer.AddChild(rangeInfoLabel);
            
            dualSlider.RangeChanged += (min, max) =>
            {
                rangeInfoLabel.Text = $"Acceptable: {min:F2} - {max:F2} {unit}";
            };
            
            advancedToggle.Toggled += (pressed) =>
            {
                rangeContainer.Visible = pressed;
            };
            
            // Return the value label so it can be stored for later updates
            return valueLabel;
        }
        
        private void CreateQuickButton(HBoxContainer parent, string text, Action onPressed)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(40, 28),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            
            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.20f, 0.22f, 0.26f, 0.9f);
            btnStyle.BorderColor = new Color(0.28f, 0.32f, 0.38f, 0.7f);
            btnStyle.SetBorderWidthAll(1);
            btnStyle.SetCornerRadiusAll(3);
            btn.AddThemeStyleboxOverride("normal", btnStyle);
            
            var btnHoverStyle = new StyleBoxFlat();
            btnHoverStyle.BgColor = new Color(0.25f, 0.28f, 0.32f, 1.0f);
            btnHoverStyle.BorderColor = new Color(0.35f, 0.4f, 0.48f, 1.0f);
            btnHoverStyle.SetBorderWidthAll(1);
            btnHoverStyle.SetCornerRadiusAll(3);
            btn.AddThemeStyleboxOverride("hover", btnHoverStyle);
            
            btn.Pressed += () => onPressed();
            parent.AddChild(btn);
        }
        
        private void AdjustSliderValue(float min, float max, Label valueLabel, string unit, Label deltaLabel,
            Action<double> onValueChanged, float baseline, float percentChange, HSlider slider = null, bool useLogScale = false)
        {
            // Parse current value from label
            string currentText = valueLabel.Text.Replace(unit, "").Trim();
            if (float.TryParse(currentText, out float currentValue))
            {
                float range = max - min;
                float adjustment = range * percentChange;
                float newValue = Mathf.Clamp(currentValue + adjustment, min, max);
                
                valueLabel.Text = $"{newValue:F2} {unit}";
                float delta = newValue - baseline;
                string arrow = delta > 0 ? "↑" : delta < 0 ? "↓" : "=";
                deltaLabel.Text = delta != 0 ? $"{arrow} {Math.Abs(delta):F2} {unit}" : "";
                deltaLabel.Modulate = delta > 0 ? new Color(0.6f, 0.9f, 0.7f) : delta < 0 ? new Color(0.9f, 0.7f, 0.6f) : new Color(0.7f, 0.7f, 0.7f);
                
                if (slider != null)
                {
                    // Update slider value, accounting for log scale
                    slider.Value = useLogScale ? Mathf.Log(newValue) : newValue;
                }
                
                onValueChanged(newValue);
            }
        }
        
        private string GetRecommendedRangeText(string paramName, float min, float max)
        {
            return paramName switch
            {
                "Robot Velocity" => "🟢 Optimal: 1.0-2.5 m/s  🟡 Caution: 2.5-4.0 m/s  🔴 Extreme: >4.0 m/s",
                "Excavation Depth" => "🟢 Optimal: 0.2-0.4 m  🟡 Caution: 0.4-0.7 m  🔴 Extreme: >0.7 m",
                "Excavation Radius" => "🟢 Optimal: 1.0-2.0 m  🟡 Caution: 2.0-3.5 m  🔴 Extreme: >3.5 m",
                "Excavation Rate" => "🟢 Optimal: 1.5-3.0 m³/s  🟡 Caution: 3.0-6.0 m³/s  🔴 Extreme: >6.0 m³/s",
                _ => $"Range: {min:F1} - {max:F1}"
            };
        }

        public void AddRobot(int robotId, string name, Color color)
        {
            // Add spacing before each robot panel
            var spacer = new Control { CustomMinimumSize = new Vector2(0, 6) };
            _leftPanelContainer.AddChild(spacer);
            
            var robotPanel = new PremiumRobotStatusEntry(robotId, name, color);
            _leftPanelContainer.AddChild(robotPanel);
            _robotEntries[robotId] = robotPanel;
            
            // Dynamically resize panel to fit all robots
            CallDeferred(nameof(UpdatePanelHeight));
            
            GD.Print($"[DigSimUI] Added robot panel {robotId}: {name}");
        }

        private void UpdatePanelHeight()
        {
            if (_leftPanelContainer == null || _mainPanel == null || _leftPanel == null) return;
            
            // Force update layout to get accurate sizes
            _leftPanelContainer.CallDeferred("update_minimum_size");
            
            // Wait one frame for layout update
            CallDeferred(nameof(ApplyPanelHeight));
        }
        
        private void ApplyPanelHeight()
        {
            if (_leftPanelContainer == null || _mainPanel == null || _leftPanel == null) return;
            
            // Calculate required height based on actual content
            float contentHeight = 0f;
            int separation = (int)_leftPanelContainer.GetThemeConstant("separation");
            
            int childCount = 0;
            foreach (var child in _leftPanelContainer.GetChildren())
            {
                if (child is Control control && control.Visible)
                {
                    float childHeight = control.CustomMinimumSize.Y;
                    
                    // If no custom minimum size, use calculated size
                    if (childHeight <= 0)
                    {
                        childHeight = control.Size.Y > 0 ? control.Size.Y : 30f;
                    }
                    
                    contentHeight += childHeight;
                    childCount++;
                }
            }
            
            // Add separation between items
            if (childCount > 1)
            {
                contentHeight += separation * (childCount - 1);
            }
            
            // Add margins (top + bottom) and title bar height
            float totalHeight = contentHeight + 62 + 40 + 52; // content + top margin + bottom margin + title bar
            
            // Ensure minimum height and reasonable maximum
            totalHeight = Mathf.Max(400f, totalHeight);
            
            // Get viewport height to avoid going off-screen
            float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
            float maxHeight = viewportHeight - 40f; // 40px total margin from top and bottom
            totalHeight = Mathf.Min(totalHeight, maxHeight);
            
            // Get current panel width
            float panelWidth = _leftPanel.Size.X > 0 ? _leftPanel.Size.X : _mainPanel.Size.X;
            
            // Update panel size
            _mainPanel.CustomMinimumSize = new Vector2(panelWidth, totalHeight);
            _leftPanel.OffsetBottom = _leftPanel.OffsetTop + totalHeight;
            
            GD.Print($"[DigSimUI] Panel height adjusted to {totalHeight}px for {_robotEntries.Count} robots (content: {contentHeight}px)");
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
            string status = enabled ? "Active" : "Inactive";
            _heatMapStatusLabel.Text = $"Terrain Analysis: {status}";
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

        private void OnDigRateChanged(double value)
        {
            float digRate = (float)value;
            if (_digConfig != null)
            {
                _digConfig.DigRatePerSecond = digRate;
            }
            GD.Print($"[Settings] ⚙️ Dig rate changed to {digRate:F2} m³/s");
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
