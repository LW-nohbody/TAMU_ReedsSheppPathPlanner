using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DigSim3D.Domain;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Simple, reliable DigSim3D UI system using Position-based layout
    /// </summary>
    public partial class DigSimUIv2 : Control
    {
        private VBoxContainer _container = null!;
        private Dictionary<int, RobotStatusEntry> _robotEntries = new();
        private Label _remainingDirtLabel = null!;
        private ProgressBar _overallProgressBar = null!;
        private Label _overallProgressLabel = null!;
        private Label _heatMapStatusLabel = null!;

        private DigConfig _digConfig = null!;
        private float _initialTerrainVolume = 0f;
        private SimulationSettingsPanel _settingsPanel = null!;

        public override void _Ready()
        {
            GD.Print("[DigSimUIv2] Initializing UI...");

            // CRITICAL: Ensure UI is visible
            Visible = true;
            Modulate = new Color(1, 1, 1, 1); // Fully opaque
            ZIndex = 100; // Draw on top
            MouseFilter = MouseFilterEnum.Stop; // Prevent mouse events from passing through

            // Anchor to top-left corner (explicitly set)
            AnchorLeft = 0.0f;
            AnchorTop = 0.0f;
            AnchorRight = 0.0f;
            AnchorBottom = 0.0f;
            
            // Position and size - LARGER panel
            OffsetLeft = 15.0f;
            OffsetTop = 15.0f;
            OffsetRight = 415.0f; // 400px width + 15px margin
            OffsetBottom = 815.0f; // 800px height

            // Create main panel
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(400, 800),
                MouseFilter = MouseFilterEnum.Stop // Prevent click-through
            };
            
            // Create a StyleBoxFlat for visible background
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Darker, more opaque background
            styleBox.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f); // Light blue border
            styleBox.SetBorderWidthAll(3);
            styleBox.SetCornerRadiusAll(8);
            panel.AddThemeStyleboxOverride("panel", styleBox);
            
            AddChild(panel);

            // Create container for all content
            _container = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _container.AddThemeConstantOverride("separation", 8);
            
            // Add padding/margin to container
            var marginContainer = new MarginContainer();
            marginContainer.AddThemeConstantOverride("margin_left", 15);
            marginContainer.AddThemeConstantOverride("margin_right", 15);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            marginContainer.MouseFilter = MouseFilterEnum.Stop;
            panel.AddChild(marginContainer);
            marginContainer.AddChild(_container);

            // Progress label
            _overallProgressLabel = new Label 
            { 
                Text = "Progress: 0%",
                Modulate = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore // Labels don't need to block mouse
            };
            _overallProgressLabel.AddThemeFontSizeOverride("font_size", 16);
            _overallProgressLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_overallProgressLabel);

            // Progress bar
            _overallProgressBar = new ProgressBar
            {
                MinValue = 0, 
                MaxValue = 100, 
                Value = 0,
                CustomMinimumSize = new Vector2(370, 28),
                MouseFilter = MouseFilterEnum.Stop
            };
            _container.AddChild(_overallProgressBar);

            // Remaining dirt label
            _remainingDirtLabel = new Label 
            { 
                Text = "Remaining: 0.00 m³",
                Modulate = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _remainingDirtLabel.AddThemeFontSizeOverride("font_size", 13);
            _remainingDirtLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_remainingDirtLabel);

            // Heat map status
            _heatMapStatusLabel = new Label 
            { 
                Text = "Heat Map: OFF",
                Modulate = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _heatMapStatusLabel.AddThemeFontSizeOverride("font_size", 13);
            _heatMapStatusLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_heatMapStatusLabel);

            // Separator
            var separator = new HSeparator();
            _container.AddChild(separator);

            GD.Print($"[DigSimUIv2] ✅ UI initialized!");
            GD.Print($"[DigSimUIv2] Visible={Visible}, ZIndex={ZIndex}, Position=({OffsetLeft},{OffsetTop})");
            GD.Print($"[DigSimUIv2] Size=({OffsetRight - OffsetLeft},{OffsetBottom - OffsetTop})");
            
            // === Create Settings Panel (positioned bottom-right) ===
            CreateSettingsPanel();
        }
        
        private void CreateSettingsPanel()
        {
            _settingsPanel = new SimulationSettingsPanel();
            
            // Position in TOP-RIGHT corner
            _settingsPanel.AnchorLeft = 1.0f;   // Right edge
            _settingsPanel.AnchorTop = 0.0f;    // TOP edge (0 = top, 1 = bottom)
            _settingsPanel.AnchorRight = 1.0f;  // Right edge
            _settingsPanel.AnchorBottom = 0.0f; // TOP edge
            _settingsPanel.OffsetLeft = -330;   // 320px width + 10px margin from right
            _settingsPanel.OffsetTop = 15;      // 15px margin from top
            _settingsPanel.OffsetRight = -10;   // 10px margin from right
            _settingsPanel.OffsetBottom = 415;  // 400px height
            
            // Ensure it's visible and blocks mouse
            _settingsPanel.Visible = true;
            _settingsPanel.MouseFilter = MouseFilterEnum.Stop;
            
            // Add to parent CanvasLayer (not this Control)
            GetParent().AddChild(_settingsPanel);
            
            GD.Print("[DigSimUIv2] Settings panel created at TOP-RIGHT");
        }

        public void AddRobot(int robotId, string name, Color color)
        {
            var robotPanel = new RobotStatusEntry(robotId, name, color);
            _container.AddChild(robotPanel);
            _robotEntries[robotId] = robotPanel;
            GD.Print($"[DigSimUIv2] Added robot {robotId}: {name}");
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
            _remainingDirtLabel.Text = $"Remaining: {remainingVolume:F2} m³";
            
            float progress = initialVolume > 0 ? ((initialVolume - remainingVolume) / initialVolume) * 100f : 0f;
            progress = Mathf.Clamp(progress, 0f, 100f);
            
            _overallProgressBar.Value = progress;
            _overallProgressLabel.Text = $"Progress: {progress:F0}%";
        }

        public void SetDigConfig(DigConfig config)
        {
            _digConfig = config;
            
            // Also set on settings panel
            if (_settingsPanel != null)
            {
                _settingsPanel.SetDigConfig(config);
            }
        }

        public void SetHeatMapStatus(bool enabled)
        {
            _heatMapStatusLabel.Text = $"Heat Map: {(enabled ? "ON" : "OFF")}";
        }

        public void SetInitialVolume(float volume)
        {
            _initialTerrainVolume = volume;
        }
        
        public void SetVehicles(List<VehicleVisualizer> vehicles)
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetVehicles(vehicles);
            }
        }
    }

    public partial class RobotStatusEntry : PanelContainer
    {
        private int _robotId;
        private Label _nameLabel = null!;
        private ProgressBar _payloadBar = null!;
        private Label _statusLabel = null!;
        private Color _robotColor;

        public RobotStatusEntry(int id, string name, Color color)
        {
            _robotId = id;
            _robotColor = color;
            CustomMinimumSize = new Vector2(370, 95);
            MouseFilter = MouseFilterEnum.Stop; // Stop mouse events
            
            // Add visible background to robot panel
            var robotStyleBox = new StyleBoxFlat();
            robotStyleBox.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            robotStyleBox.BorderColor = color;
            robotStyleBox.SetBorderWidthAll(2);
            robotStyleBox.SetCornerRadiusAll(6);
            AddThemeStyleboxOverride("panel", robotStyleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.MouseFilter = MouseFilterEnum.Ignore; // Let parent handle mouse
            AddChild(vbox);

            // Robot name with color
            _nameLabel = new Label 
            { 
                Text = $"[{name}]",
                Modulate = color,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 14);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(_nameLabel);

            // Payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(350, 20),
                MouseFilter = MouseFilterEnum.Stop
            };
            vbox.AddChild(_payloadBar);

            // Status label
            _statusLabel = new Label 
            { 
                Text = "Status: Idle",
                Modulate = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            _statusLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(_statusLabel);
        }

        public void UpdatePayload(float percentFull, string status, Vector3 pos)
        {
            _payloadBar.Value = percentFull * 100f;
            _statusLabel.Text = $"{status} | ({pos.X:F1}, {pos.Z:F1})";
        }
    }
}
