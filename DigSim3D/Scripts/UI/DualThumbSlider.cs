using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// A custom dual-thumb slider for setting min/max ranges
    /// Useful for defining acceptable parameter ranges with visual feedback
    /// </summary>
    public partial class DualThumbSlider : Control
    {
        [Signal]
        public delegate void RangeChangedEventHandler(float minValue, float maxValue);
        
        private float _minValue = 0f;
        private float _maxValue = 100f;
        private float _currentMin = 25f;
        private float _currentMax = 75f;
        
        private bool _draggingMin = false;
        private bool _draggingMax = false;
        
        private Color _accentColor = new Color(0.3f, 0.6f, 0.9f);
        private Color _rangeColor = new Color(0.3f, 0.6f, 0.9f, 0.3f);
        
        private const float ThumbRadius = 10f;
        private const float TrackHeight = 6f;
        
        public float MinValue
        {
            get => _minValue;
            set { _minValue = value; QueueRedraw(); }
        }
        
        public float MaxValue
        {
            get => _maxValue;
            set { _maxValue = value; QueueRedraw(); }
        }
        
        public float CurrentMin
        {
            get => _currentMin;
            set 
            { 
                _currentMin = Mathf.Clamp(value, _minValue, _currentMax);
                QueueRedraw();
                EmitSignal(SignalName.RangeChanged, _currentMin, _currentMax);
            }
        }
        
        public float CurrentMax
        {
            get => _currentMax;
            set 
            { 
                _currentMax = Mathf.Clamp(value, _currentMin, _maxValue);
                QueueRedraw();
                EmitSignal(SignalName.RangeChanged, _currentMin, _currentMax);
            }
        }
        
        public Color AccentColor
        {
            get => _accentColor;
            set 
            { 
                _accentColor = value;
                _rangeColor = new Color(value.R, value.G, value.B, 0.3f);
                QueueRedraw();
            }
        }
        
        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(200, 32);
            MouseFilter = MouseFilterEnum.Stop;
        }
        
        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size);
            float trackY = rect.Size.Y / 2f;
            float trackWidth = rect.Size.X - ThumbRadius * 2f;
            float trackX = ThumbRadius;
            
            // Draw background track
            DrawRect(new Rect2(trackX, trackY - TrackHeight / 2f, trackWidth, TrackHeight),
                new Color(0.2f, 0.22f, 0.26f, 0.9f));
            
            // Calculate thumb positions
            float minThumbX = trackX + ((_currentMin - _minValue) / (_maxValue - _minValue)) * trackWidth;
            float maxThumbX = trackX + ((_currentMax - _minValue) / (_maxValue - _minValue)) * trackWidth;
            
            // Draw range highlight (between thumbs)
            DrawRect(new Rect2(minThumbX, trackY - TrackHeight / 2f, maxThumbX - minThumbX, TrackHeight),
                _rangeColor);
            
            // Draw filled portion for min thumb
            DrawRect(new Rect2(trackX, trackY - TrackHeight / 2f, minThumbX - trackX, TrackHeight),
                new Color(_accentColor.R * 0.5f, _accentColor.G * 0.5f, _accentColor.B * 0.5f, 0.5f));
            
            // Draw min thumb
            DrawCircle(new Vector2(minThumbX, trackY), ThumbRadius, _accentColor);
            DrawCircle(new Vector2(minThumbX, trackY), ThumbRadius - 2f, new Color(0.15f, 0.16f, 0.19f));
            
            // Draw max thumb
            DrawCircle(new Vector2(maxThumbX, trackY), ThumbRadius, _accentColor);
            DrawCircle(new Vector2(maxThumbX, trackY), ThumbRadius - 2f, new Color(0.15f, 0.16f, 0.19f));
            
            // Draw labels
            var font = ThemeDB.FallbackFont;
            int fontSize = 10;
            
            DrawString(font, new Vector2(minThumbX - 15, trackY + ThumbRadius + 15),
                $"{_currentMin:F1}", HorizontalAlignment.Center, -1, fontSize, new Color(0.7f, 0.75f, 0.82f));
            DrawString(font, new Vector2(maxThumbX - 15, trackY + ThumbRadius + 15),
                $"{_currentMax:F1}", HorizontalAlignment.Center, -1, fontSize, new Color(0.7f, 0.75f, 0.82f));
        }
        
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    var rect = new Rect2(Vector2.Zero, Size);
                    float trackY = rect.Size.Y / 2f;
                    float trackWidth = rect.Size.X - ThumbRadius * 2f;
                    float trackX = ThumbRadius;
                    
                    float minThumbX = trackX + ((_currentMin - _minValue) / (_maxValue - _minValue)) * trackWidth;
                    float maxThumbX = trackX + ((_currentMax - _minValue) / (_maxValue - _minValue)) * trackWidth;
                    
                    var mousePos = mouseButton.Position;
                    
                    if (mouseButton.Pressed)
                    {
                        // Check if clicking on min thumb
                        if (mousePos.DistanceTo(new Vector2(minThumbX, trackY)) < ThumbRadius * 1.5f)
                        {
                            _draggingMin = true;
                        }
                        // Check if clicking on max thumb
                        else if (mousePos.DistanceTo(new Vector2(maxThumbX, trackY)) < ThumbRadius * 1.5f)
                        {
                            _draggingMax = true;
                        }
                    }
                    else
                    {
                        _draggingMin = false;
                        _draggingMax = false;
                    }
                }
            }
            else if (@event is InputEventMouseMotion mouseMotion)
            {
                if (_draggingMin || _draggingMax)
                {
                    var rect = new Rect2(Vector2.Zero, Size);
                    float trackWidth = rect.Size.X - ThumbRadius * 2f;
                    float trackX = ThumbRadius;
                    
                    float normalizedPos = Mathf.Clamp((mouseMotion.Position.X - trackX) / trackWidth, 0f, 1f);
                    float newValue = _minValue + normalizedPos * (_maxValue - _minValue);
                    
                    if (_draggingMin)
                    {
                        CurrentMin = newValue;
                    }
                    else if (_draggingMax)
                    {
                        CurrentMax = newValue;
                    }
                }
            }
        }
    }
}
