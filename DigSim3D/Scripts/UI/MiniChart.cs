using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigSim3D.UI
{
    /// <summary>
    /// Small line chart showing historical data
    /// </summary>
    public partial class MiniChart : Control
    {
        private List<float> _dataPoints = new();
        private int _maxPoints = 50;
        private Color _lineColor = new Color(0.4f, 0.7f, 1.0f);

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(350, 25);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void AddDataPoint(float value)
        {
            _dataPoints.Add(Mathf.Clamp(value, 0f, 1f));
            if (_dataPoints.Count > _maxPoints)
            {
                _dataPoints.RemoveAt(0);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_dataPoints.Count < 2) return;
            
            var size = Size;
            float stepX = size.X / (_maxPoints - 1);
            float startIndex = Mathf.Max(0, _dataPoints.Count - _maxPoints);
            
            for (int i = 1; i < _dataPoints.Count; i++)
            {
                float x1 = (i - 1) * stepX;
                float y1 = size.Y - (_dataPoints[i - 1] * size.Y);
                float x2 = i * stepX;
                float y2 = size.Y - (_dataPoints[i] * size.Y);
                
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), _lineColor, 2f);
            }
        }
    }
}
