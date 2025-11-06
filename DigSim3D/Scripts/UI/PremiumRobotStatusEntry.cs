using Godot;
using System;
using System.Collections.Generic;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium robot status entry with charts, animations, and color coding
    /// </summary>
    public partial class PremiumRobotStatusEntry : PanelContainer
    {
        private int _robotId;
        private Color _robotColor;
        
        // UI Elements
        private Label _nameLabel = null!;
        private AnimatedValueLabel _payloadLabel = null!;
        private AnimatedValueLabel _speedLabel = null!;
        private Label _statusLabel = null!;
        private MiniChart _speedChart = null!;
        private MiniChart _payloadChart = null!;
        private Button _collapseButton = null!;
        private VBoxContainer _detailsContainer = null!;
        
        private bool _isCollapsed = false;
        private float _lastUpdateTime = 0f;
        
        public PremiumRobotStatusEntry(int id, string name, Color color)
        {
            _robotId = id;
            _robotColor = color;
            CustomMinimumSize = new Vector2(380, 160);
            MouseFilter = MouseFilterEnum.Stop;
            
            SetupStyles();
            CreateContent(name);
        }
        
        private void SetupStyles()
        {
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.12f, 0.18f, 0.85f);
            styleBox.BorderColor = _robotColor;
            styleBox.SetBorderWidthAll(2);
            styleBox.SetCornerRadiusAll(8);
            
            // Glow effect
            styleBox.ShadowColor = new Color(_robotColor.R, _robotColor.G, _robotColor.B, 0.3f);
            styleBox.ShadowSize = 6;
            styleBox.ShadowOffset = new Vector2(0, 2);
            
            AddThemeStyleboxOverride("panel", styleBox);
        }
        
        private void CreateContent(string name)
        {
            var mainContainer = new VBoxContainer();
            mainContainer.AddThemeConstantOverride("separation", 8);
            AddChild(mainContainer);
            
            // Header with name and collapse button
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 8);
            mainContainer.AddChild(header);
            
            _nameLabel = new Label
            {
                Text = $"🤖 {name}",
                Modulate = _robotColor,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 14);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            header.AddChild(_nameLabel);
            
            _collapseButton = new Button
            {
                Text = "▼",
                CustomMinimumSize = new Vector2(32, 32)
            };
            _collapseButton.Pressed += ToggleCollapse;
            header.AddChild(_collapseButton);
            
            // Details container (collapsible)
            _detailsContainer = new VBoxContainer();
            _detailsContainer.AddThemeConstantOverride("separation", 6);
            mainContainer.AddChild(_detailsContainer);
            
            // Status label
            _statusLabel = new Label
            {
                Text = "Status: Idle",
                Modulate = new Color(0.8f, 0.8f, 0.9f)
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            _detailsContainer.AddChild(_statusLabel);
            
            // Animated value labels
            _payloadLabel = new AnimatedValueLabel
            {
                LabelText = "Payload",
                ValueFormat = "F0",
                ValueSuffix = "%"
            };
            _payloadLabel.SetColorRanges(0, 100, 40, 80);
            _detailsContainer.AddChild(_payloadLabel);
            
            _speedLabel = new AnimatedValueLabel
            {
                LabelText = "Speed",
                ValueFormat = "F2",
                ValueSuffix = " m/s"
            };
            _speedLabel.SetColorRanges(0f, 2f, 0.4f, 0.8f);
            _detailsContainer.AddChild(_speedLabel);
            
            // Charts container
            var chartsContainer = new HBoxContainer();
            chartsContainer.AddThemeConstantOverride("separation", 8);
            _detailsContainer.AddChild(chartsContainer);
            
            // Speed chart
            var speedChartContainer = new VBoxContainer();
            speedChartContainer.AddThemeConstantOverride("separation", 2);
            chartsContainer.AddChild(speedChartContainer);
            
            var speedLabel = new Label { Text = "Speed History" };
            speedLabel.AddThemeFontSizeOverride("font_size", 9);
            speedLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
            speedChartContainer.AddChild(speedLabel);
            
            _speedChart = new MiniChart
            {
                ChartTitle = "Speed",
                CustomMinimumSize = new Vector2(170, 60)
            };
            _speedChart.SetValueRange(0, 1);
            _speedChart.SetColor(new Color(0.4f, 0.8f, 1.0f), new Color(0.4f, 0.8f, 1.0f, 0.2f));
            speedChartContainer.AddChild(_speedChart);
            
            // Payload chart
            var payloadChartContainer = new VBoxContainer();
            payloadChartContainer.AddThemeConstantOverride("separation", 2);
            chartsContainer.AddChild(payloadChartContainer);
            
            var payloadLabelChart = new Label { Text = "Payload History" };
            payloadLabelChart.AddThemeFontSizeOverride("font_size", 9);
            payloadLabelChart.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
            payloadChartContainer.AddChild(payloadLabelChart);
            
            _payloadChart = new MiniChart
            {
                ChartTitle = "Payload",
                CustomMinimumSize = new Vector2(170, 60)
            };
            _payloadChart.SetValueRange(0, 100);
            _payloadChart.SetColor(new Color(1.0f, 0.6f, 0.3f), new Color(1.0f, 0.6f, 0.3f, 0.2f));
            payloadChartContainer.AddChild(_payloadChart);
        }
        
        public void UpdateStatus(float payloadPercent, string status, Vector3 position, float speed)
        {
            _payloadLabel.SetValue(payloadPercent * 100f);
            _speedLabel.SetValue(speed);
            _statusLabel.Text = $"{status} | ({position.X:F1}, {position.Z:F1})";
            
            // Update charts every 0.2 seconds
            float currentTime = Time.GetTicksMsec() / 1000f;
            if (currentTime - _lastUpdateTime > 0.2f)
            {
                _speedChart.AddDataPoint(speed);
                _payloadChart.AddDataPoint(payloadPercent * 100f);
                _lastUpdateTime = currentTime;
            }
        }
        
        private void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;
            _collapseButton.Text = _isCollapsed ? "▶" : "▼";
            
            // Animate collapse/expand
            var tween = CreateTween();
            if (_isCollapsed)
            {
                tween.TweenProperty(_detailsContainer, "scale:y", 0.0f, 0.2f);
                tween.TweenProperty(_detailsContainer, "modulate:a", 0.0f, 0.2f);
                tween.Finished += () => _detailsContainer.Visible = false;
                
                CustomMinimumSize = new Vector2(380, 50);
            }
            else
            {
                _detailsContainer.Visible = true;
                _detailsContainer.Scale = new Vector2(1, 0);
                _detailsContainer.Modulate = new Color(1, 1, 1, 0);
                
                tween.TweenProperty(_detailsContainer, "scale:y", 1.0f, 0.2f);
                tween.TweenProperty(_detailsContainer, "modulate:a", 1.0f, 0.2f);
                
                CustomMinimumSize = new Vector2(380, 160);
            }
        }
    }
}
