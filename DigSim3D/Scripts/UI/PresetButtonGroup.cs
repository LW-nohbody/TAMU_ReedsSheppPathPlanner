using Godot;
using System;
using System.Collections.Generic;

namespace DigSim3D.UI
{
    /// <summary>
    /// Quick preset button group (Slow/Medium/Fast, etc.)
    /// </summary>
    public partial class PresetButtonGroup : VBoxContainer
    {
        private Label _titleLabel = null!;
        private HBoxContainer _buttonContainer = null!;
        private List<Button> _buttons = new();
        private int _selectedIndex = -1;
        
        public string Title { get; set; } = "Presets";
        public List<(string name, float value)> Presets { get; set; } = new();
        
        public event Action<float> PresetSelected;
        
        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);
            
            // Title
            _titleLabel = new Label
            {
                Text = Title
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 12);
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
            AddChild(_titleLabel);
            
            // Button container
            _buttonContainer = new HBoxContainer();
            _buttonContainer.AddThemeConstantOverride("separation", 6);
            AddChild(_buttonContainer);
            
            // Create buttons for each preset
            for (int i = 0; i < Presets.Count; i++)
            {
                var preset = Presets[i];
                var button = CreatePresetButton(preset.name, preset.value, i);
                _buttons.Add(button);
                _buttonContainer.AddChild(button);
            }
        }
        
        private Button CreatePresetButton(string name, float value, int index)
        {
            var button = new Button
            {
                Text = name,
                CustomMinimumSize = new Vector2(70, 32)
            };
            
            // Style
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.5f);
            normalStyle.SetBorderWidthAll(1);
            normalStyle.SetCornerRadiusAll(6);
            button.AddThemeStyleboxOverride("normal", normalStyle);
            
            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.3f, 0.4f, 0.5f, 0.9f);
            hoverStyle.BorderColor = new Color(0.5f, 0.6f, 0.8f);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.SetCornerRadiusAll(6);
            button.AddThemeStyleboxOverride("hover", hoverStyle);
            
            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(0.4f, 0.6f, 0.9f, 1.0f);
            pressedStyle.BorderColor = new Color(0.5f, 0.7f, 1.0f);
            pressedStyle.SetBorderWidthAll(2);
            pressedStyle.SetCornerRadiusAll(6);
            button.AddThemeStyleboxOverride("pressed", pressedStyle);
            
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 11);
            
            button.Pressed += () => OnPresetButtonPressed(index, value, button);
            
            return button;
        }
        
        private void OnPresetButtonPressed(int index, float value, Button button)
        {
            _selectedIndex = index;
            PresetSelected?.Invoke(value);
            
            // Highlight selected button
            UpdateButtonStates();
            
            // Animate button press
            button.Scale = new Vector2(0.95f, 0.95f);
            var tween = CreateTween();
            tween.TweenProperty(button, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        
        private void UpdateButtonStates()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (i == _selectedIndex)
                {
                    _buttons[i].Modulate = new Color(1.2f, 1.2f, 1.0f);
                }
                else
                {
                    _buttons[i].Modulate = Colors.White;
                }
            }
        }
    }
}
