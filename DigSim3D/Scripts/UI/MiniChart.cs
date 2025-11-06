using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigSim3D.UI
{
    /// <summary>
    /// Small line chart for visualizing robot metrics over time
    /// </summary>
    public partial class MiniChart : Control
    {
        private List<float> _dataPoints = new();
        private int _maxDataPoints = 50;
        private float _minValue = 0f;
        private float _maxValue = 100f;
        private Color _lineColor = new Color(0.4f, 0.8f, 1.0f);
        private Color _fillColor = new Color(0.4f, 0.8f, 1.0f, 0.2f);
        
        public string ChartTitle { get; set; } = "Chart";
        
        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(200, 80);
            MouseFilter = MouseFilterEnum.Ignore;
        }
        
        public void AddDataPoint(float value)
        {
            _dataPoints.Add(value);
            
            // Keep only the last N points
            if (_dataPoints.Count > _maxDataPoints)
            {
                _dataPoints.RemoveAt(0);
            }
            
            QueueRedraw();
        }
        
        public void SetValueRange(float min, float max)
        {
            _minValue = min;
            _maxValue = max;
        }
        
        public void SetColor(Color lineColor, Color fillColor)
        {
            _lineColor = lineColor;
            _fillColor = fillColor;
        }
        
        public override void _Draw()
        {
            if (_dataPoints.Count < 2) return;
            
            var size = Size;
            var padding = 5f;
            var chartWidth = size.X - padding * 2;
            var chartHeight = size.Y - padding * 2;
            
            // Draw background
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.05f, 0.05f, 0.1f, 0.5f));
            
            // Calculate points
            var points = new List<Vector2>();
            float stepX = chartWidth / (_maxDataPoints - 1);
            
            for (int i = 0; i < _dataPoints.Count; i++)
            {
                float t = (float)i / (_dataPoints.Count - 1);
                float x = padding + t * chartWidth;
                
                float normalizedValue = (_dataPoints[i] - _minValue) / (_maxValue - _minValue);
                normalizedValue = Mathf.Clamp(normalizedValue, 0f, 1f);
                float y = padding + chartHeight - (normalizedValue * chartHeight);
                
                points.Add(new Vector2(x, y));
            }
            
            // Draw filled area under line
            if (points.Count >= 2)
            {
                var fillPoints = new List<Vector2>(points);
                fillPoints.Add(new Vector2(points[points.Count - 1].X, size.Y - padding));
                fillPoints.Add(new Vector2(points[0].X, size.Y - padding));
                
                DrawColoredPolygon(fillPoints.ToArray(), _fillColor);
            }
            
            // Draw line
            for (int i = 0; i < points.Count - 1; i++)
            {
                DrawLine(points[i], points[i + 1], _lineColor, 2f, true);
            }
            
            // Draw current value point
            if (points.Count > 0)
            {
                var lastPoint = points[points.Count - 1];
                DrawCircle(lastPoint, 4f, _lineColor);
                DrawCircle(lastPoint, 3f, Colors.White);
            }
            
            // Draw border
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.3f, 0.3f, 0.4f), false, 1f);
        }
    }
}
