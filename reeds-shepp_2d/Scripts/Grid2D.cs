using Godot;
using System;

public partial class Grid2D : Node2D
{
    [Export] public int CellSize = 50;                        // visual spacing in pixels
    [Export] public Color GridColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
    [Export] public int LineWidth = 1;

    // Link your SidePanel here so we can account for its width
    [Export] public NodePath SidePanelPath;

    // Keep this equal to World.WorldOriginOffset
    [Export] public Vector2 OriginOffset = new Vector2(100, 100);

    // Styling for origin axes
    [Export] public Color AxisColor = new Color(0.95f, 0.25f, 0.25f, 0.9f);
    [Export] public int AxisWidth = 2;

    private Control _sidePanel;

    public override void _Ready()
    {
        _sidePanel = GetNodeOrNull<Control>(SidePanelPath);

        // Robust fallback: search the scene tree for a node literally named "SidePanel"
        if (_sidePanel == null)
        {
            _sidePanel = GetTree()?.Root?.FindChild("SidePanel", recursive: true, owned: false) as Control;
            if (_sidePanel == null)
                GD.PrintErr("Grid2D: SidePanel not set/found. Grid will start at 0 + OriginOffset.");
        }
    }

    private int FirstLineAtOrBefore(float origin, int cell, float from)
    {
        return (int)(Math.Floor((from - origin) / cell) * cell + origin);
    }

    public override void _Draw()
    {
        var vp = GetViewportRect();
        float panelW = _sidePanel != null ? _sidePanel.Size.X : 0f;

        // Global/screen-space origin for grid (right edge of panel + offset)
        float originX = panelW + OriginOffset.X;
        float originY = OriginOffset.Y;

        // Vertical lines: x = originX + n*CellSize
        int startX = FirstLineAtOrBefore(originX, CellSize, 0);
        for (int x = startX; x < vp.Size.X; x += CellSize)
            DrawLine(new Vector2(x, 0), new Vector2(x, vp.Size.Y), GridColor, LineWidth);

        // Horizontal lines: y = originY + m*CellSize
        int startY = FirstLineAtOrBefore(originY, CellSize, 0);
        for (int y = startY; y < vp.Size.Y; y += CellSize)
            DrawLine(new Vector2(0, y), new Vector2(vp.Size.X, y), GridColor, LineWidth);

        // Origin axes (distinct color)
        DrawLine(new Vector2(originX, 0),       new Vector2(originX, vp.Size.Y), AxisColor, AxisWidth);
        DrawLine(new Vector2(0, originY),       new Vector2(vp.Size.X, originY), AxisColor, AxisWidth);

        // Crosshair at exact origin
        DrawLine(new Vector2(originX - 6, originY), new Vector2(originX + 6, originY), AxisColor, AxisWidth);
        DrawLine(new Vector2(originX, originY - 6), new Vector2(originX, originY + 6), AxisColor, AxisWidth);
    }

    public override void _Process(double delta) => QueueRedraw();
}