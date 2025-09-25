using Godot;

public partial class Grid2D : Node2D
{
    [Export] public int CellSize = 100;   // spacing in pixels
    [Export] public Color GridColor = new Color(0.6f, 0.6f, 0.6f, 0.5f); // light gray, semi-transparent
    [Export] public int LineWidth = 1;

    public override void _Draw()
    {
        var viewport = GetViewportRect();

        // Vertical lines
        for (int x = 0; x < viewport.Size.X; x += CellSize)
            DrawLine(new Vector2(x, 0), new Vector2(x, viewport.Size.Y), GridColor, LineWidth);

        // Horizontal lines
        for (int y = 0; y < viewport.Size.Y; y += CellSize)
            DrawLine(new Vector2(0, y), new Vector2(viewport.Size.X, y), GridColor, LineWidth);
    }

    public override void _Process(double delta)
    {
        QueueRedraw(); // redraw continuously in case of resize
    }
}