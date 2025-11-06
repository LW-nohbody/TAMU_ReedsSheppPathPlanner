using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium slider with glow effect when active/changing
    /// </summary>
    public partial class PremiumSlider : VBoxContainer
    {
        private Label _nameLabel = null!;
        private HBoxContainer _sliderContainer = null!;
        private HSlider _slider = null!;
        private AnimatedValueLabel _valueLabel = null!;
        private Panel _glowPanel = null!;
        
        public string LabelText { get; set; } = "Setting";
        public float MinValue { get; set; } = 0f;
        public float MaxValue { get; set; } = 100f;
        public float Step { get; set; } = 1f;
        public float CurrentValue { get; private set; } = 50f;
        public string ValueFormat { get; set; } = "F1";
        public string ValueSuffix { get; set; } = "";
        
        public event Action<float> ValueChanged;
        
        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 5);
            
            // Title label
            _nameLabel = new Label
            {
                Text = LabelText
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 13);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1.0f));
            AddChild(_nameLabel);
            
            // Glow panel behind slider
            _glowPanel = new Panel
            {
                CustomMinimumSize = new Vector2(0, 32),
                Modulate = new Color(1, 1, 1, 0) // Start invisible
            };
            
            var glowStyle = new StyleBoxFlat();
            glowStyle.BgColor = new Color(0.4f, 0.6f, 1.0f, 0.2f);
            glowStyle.BorderColor = new Color(0.4f, 0.6f, 1.0f, 0.6f);
            glowStyle.SetBorderWidthAll(2);
            glowStyle.SetCornerRadiusAll(6);
            glowStyle.ShadowColor = new Color(0.4f, 0.6f, 1.0f, 0.8f);
            glowStyle.ShadowSize = 12;
            _glowPanel.AddThemeStyleboxOverride("panel", glowStyle);
            AddChild(_glowPanel);
            
            // Slider container
            _sliderContainer = new HBoxContainer();
            _sliderContainer.AddThemeConstantOverride("separation", 10);
            _sliderContainer.Position = new Vector2(0, _glowPanel.Position.Y);
            AddChild(_sliderContainer);
            
            // Slider
            _slider = new HSlider
            {
                MinValue = MinValue,
                MaxValue = MaxValue,
                Step = Step,
                Value = CurrentValue,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(200, 24)
            };
            _slider.ValueChanged += OnSliderValueChanged;
            _slider.DragStarted += OnDragStarted;
            _slider.DragEnded += OnDragEnded;
            _sliderContainer.AddChild(_slider);
            
            // Value display
            _valueLabel = new AnimatedValueLabel
            {
                LabelText = "",
                ValueFormat = ValueFormat,
                ValueSuffix = ValueSuffix,
                CustomMinimumSize = new Vector2(80, 0)
            };
            _valueLabel.SetValueInstant(CurrentValue);
            _sliderContainer.AddChild(_valueLabel);
        }
        
        private void OnSliderValueChanged(double value)
        {
            CurrentValue = (float)value;
            _valueLabel.SetValue(CurrentValue);
            ValueChanged?.Invoke(CurrentValue);
            
            // Pulse glow on change
            AnimateGlow();
        }
        
        private void OnDragStarted()
        {
            // Show strong glow when dragging
            var tween = CreateTween();
            tween.TweenProperty(_glowPanel, "modulate:a", 1.0f, 0.2);
        }
        
        private void OnDragEnded(bool valueChanged)
        {
            // Fade glow when done dragging
            var tween = CreateTween();
            tween.TweenProperty(_glowPanel, "modulate:a", 0.0f, 0.5);
        }
        
        private void AnimateGlow()
        {
            _glowPanel.Modulate = new Color(1, 1, 1, 0.8f);
            
            var tween = CreateTween();
            tween.TweenProperty(_glowPanel, "modulate:a", 0.0f, 0.4);
        }
        
        public void SetValue(float value)
        {
            _slider.Value = value;
            CurrentValue = value;
            _valueLabel.SetValueInstant(value);
        }
    }
}
