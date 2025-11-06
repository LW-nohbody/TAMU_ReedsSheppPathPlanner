using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Label that smoothly animates value changes
    /// </summary>
    public partial class AnimatedValueLabel : Label
    {
        private float _targetValue = 0f;
        private float _currentValue = 0f;
        private float _animationSpeed = 2.0f;
        private string _textFormat = "{0:F2}";
        private string _prefix = "";
        private string _suffix = "";

        public AnimatedValueLabel()
        {
            Modulate = Colors.White;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void SetValue(float value, string format = "{0:F2}")
        {
            _targetValue = value;
            _textFormat = format;
        }

        public new void SetText(string text)
        {
            Text = text;
        }

        public void SetFontSize(int size)
        {
            AddThemeFontSizeOverride("font_size", size);
        }

        public void SetColor(Color color)
        {
            Modulate = color;
            AddThemeColorOverride("font_color", color);
        }

        public override void _Process(double delta)
        {
            // Smooth interpolation
            float dt = (float)delta;
            if (Mathf.Abs(_currentValue - _targetValue) > 0.01f)
            {
                _currentValue = Mathf.Lerp(_currentValue, _targetValue, _animationSpeed * dt);
                Text = _prefix + string.Format(_textFormat, _currentValue) + _suffix;
            }
            else
            {
                _currentValue = _targetValue;
            }
        }
    }
}
