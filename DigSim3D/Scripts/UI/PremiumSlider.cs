using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium slider with glow effect and color-coded values
    /// </summary>
    public partial class PremiumSlider : VBoxContainer
    {
        private Label _label = null!;
        private HSlider _slider = null!;
        private Label _valueLabel = null!;
        
        public float MinValue { get; set; } = 0f;
        public float MaxValue { get; set; } = 100f;
        public float Value { get; set; } = 50f;
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
            
            // Horizontal container for slider and value
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            AddChild(hbox);
            
            // Slider
            _slider = new HSlider
            {
                MinValue = MinValue,
                MaxValue = MaxValue,
                Value = Value,  // Use the Value property
                Step = (MaxValue - MinValue) / 100f,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(280, 24)
            };
            
            CurrentValue = Value;  // Initialize CurrentValue
            
            // Style the slider
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
            _valueLabel.Text = $"{CurrentValue:F2}";
            
            // Color code based on range
            float normalized = (CurrentValue - MinValue) / (MaxValue - MinValue);
            Color valueColor;
            if (normalized < 0.33f)
                valueColor = new Color(0.3f, 0.8f, 0.5f); // Green
            else if (normalized < 0.66f)
                valueColor = new Color(0.8f, 0.8f, 0.3f); // Yellow
            else
                valueColor = new Color(0.8f, 0.3f, 0.3f); // Red
                
            _valueLabel.Modulate = valueColor;
            
            ValueChanged?.Invoke(value);
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.Text = text;
        }
    }
}
