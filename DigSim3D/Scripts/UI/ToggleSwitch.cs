using Godot;
namespace DigSim3D.UI;

public partial class ToggleSwitch : Control
{
    private Button _background;
    private StyleBoxFlat _styleNormal;
    private StyleBoxFlat _styleHover;
    private Button _knob;
    private StyleBoxFlat _knobStyleNormal;
    private StyleBoxFlat _knobStyleHover;

    private bool _pressed = true;
    private Control? _uiToToggle = null;
    private Vector2 _offset = new Vector2(20, 20);

    public override void _Ready()
    {
        // Size of switch
        Size = new Vector2(50, 25);

        // Background (Panel)
        _background = new Button();
        _background.Size = Size;
        AddChild(_background);

        // === NORMAL STYLE ===
        _styleNormal = new StyleBoxFlat();
        _styleNormal.BgColor = new Color(0.2f, 0.3f, 0.5f, 0.8f);
        _styleNormal.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f);
        _styleNormal.SetBorderWidthAll(2);
        _styleNormal.SetCornerRadiusAll(2);

        // === HOVER STYLE ===
        _styleHover = new StyleBoxFlat();
        _styleHover.BgColor = new Color(0.3f, 0.4f, 0.6f, 0.9f);
        _styleHover.BorderColor = new Color(0.5f, 0.7f, 1.0f, 1.0f);
        _styleHover.SetBorderWidthAll(2);
        _styleHover.SetCornerRadiusAll(2);

        // Apply styleboxes to theme overrides
        _background.AddThemeStyleboxOverride("normal", _styleNormal);
        _background.AddThemeStyleboxOverride("hover", _styleHover);

        _background.Pressed += () => Toggle();

        // Knob
        _knob = new Button();
        _knob.Size = new Vector2(21, 21);
        _knob.Position = new Vector2(2, 2);
        _knob.Pressed += () => Toggle();
        AddChild(_knob);

        // Normal knob style
        _knobStyleNormal = new StyleBoxFlat();
        _knobStyleNormal.BgColor = new Color(1f, 1f, 1f);
        _knob.AddThemeStyleboxOverride("normal", _knobStyleNormal);

        // Hover knob style
        _knobStyleHover = new StyleBoxFlat();
        _knobStyleHover.BgColor = new Color(0.9f, 0.9f, 0.9f);
        _knob.AddThemeStyleboxOverride("hover", _knobStyleHover);


        // Enable input
        MouseFilter = MouseFilterEnum.Stop;
    }

    private void Toggle()
    {
        _pressed = !_pressed;

        // Move knob left or right
        _knob.Position = _pressed
            ? new Vector2(2, 2) // left
            : new Vector2(Size.X - _knob.Size.X - 2, 2); // right

        // Toggle UI
        if (_uiToToggle != null)
            _uiToToggle.Visible = _pressed;
    }

    public void SetTargetUI(Control ui)
    {
        _uiToToggle = ui;
        _uiToToggle.Visible = _pressed;
    }

    public override void _Process(double delta)
    {
        // Keep switch at bottom-right
        Position = new Vector2(
            GetViewportRect().Size.X - Size.X - _offset.X,
            GetViewportRect().Size.Y - Size.Y - _offset.Y
        );
    }

    public bool IsPointInUI(Vector2 point)
    {
        return this.GetGlobalRect().HasPoint(point);
    }
}
