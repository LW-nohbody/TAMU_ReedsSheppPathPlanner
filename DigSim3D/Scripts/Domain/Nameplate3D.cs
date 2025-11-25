using Godot;

/// <summary>
/// Creates the name tags for each robot displayed in the simulation
/// </summary>
public partial class Nameplate3D : Node3D
{
    [Export] public string Text = "RS-00";
    [Export] public Color FontColor = Colors.White;
    [Export] public float HeightOffset = 1.6f;     // vertical offset above vehicle origin
    [Export] public bool YBillboardOnly = true;    // rotate only around Y
    [Export] public bool FixedSize = true;         // keep text readable with distance
    [Export] public float PixelSize = 0.01f;       // used when FixedSize=true (tweak to taste)
    [Export] public int FontSize = 36;             // label font size (if using default theme)

    private Label3D _label = null!;

    public override void _Ready()
    {
        _label = new Label3D
        {
            Text = Text,
            Modulate = FontColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FixedSize = FixedSize,
            // PixelSize is honored when FixedSize=true; smaller = larger on screen
            PixelSize = PixelSize
        };

        // Readability
        _label.OutlineSize = 4;
        _label.OutlineModulate = new Color(0, 0, 0, 0.8f);
        _label.FontSize = FontSize;

        AddChild(_label);

        AddToGroup("nameplates");

        // place the nameplate above the parent
        Position = new Vector3(0, HeightOffset, 0);
    }

    public override void _Process(double delta)
    {
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;

        if (YBillboardOnly)
        {
            // Face camera around Y only (prevents tilting/flip)
            Vector3 toCam = cam.GlobalTransform.Origin - GlobalTransform.Origin;
            toCam.Y = 0;
            if (toCam.LengthSquared() > 1e-6f)
            {
                float yaw = Mathf.Atan2(toCam.X, toCam.Z);
                Rotation = new Vector3(0f, yaw, 0f);
            }
        }
        else
        {
            // Full billboard but screen-upright.
            GlobalBasis = cam.GlobalTransform.Basis; // copies camera up/orientation
        }
    }

    // Optional runtime tweaks
    public void SetText(string t) { Text = t; if (_label != null) _label.Text = t; }
    public void SetColor(Color c) { FontColor = c; if (_label != null) _label.Modulate = c; }
}