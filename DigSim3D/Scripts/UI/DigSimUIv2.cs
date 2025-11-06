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
        private SimulationSettingsPanel_Simple _settingsPanel = null!;

        public override void _Ready()
        {
            GD.Print("[DigSimUIv2] Initializing UI...");

            // CRITICAL: Ensure UI is visible
            Visible = true;
            Modulate = new Color(1, 1, 1, 1); // Fully opaque
            ZIndex = 100; // Draw on top

            // Anchor to top-left corner
            AnchorLeft = 0.0f;
            AnchorTop = 0.0f;
            AnchorRight = 0.0f;
            AnchorBottom = 0.0f;
            OffsetLeft = 10.0f;
            OffsetTop = 10.0f;
            OffsetRight = 310.0f; // 300px width + 10px margin
            OffsetBottom = 610.0f; // Arbitrary height, will be controlled by content

            // Create main panel
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(300, 600)
            };
            
            // Create a StyleBoxFlat for visible background
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f); // Dark semi-transparent background
            styleBox.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f); // Light blue border
            styleBox.SetBorderWidthAll(2);
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
            marginContainer.AddThemeConstantOverride("margin_left", 10);
            marginContainer.AddThemeConstantOverride("margin_right", 10);
            marginContainer.AddThemeConstantOverride("margin_top", 10);
            marginContainer.AddThemeConstantOverride("margin_bottom", 10);
            panel.AddChild(marginContainer);
            marginContainer.AddChild(_container);

            // Progress label
            _overallProgressLabel = new Label 
            { 
                Text = "Progress: 0%",
                Modulate = Colors.White
            };
            _overallProgressLabel.AddThemeFontSizeOverride("font_size", 14);
            _overallProgressLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_overallProgressLabel);

            // Progress bar
            _overallProgressBar = new ProgressBar
            {
                MinValue = 0, 
                MaxValue = 100, 
                Value = 0,
                CustomMinimumSize = new Vector2(280, 20)
            };
            _container.AddChild(_overallProgressBar);

            // Remaining dirt label
            _remainingDirtLabel = new Label 
            { 
                Text = "Remaining: 0.00 m³",
                Modulate = Colors.White
            };
            _remainingDirtLabel.AddThemeFontSizeOverride("font_size", 11);
            _remainingDirtLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_remainingDirtLabel);

            // Heat map status
            _heatMapStatusLabel = new Label 
            { 
                Text = "Heat Map: OFF",
                Modulate = Colors.White
            };
            _heatMapStatusLabel.AddThemeFontSizeOverride("font_size", 11);
            _heatMapStatusLabel.AddThemeColorOverride("font_color", Colors.White);
            _container.AddChild(_heatMapStatusLabel);

            // Separator
            var separator = new HSeparator();
            _container.AddChild(separator);

            GD.Print($"[DigSimUIv2] ✅ UI initialized!");
            GD.Print($"[DigSimUIv2] Visible={Visible}, ZIndex={ZIndex}, Position=({OffsetLeft},{OffsetTop})");
            GD.Print($"[DigSimUIv2] Size=({OffsetRight - OffsetLeft},{OffsetBottom - OffsetTop})");
            
            // Create settings panel
            CreateSettingsPanel();
        }
        
        private void CreateSettingsPanel()
        {
            _settingsPanel = new SimulationSettingsPanel_Simple();
            GetParent().AddChild(_settingsPanel);
            GD.Print("[DigSimUIv2] Settings panel created");
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
            _settingsPanel?.SetDigConfig(config);
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
            _settingsPanel?.SetVehicles(vehicles);
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
            CustomMinimumSize = new Vector2(280, 80);
            
            // Add visible background to robot panel
            var robotStyleBox = new StyleBoxFlat();
            robotStyleBox.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            robotStyleBox.BorderColor = color;
            robotStyleBox.SetBorderWidthAll(2);
            robotStyleBox.SetCornerRadiusAll(4);
            AddThemeStyleboxOverride("panel", robotStyleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            AddChild(vbox);

            // Robot name with color
            _nameLabel = new Label 
            { 
                Text = $"[{name}]",
                Modulate = color
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 12);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(_nameLabel);

            // Payload bar
            _payloadBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(260, 16)
            };
            vbox.AddChild(_payloadBar);

            // Status label
            _statusLabel = new Label 
            { 
                Text = "Status: Idle",
                Modulate = Colors.White
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 10);
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
