using Godot;

namespace DigSim3D.App
{
    /// <summary>
    /// Visual legend showing the heat map color scale
    /// </summary>
    public partial class HeatMapLegend : Control
    {
        private bool _visible = false;
        private Panel _panel = null!;
        private ColorRect _gradientBar = null!;
        private Label _titleLabel = null!;
        private Label _highLabel = null!;
        private Label _lowLabel = null!;

        public new bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (_panel != null)
                    _panel.Visible = value;
            }
        }

        public override void _Ready()
        {
            // Position in bottom-right corner
            Position = new Vector2(0, 0);
            Size = new Vector2(200, 300);
            
            // Create background panel
            _panel = new Panel
            {
                Size = Size,
                Position = Vector2.Zero
            };
            
            // Style the panel with semi-transparent background
            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.7f),
                BorderColor = new Color(1, 1, 1, 0.3f),
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            };
            styleBox.SetBorderWidthAll(2);
            _panel.AddThemeStyleboxOverride("panel", styleBox);
            AddChild(_panel);

            // Title
            _titleLabel = new Label
            {
                Text = "HEIGHT MAP",
                Position = new Vector2(10, 10),
                Size = new Vector2(180, 30),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _titleLabel.AddThemeColorOverride("font_color", Colors.White);
            _titleLabel.AddThemeFontSizeOverride("font_size", 18);
            _panel.AddChild(_titleLabel);

            // High label
            _highLabel = new Label
            {
                Text = "HIGH",
                Position = new Vector2(10, 45),
                Size = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _highLabel.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0));
            _highLabel.AddThemeFontSizeOverride("font_size", 14);
            _panel.AddChild(_highLabel);

            // Gradient bar
            _gradientBar = new ColorRect
            {
                Position = new Vector2(60, 70),
                Size = new Vector2(80, 180)
            };
            CreateGradientTexture();
            _panel.AddChild(_gradientBar);

            // Low label
            _lowLabel = new Label
            {
                Text = "LOW",
                Position = new Vector2(10, 255),
                Size = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lowLabel.AddThemeColorOverride("font_color", new Color(0.1f, 0.3f, 0.7f));
            _lowLabel.AddThemeFontSizeOverride("font_size", 14);
            _panel.AddChild(_lowLabel);

            _panel.Visible = _visible;
        }

        private void CreateGradientTexture()
        {
            // Create a gradient that matches the terrain colors
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(0.8f, 0.1f, 0.1f));      // Deep red (highest)
            gradient.SetColor(1, new Color(1.0f, 0.4f, 0.0f));       // Orange
            gradient.SetColor(2, new Color(1.0f, 0.85f, 0.0f));      // Yellow
            gradient.SetColor(3, new Color(0.5f, 0.85f, 0.3f));      // Light green
            gradient.SetColor(4, new Color(0.2f, 0.7f, 0.6f));       // Teal
            gradient.SetColor(5, new Color(0.1f, 0.3f, 0.7f));       // Deep blue (lowest)

            gradient.SetOffset(0, 0.0f);
            gradient.SetOffset(1, 0.2f);
            gradient.SetOffset(2, 0.4f);
            gradient.SetOffset(3, 0.6f);
            gradient.SetOffset(4, 0.8f);
            gradient.SetOffset(5, 1.0f);

            var gradientTexture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 80,
                Height = 180,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1)
            };

            var textureRect = new TextureRect
            {
                Texture = gradientTexture,
                Size = new Vector2(80, 180)
            };
            _gradientBar.AddChild(textureRect);
        }

        public override void _Process(double delta)
        {
            // Keep legend in bottom-right corner
            var viewportSize = GetViewportRect().Size;
            Position = new Vector2(viewportSize.X - 220, viewportSize.Y - 320);
        }
    }
}
