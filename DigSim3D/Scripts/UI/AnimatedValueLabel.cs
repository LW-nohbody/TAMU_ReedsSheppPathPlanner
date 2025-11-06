using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Label that smoothly animates value changes with color coding
    /// </summary>
    public partial class AnimatedValueLabel : HBoxContainer
    {
        private Label _nameLabel = null!;
        private Label _valueLabel = null!;
        private float _currentValue = 0f;
        private float _targetValue = 0f;
        private float _animationSpeed = 5.0f;
        
        // Color coding ranges
        private float _minValue = 0f;
        private float _maxValue = 100f;
        private float _optimalMin = 40f;
        private float _optimalMax = 60f;
        
        public string LabelText { get; set; } = "Value";
        public string ValueFormat { get; set; } = "F2";
        public string ValueSuffix { get; set; } = "";
        
        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);
            
            // Name label
            _nameLabel = new Label
            {
                Text = LabelText,
                CustomMinimumSize = new Vector2(120, 0)
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 12);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            AddChild(_nameLabel);
            
            // Value label with glow effect
            _valueLabel = new Label
            {
                Text = "0.00",
                CustomMinimumSize = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _valueLabel.AddThemeFontSizeOverride("font_size", 14);
            AddChild(_valueLabel);
        }
        
        public override void _Process(double delta)
        {
            if (Mathf.Abs(_currentValue - _targetValue) > 0.01f)
            {
                // Smooth transition
                _currentValue = Mathf.Lerp(_currentValue, _targetValue, _animationSpeed * (float)delta);
                UpdateValueDisplay();
            }
        }
        
        public void SetValue(float value)
        {
            _targetValue = value;
        }
        
        public void SetValueInstant(float value)
        {
            _currentValue = value;
            _targetValue = value;
            UpdateValueDisplay();
        }
        
        public void SetColorRanges(float min, float max, float optimalMin, float optimalMax)
        {
            _minValue = min;
            _maxValue = max;
            _optimalMin = optimalMin;
            _optimalMax = optimalMax;
        }
        
        private void UpdateValueDisplay()
        {
            _valueLabel.Text = _currentValue.ToString(ValueFormat) + ValueSuffix;
            
            // Color code based on value range
            Color valueColor;
            if (_currentValue >= _optimalMin && _currentValue <= _optimalMax)
            {
                // Optimal range - Green
                valueColor = new Color(0.3f, 1.0f, 0.3f);
            }
            else if (_currentValue < _optimalMin || _currentValue > _optimalMax)
            {
                // Moderate range - Yellow
                float deviation = Mathf.Min(
                    Mathf.Abs(_currentValue - _optimalMin),
                    Mathf.Abs(_currentValue - _optimalMax)
                ) / (_maxValue - _minValue);
                
                if (deviation > 0.3f)
                {
                    // Extreme range - Red
                    valueColor = new Color(1.0f, 0.3f, 0.3f);
                }
                else
                {
                    // Moderate - Yellow
                    valueColor = new Color(1.0f, 1.0f, 0.3f);
                }
            }
            else
            {
                valueColor = Colors.White;
            }
            
            _valueLabel.AddThemeColorOverride("font_color", valueColor);
            
            // Add subtle glow effect on change
            if (Mathf.Abs(_currentValue - _targetValue) > 0.1f)
            {
                _valueLabel.Modulate = new Color(1.3f, 1.3f, 1.3f);
                
                var tween = CreateTween();
                tween.TweenProperty(_valueLabel, "modulate", Colors.White, 0.3);
            }
        }
    }
}
