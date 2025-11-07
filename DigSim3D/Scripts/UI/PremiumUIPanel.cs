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
            CustomMinimumSize = new Vector2(420, 500);
            
            // Professional, academic-style panel
            _panel = new Panel
            {
                CustomMinimumSize = CustomMinimumSize,
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.13f, 0.15f, 0.96f);
            styleBox.BorderColor = new Color(0.25f, 0.28f, 0.35f, 0.9f);
            styleBox.SetBorderWidthAll(2);
            styleBox.SetCornerRadiusAll(8);
            styleBox.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);
            styleBox.ShadowSize = 16;
            styleBox.ShadowOffset = new Vector2(0, 4);
            _panel.AddThemeStyleboxOverride("panel", styleBox);
            
            AddChild(_panel);
            
            // Professional title bar
            var titleBar = new PanelContainer
            {
                CustomMinimumSize = new Vector2(420, 50),
                MouseFilter = MouseFilterEnum.Stop
            };
            var titleStyleBox = new StyleBoxFlat();
            titleStyleBox.BgColor = new Color(0.18f, 0.20f, 0.24f, 0.98f);
            titleStyleBox.BorderColor = new Color(0.28f, 0.32f, 0.38f, 0.8f);
            titleStyleBox.SetBorderWidthAll(0);
            titleStyleBox.SetBorderWidth(Side.Bottom, 1);
            titleStyleBox.SetCornerRadiusAll(8);
            titleStyleBox.CornerRadiusBottomLeft = 0;
            titleStyleBox.CornerRadiusBottomRight = 0;
            titleBar.AddThemeStyleboxOverride("panel", titleStyleBox);
            _panel.AddChild(titleBar);
            
            var titleLabel = new Label
            {
                Text = Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.85f, 0.88f, 0.92f)
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 15);
            titleLabel.AddThemeColorOverride("font_color", Colors.White);
            titleBar.AddChild(titleLabel);
            
            // Content container with professional margins
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 60);
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
