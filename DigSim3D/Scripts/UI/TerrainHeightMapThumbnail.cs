using Godot;
using System;

namespace DigSim3D.UI
{
    /// <summary>
    /// Small thumbnail showing terrain elevation as a heat map
    /// </summary>
    public partial class TerrainHeightMapThumbnail : Control
    {
        private Image _heightMapImage = null!;
        private ImageTexture _heightMapTexture = null!;
        private TextureRect _textureRect = null!;
        
        private int _thumbnailSize = 128;
        private float[,] _heightData = null!;
        
        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(_thumbnailSize, _thumbnailSize);
            
            // Create texture rect for display
            _textureRect = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale
            };
            _textureRect.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_textureRect);
            
            // Border
            var borderPanel = new Panel();
            borderPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            borderPanel.MouseFilter = MouseFilterEnum.Ignore;
            
            var borderStyle = new StyleBoxFlat();
            borderStyle.DrawCenter = false;
            borderStyle.BorderColor = new Color(0.4f, 0.6f, 0.8f);
            borderStyle.SetBorderWidthAll(2);
            borderPanel.AddThemeStyleboxOverride("panel", borderStyle);
            AddChild(borderPanel);
        }
        
        public void UpdateHeightMap(float[,] heightData, int gridSize)
        {
            _heightData = heightData;
            GenerateHeightMapImage(gridSize);
        }
        
        private void GenerateHeightMapImage(int gridSize)
        {
            if (_heightData == null) return;
            
            _heightMapImage = Image.CreateEmpty(_thumbnailSize, _thumbnailSize, false, Image.Format.Rgb8);
            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            
            // Find height range
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    float h = _heightData[x, y];
                    if (h < minHeight) minHeight = h;
                    if (h > maxHeight) maxHeight = h;
                }
            }
            
            float heightRange = maxHeight - minHeight;
            if (heightRange < 0.01f) heightRange = 1f;
            
            // Generate image
            for (int py = 0; py < _thumbnailSize; py++)
            {
                for (int px = 0; px < _thumbnailSize; px++)
                {
                    // Sample from height data
                    float tx = (float)px / _thumbnailSize;
                    float ty = (float)py / _thumbnailSize;
                    
                    int dataX = (int)(tx * (gridSize - 1));
                    int dataY = (int)(ty * (gridSize - 1));
                    
                    float h = _heightData[dataX, dataY];
                    float normalized = (h - minHeight) / heightRange;
                    
                    // Color based on height (heat map)
                    Color pixelColor = GetHeightColor(normalized);
                    _heightMapImage.SetPixel(px, py, pixelColor);
                }
            }
            
            // Update texture
            if (_heightMapTexture == null)
            {
                _heightMapTexture = ImageTexture.CreateFromImage(_heightMapImage);
            }
            else
            {
                _heightMapTexture.Update(_heightMapImage);
            }
            
            _textureRect.Texture = _heightMapTexture;
        }
        
        private Color GetHeightColor(float normalized)
        {
            // Blue (low) -> Green (mid) -> Yellow -> Red (high)
            if (normalized < 0.33f)
            {
                // Blue to Cyan
                float t = normalized / 0.33f;
                return new Color(0, t, 1);
            }
            else if (normalized < 0.66f)
            {
                // Cyan to Green to Yellow
                float t = (normalized - 0.33f) / 0.33f;
                return new Color(t, 1, 1 - t);
            }
            else
            {
                // Yellow to Red
                float t = (normalized - 0.66f) / 0.34f;
                return new Color(1, 1 - t, 0);
            }
        }
    }
}
