using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium panel with glassmorphism and draggable support
    /// </summary>
    public partial class PremiumUIPanel : Control
    {
        public string Title { get; set; } = "Panel";
        
        private Panel _panel = null!;
        private VBoxContainer _contentContainer = null!;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(380, 500);
            
            // Main panel with glassmorphism
            _panel = new Panel
            {
                CustomMinimumSize = CustomMinimumSize,
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            styleBox.BorderColor = new Color(0.4f, 0.6f, 1.0f, 0.8f);
            styleBox.SetBorderWidthAll(3);
            styleBox.SetCornerRadiusAll(12);
            styleBox.ShadowColor = new Color(0.4f, 0.6f, 1.0f, 0.4f);
            styleBox.ShadowSize = 8;
            _panel.AddThemeStyleboxOverride("panel", styleBox);
            
            AddChild(_panel);
            
            // Title bar
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(380, 40),
                MouseFilter = MouseFilterEnum.Stop
            };
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            titleStyleBox.SetCornerRadiusAll(12);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            _panel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.7f, 0.9f, 1.0f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            titleLabel.AddThemeColorOverride("font_color", Colors.White);
            titleBar.AddChild(titleLabel);
            
            // Content container
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 50);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            _panel.AddChild(margin);
            
            _contentContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _contentContainer.AddThemeConstantOverride("separation", 10);
            margin.AddChild(_contentContainer);
        }

        public void SetContent(Node content)
        {
            if (_contentContainer != null)
            {
                // Clear existing
                foreach (var child in _contentContainer.GetChildren())
                {
                    _contentContainer.RemoveChild(child);
                }
                _contentContainer.AddChild(content);
            }
        }
    }
}
