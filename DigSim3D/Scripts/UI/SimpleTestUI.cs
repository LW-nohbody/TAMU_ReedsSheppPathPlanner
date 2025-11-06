using Godot;

namespace DigSim3D.UI
{
    public partial class SimpleTestUI : Control
    {
        public override void _Ready()
        {
            // Anchor to top-left
            AnchorRight = 0;
            AnchorBottom = 0;
            OffsetLeft = 20;
            OffsetTop = 20;
            OffsetRight = 220; // 200px width
            OffsetBottom = 70; // 50px height

            // Add a semi-transparent panel to make it visible
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.5f); // Black, 50% transparent
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);

            // Create a default theme to ensure visibility
            var theme = new Theme();
            theme.DefaultFont = new SystemFont { FontNames = new[] { "Arial" } };
            theme.DefaultFontSize = 16;
            
            // Add a label
            var label = new Label
            {
                Theme = theme,
                Text = "UI Test: Is this visible?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            panel.AddChild(label);

            GD.Print("[SimpleTestUI] Minimal UI is ready.");
        }
    }
}
