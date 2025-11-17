using Godot;
namespace DigSim3D.UI;

public partial class UIToggleSwitch : Control
{
    // UI Toggle Switch Objects
    private Button? _background;
    private StyleBoxFlat? _styleNormal;
    private StyleBoxFlat? _styleHover;
    private Button? _knob;
    private StyleBoxFlat? _knobStyleNormal;
    private StyleBoxFlat? _knobStyleHover;
    private Label? _titleLabel;
    private Label? _onLabel;
    private Label? _offLabel;

    // Toggle Switch Internal State Variables
    private bool _pressed = true;
    private Control? _uiToToggle = null;
    private Vector2 _offset = new Vector2(20, 20);

    public override void _Ready()
    {
        // === TITLE LABEL ABOVE THE SWITCH ===
        _titleLabel = new Label();
        _titleLabel.Text = "Toggle UI";
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_titleLabel);

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

        // === OFF LABEL (LEFT) ===
        _offLabel = new Label();
        _offLabel.Text = "OFF";
        _offLabel.Position = new Vector2(4, 5);
        AddChild(_offLabel);

        // === ON LABEL (RIGHT) ===
        _onLabel = new Label();
        _onLabel.Text = "ON";
        _onLabel.Position = new Vector2((float) (Size.X - _knob.Size.X - 2.5), (float) 5); // right
        AddChild(_onLabel);

        _onLabel.AddThemeFontSizeOverride("font_size", 11);
        _offLabel.AddThemeFontSizeOverride("font_size", 11);
        _offLabel.Visible = false; // Start with OFF visible

        // Set Switch Position to Bottom-Right
        Position = new Vector2(
            GetViewportRect().Size.X - Size.X - _offset.X,
            GetViewportRect().Size.Y - Size.Y - _offset.Y
        );

        Vector2 titleSize = _titleLabel.GetCombinedMinimumSize();
        // Position above the switch
        _titleLabel.Position = new Vector2(
            (Size.X - titleSize.X) / 2f,
            -titleSize.Y - 4
        );

        // Enable input
        MouseFilter = MouseFilterEnum.Stop;
    }

    private void Toggle()
    {
        _pressed = !_pressed;
        if (_knob != null)
        {
            // Move knob left or right
            _knob.Position = _pressed
                ? new Vector2(2, 2) // left
                : new Vector2(Size.X - _knob.Size.X - 2, 2); // right
            
            if (_pressed)
            {
                _onLabel.Visible = true;
                _offLabel.Visible = false;
            } else
            {
                _onLabel.Visible = false;
                _offLabel.Visible = true;
            }

            // Toggle UI
            if (_uiToToggle != null)
                _uiToToggle.Visible = _pressed;
        } else
        {
            GD.PrintErr("UIToggleSwitch: _knob is null!");
            return;
        }
    }

    public void SetTargetUI(Control ui)
    {
        _uiToToggle = ui;
        _uiToToggle.Visible = _pressed;
    }

    public bool IsPointInUI(Vector2 point)
    {
        return this.GetGlobalRect().HasPoint(point);
    }
}
