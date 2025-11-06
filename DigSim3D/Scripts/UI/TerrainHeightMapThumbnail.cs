using Godot;
using System;
using DigSim3D.App;

namespace DigSim3D.UI
{
    /// <summary>
    /// Small thumbnail showing terrain height map
    /// </summary>
    public partial class TerrainHeightMapThumbnail : Control
    {
        private float _progress = 0f;
        private TerrainDisk? _terrain = null;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(350, 70);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void UpdateProgress(float progress)
        {
            _progress = Mathf.Clamp(progress, 0f, 1f);
            QueueRedraw();
        }

        public void SetTerrain(TerrainDisk terrain)
        {
            _terrain = terrain;
        }

        public override void _Draw()
        {
            var size = Size;
            
            // Background
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.1f, 0.1f, 0.15f, 0.8f));
            
            // Simple height visualization (grid)
            if (_terrain != null && _terrain.HeightGrid != null)
            {
                int resolution = _terrain.Resolution;
                float cellWidth = size.X / resolution;
                float cellHeight = size.Y / resolution;
                
                for (int x = 0; x < Mathf.Min(resolution, 20); x++)
                {
                    for (int z = 0; z < Mathf.Min(resolution, 20); z++)
                    {
                        float height = _terrain.HeightGrid[x, z];
                        float normalizedHeight = Mathf.Clamp(height / 5f, 0f, 1f);
                        
                        Color color = new Color(
                            0.2f + normalizedHeight * 0.6f,
                            0.3f + normalizedHeight * 0.4f,
                            0.2f,
                            0.8f
                        );
                        
                        DrawRect(new Rect2(
                            x * cellWidth, 
                            z * cellHeight, 
                            cellWidth - 1, 
                            cellHeight - 1
                        ), color);
                    }
                }
            }
            else
            {
                // Placeholder gradient
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    Color color = new Color(0.3f, 0.5f + t * 0.3f, 0.3f, 0.6f);
                    DrawRect(new Rect2(0, i * size.Y / 20f, size.X, size.Y / 20f), color);
                }
            }
            
            // Progress overlay
            DrawRect(new Rect2(0, 0, size.X * _progress, 5), new Color(0.3f, 0.8f, 0.5f, 0.9f));
            
            // Border
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.4f, 0.6f, 0.8f, 0.5f), false, 2f);
            
            // Label
            DrawString(ThemeDB.FallbackFont, new Vector2(10, 20), 
                $"Terrain Progress: {_progress * 100f:F0}%", 
                HorizontalAlignment.Left, -1, 12, Colors.White);
        }
    }
}
