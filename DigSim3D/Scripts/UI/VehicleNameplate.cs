using Godot;
namespace DigSim3D.UI;
public partial class VehicleNameplate : Node3D
{
    [Export] public float HeightOffset = 1.0f;     // vertical offset above vehicle origin
    [Export] public bool YBillboardOnly = false;    // face camera only around Y axis
    [Export] private Label3D _label = null!;

    public override void _Ready()
    {
         if (_label == null)
        {
            GD.PushError("[VehicleNameplate] Label3D node is not assigned.");
            return;
        }

        AddToGroup("nameplates");

        // Defer positioning to ensure parent transform is set first
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
    public void SetText(string t) { if (_label != null) _label.Text = t; }
    public void SetColor(Color c) { if (_label != null) _label.Modulate = c; }
}