using Godot;

public partial class Gizmo2D : Node2D
{
    [Export] public Color Color = new Color(1, 0, 0); // red by default
    [Export] public float Radius = 8f;
    [Export] public float ArrowLen = 30f;

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, Color);
        var tip = new Vector2(ArrowLen, 0);
        DrawLine(Vector2.Zero, tip, Color, 2);
        // little V at the tip
        var left = tip + new Vector2(-8, -6).Rotated(0);
        var right = tip + new Vector2(-8, 6).Rotated(0);
        DrawLine(tip, left, Color, 2);
        DrawLine(tip, right, Color, 2);
    }

    public override void _Process(double delta) => QueueRedraw();
}
