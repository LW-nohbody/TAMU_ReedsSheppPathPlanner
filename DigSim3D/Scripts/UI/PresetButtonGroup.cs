using Godot;
using System;
using System.Collections.Generic;

namespace DigSim3D.UI
{
    /// <summary>
    /// Quick preset buttons for common values
    /// </summary>
    public partial class PresetButtonGroup : HBoxContainer
    {
        public event Action<float>? PresetSelected;
        
        private List<Button> _buttons = new();

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);
            CustomMinimumSize = new Vector2(380, 36);
        }

        public void AddPreset(string label, float value)
        {
            var button = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(90, 32),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            
            // Professional button styling
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.20f, 0.22f, 0.28f, 0.9f);
            styleBox.BorderColor = new Color(0.30f, 0.34f, 0.40f, 0.9f);
            styleBox.SetBorderWidthAll(1);
            styleBox.SetCornerRadiusAll(4);
            button.AddThemeStyleboxOverride("normal", styleBox);
            
            var styleBoxHover = new StyleBoxFlat();
            styleBoxHover.BgColor = new Color(0.28f, 0.32f, 0.38f, 1.0f);
            styleBoxHover.BorderColor = new Color(0.45f, 0.55f, 0.65f, 1.0f);
            styleBoxHover.SetBorderWidthAll(1);
            styleBoxHover.SetCornerRadiusAll(4);
            button.AddThemeStyleboxOverride("hover", styleBoxHover);
            
            var styleBoxPressed = new StyleBoxFlat();
            styleBoxPressed.BgColor = new Color(0.35f, 0.75f, 0.45f, 0.9f);
            styleBoxPressed.BorderColor = new Color(0.40f, 0.85f, 0.50f, 1.0f);
            styleBoxPressed.SetBorderWidthAll(1);
            styleBoxPressed.SetCornerRadiusAll(4);
            button.AddThemeStyleboxOverride("pressed", styleBoxPressed);
            
            button.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
            button.AddThemeFontSizeOverride("font_size", 11);
            
            button.Pressed += () => OnPresetPressed(value);
            AddChild(button);
            _buttons.Add(button);
        }

        private void OnPresetPressed(float value)
        {
            PresetSelected?.Invoke(value);
        }
    }
}
