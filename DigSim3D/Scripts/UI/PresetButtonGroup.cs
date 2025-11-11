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
            CustomMinimumSize = new Vector2(350, 35);
        }

        public void AddPreset(string label, float value)
        {
            var button = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(80, 30),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            
            // Style
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.2f, 0.3f, 0.5f, 0.8f);
            styleBox.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f);
            styleBox.SetBorderWidthAll(1);
            styleBox.SetCornerRadiusAll(4);
            button.AddThemeStyleboxOverride("normal", styleBox);
            
            var styleBoxHover = new StyleBoxFlat();
            styleBoxHover.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
            styleBoxHover.BorderColor = new Color(0.5f, 0.7f, 1.0f, 1.0f);
            styleBoxHover.SetBorderWidthAll(2);
            styleBoxHover.SetCornerRadiusAll(4);
            button.AddThemeStyleboxOverride("hover", styleBoxHover);
            
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
