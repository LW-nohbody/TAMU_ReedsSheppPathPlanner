using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium slider with glow effect and color-coded values.
    /// Exposes MinValue/MaxValue/Value, and keeps the internal HSlider in sync.
    /// </summary>
    public partial class PremiumSlider : VBoxContainer
    {
        private Label _label = null!;
        private HSlider _slider = null!;
        private Label _valueLabel = null!;

        private float _minValue = 0f;
        private float _maxValue = 100f;
        private float _value = 50f;

        public float MinValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                if (_slider != null)
                    _slider.MinValue = value;
            }
        }

        public float MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                if (_slider != null)
                    _slider.MaxValue = value;
            }
        }

        public float Value
        {
            get => _value;
            set
            {
                _value = value;

                if (_slider != null)
                    _slider.Value = value;

                CurrentValue = value;

                if (_valueLabel != null)
                    _valueLabel.Text = $"{CurrentValue:F2}";
            }
        }

        public float CurrentValue { get; private set; } = 50f;

        public event Action<double>? ValueChanged;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(350, 60);
            AddThemeConstantOverride("separation", 5);

            // Label
            _label = new Label
            {
                Text = "Value",
                Modulate = Colors.White
            };
            _label.AddThemeFontSizeOverride("font_size", 12);
            _label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            AddChild(_label);

            // Horizontal container
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            AddChild(hbox);

            // Slider (use backing fields so DigSimUI changes before/after _Ready work)
            _slider = new HSlider
            {
                MinValue = _minValue,
                MaxValue = _maxValue,
                Value = _value,
                Step = (_maxValue - _minValue) / 100f,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(280, 24)
            };

            CurrentValue = _value;

            var grabberStyleBox = new StyleBoxFlat();
            grabberStyleBox.BgColor = new Color(0.4f, 0.7f, 1.0f, 1.0f);
            grabberStyleBox.SetCornerRadiusAll(6);
            _slider.AddThemeStyleboxOverride("grabber_area", grabberStyleBox);

            _slider.ValueChanged += OnSliderValueChanged;
            hbox.AddChild(_slider);

            // Value label
            _valueLabel = new Label
            {
                Text = $"{CurrentValue:F2}",
                CustomMinimumSize = new Vector2(60, 24),
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = new Color(0.7f, 1.0f, 0.8f)
            };
            _valueLabel.AddThemeFontSizeOverride("font_size", 12);
            _valueLabel.AddThemeColorOverride("font_color", Colors.White);
            hbox.AddChild(_valueLabel);
        }

        private void OnSliderValueChanged(double value)
        {
            
            CurrentValue = (float)value;
            _value = CurrentValue;

            if (_valueLabel != null)
                _valueLabel.Text = $"{CurrentValue:F2}";

            // Color based on current actual range
            float min = (float)_slider.MinValue;
            float max = (float)_slider.MaxValue;
            float normalized = (CurrentValue - min) / (max - min);
            normalized = Mathf.Clamp(normalized, 0f, 1f);

            Color valueColor =
                normalized < 0.33f ? new Color(0.3f, 0.8f, 0.5f) :
                normalized < 0.66f ? new Color(0.8f, 0.8f, 0.3f) :
                                     new Color(0.8f, 0.3f, 0.3f);

            _valueLabel.Modulate = valueColor;

            ValueChanged?.Invoke(value);
        }

        public void SetLabel(string text)
        {
            if (_label != null)
                _label.Text = text;
        }

        /// <summary>
        /// Convenience: set min/max/value in one shot.
        /// Used by DigSimUI.SetDigConfig so the sliders reflect DigConfig on startup.
        /// </summary>
        public void Apply(float min, float max, float value)
        {
            MinValue = min;
            MaxValue = max;
            Value = Mathf.Clamp(value, min, max);
        }
    }
}