using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Professional panel component with configurable title and content
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
            
            // Professional dark theme panel
            _panel = new Panel
            {
                CustomMinimumSize = CustomMinimumSize,
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.13f, 0.15f, 0.95f); // Dark charcoal
            styleBox.BorderColor = new Color(0.20f, 0.22f, 0.25f, 1.0f); // Subtle dark border
            styleBox.SetBorderWidthAll(1);
            styleBox.SetCornerRadiusAll(6);
            styleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.4f);
            styleBox.ShadowSize = 15;
            styleBox.ShadowOffset = new Vector2(0, 4);
            _panel.AddThemeStyleboxOverride("panel", styleBox);
            
            AddChild(_panel);
            
            // Title bar
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(380, 40),
                MouseFilter = MouseFilterEnum.Stop
            };
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.08f, 0.09f, 0.11f, 1.0f); // Darker header
            titleStyleBox.SetCornerRadiusAll(6);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            _panel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 15);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.87f, 0.90f, 1.0f)); // Light gray text
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
