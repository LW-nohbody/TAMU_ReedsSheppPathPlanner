using Godot;
using System;
using System.Collections.Generic;

public partial class World : Node2D
{
    [Export] public Node2D StartGizmo;
    [Export] public Node2D GoalGizmo;
    [Export] public Line2D BestPath;

    [Export] public float TurnRadius = 120f; // pixels per 1 turn-radius unit
    [Export] public float SampleStep = 4f;   // visual density only

    // --- World/origin config ---
    [ExportGroup("World Origin (pixels, relative to top-left of viewport)")]
    [Export] public Vector2 WorldOriginOffset = new Vector2(100, 100); // keep in sync with Grid2D.OriginOffset

    // --- Defaults (used by Reset and optionally at startup) ---
    [ExportGroup("Defaults (grid units & degrees)")]
    [Export] public float DefaultTurnRadiusGrid = 1.20f; // in UI grid units
    [Export] public float DefaultStartX = 1.00f;
    [Export] public float DefaultStartY = 3.00f;
    [Export] public float DefaultStartThetaDeg = 0f;
    [Export] public float DefaultGoalX = 9.00f;
    [Export] public float DefaultGoalY = 3.00f;
    [Export] public float DefaultGoalThetaDeg = 180f;

    // --- UI Nodes (assign these in Inspector) ---
    [Export] public NodePath SidePanelPath;

    [ExportGroup("UI: SpinBoxes")]
    [Export] public NodePath TurnRadiusInputPath;
    [Export] public NodePath StartXPath;
    [Export] public NodePath StartYPath;
    [Export] public NodePath StartThetaPath;   // degrees
    [Export] public NodePath GoalXPath;
    [Export] public NodePath GoalYPath;
    [Export] public NodePath GoalThetaPath;    // degrees

    [ExportGroup("UI: Buttons")]
    [Export] public NodePath ResetButtonPath;

    // --- Cached UI refs ---
    private Control _sidePanel;
    private SpinBox _turnRadiusInput;
    private SpinBox _startX, _startY, _startTheta;
    private SpinBox _goalX,  _goalY,  _goalTheta;
    private Button  _resetBtn;

    // === UI/units helpers ===
    // Set to 50f if you want UI unit = 50 px; set to 100f if you want UI unit = 100 px.
    private const float GRID = 50f;

    private static float Deg2Rad(float deg) => (float)(Math.PI / 180.0) * deg;
    private static float Rad2Deg(float rad) => (float)(180.0 / Math.PI) * rad;

    private void HookSpinBox(SpinBox sb, Action handler)
    {
        if (sb == null) return;
        sb.ValueChanged += _ => handler();  // live update
        sb.FocusExited  += handler;         // safety net
    }

    // Global/screen-space origin (right edge of panel + offset)
    private Vector2 OriginScreen()
    {
        float panel = _sidePanel != null ? _sidePanel.Size.X : 0f;
        return new Vector2(panel + WorldOriginOffset.X, WorldOriginOffset.Y);
    }

    public override void _Ready()
    {
        // 1) Resolve UI nodes
        _sidePanel       = GetNodeOrNull<Control>(SidePanelPath);
        _turnRadiusInput = GetNodeOrNull<SpinBox>(TurnRadiusInputPath);
        _startX          = GetNodeOrNull<SpinBox>(StartXPath);
        _startY          = GetNodeOrNull<SpinBox>(StartYPath);
        _startTheta      = GetNodeOrNull<SpinBox>(StartThetaPath);
        _goalX           = GetNodeOrNull<SpinBox>(GoalXPath);
        _goalY           = GetNodeOrNull<SpinBox>(GoalYPath);
        _goalTheta       = GetNodeOrNull<SpinBox>(GoalThetaPath);
        _resetBtn        = GetNodeOrNull<Button>(ResetButtonPath);

        // Robust fallback: try to find a node literally named "SidePanel" anywhere
        if (_sidePanel == null)
        {
            _sidePanel = GetTree()?.Root?.FindChild("SidePanel", recursive: true, owned: false) as Control;
            if (_sidePanel == null) GD.PrintErr("World: SidePanel not set/found.");
        }

        // 2) Do NOT move the parent; draw and place in global/screen space
        Position = Vector2.Zero;

        // Keep recomputing if the panel resizes (origin depends on panel width)
        if (_sidePanel != null)
            _sidePanel.Resized += () => { ComputeAndDraw(); };

        // Ensure the path draws in screen/global coords (ignores parent transforms)
        if (BestPath != null)
        {
            BestPath.TopLevel = true;
            BestPath.Position = Vector2.Zero;
        }

        // 3) Configure input ranges (in grid units, relative to our origin)
        var view = GetViewportRect();
        float panelW  = _sidePanel != null ? _sidePanel.Size.X : 0f;
        float usableW = Math.Max(0, view.Size.X - (panelW + WorldOriginOffset.X));
        float usableH = Math.Max(0, view.Size.Y - WorldOriginOffset.Y);

        if (_startX != null) { _startX.MinValue = 0; _startX.MaxValue = usableW / GRID; _startX.Step = 0.01; }
        if (_goalX  != null) { _goalX.MinValue  = 0; _goalX.MaxValue  = usableW / GRID; _goalX.Step  = 0.01; }
        if (_startY != null) { _startY.MinValue = 0; _startY.MaxValue = usableH / GRID; _startY.Step = 0.01; }
        if (_goalY  != null) { _goalY.MinValue  = 0; _goalY.MaxValue  = usableH / GRID; _goalY.Step  = 0.01; }
        if (_startTheta != null) { _startTheta.MinValue = 0; _startTheta.MaxValue = 360; _startTheta.Step = 1; }
        if (_goalTheta  != null) { _goalTheta.MinValue  = 0; _goalTheta.MaxValue  = 360; _goalTheta.Step  = 1; }
        if (_turnRadiusInput != null) _turnRadiusInput.Step = 0.01;

        // 4) Hook commit handlers
        HookSpinBox(_turnRadiusInput, OnTurnRadiusCommitted);
        HookSpinBox(_startX,          OnStartPoseCommitted);
        HookSpinBox(_startY,          OnStartPoseCommitted);
        HookSpinBox(_startTheta,      OnStartPoseCommitted);
        HookSpinBox(_goalX,           OnGoalPoseCommitted);
        HookSpinBox(_goalY,           OnGoalPoseCommitted);
        HookSpinBox(_goalTheta,       OnGoalPoseCommitted);
        if (_resetBtn != null) _resetBtn.Pressed += OnResetPressed;

        // 5) Start from exported defaults (also used by Reset)
        ApplyDefaultsToUIAndWorld();

        // 6) Initial draw
        ComputeAndDraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept")) ComputeAndDraw();
    }

    // Godot (x, y_down, +CW) -> math (x, y_up, +CCW)
    static (double x, double y, double th) ToMath((double x, double y, double th) g)
        => (g.x, -g.y, -g.th);

    // math (x, y_up) -> Godot (x, y_down)
    static Vector2 ToGodot2D((double x, double y) m)
        => new Vector2((float)m.x, (float)(-m.y));

    // ---- UI ↔ world glue ----

    private void InitUIFromWorld()
    {
        if (_turnRadiusInput != null) _turnRadiusInput.Value = TurnRadius / GRID;

        var origin = OriginScreen();

        var sRel = StartGizmo.GlobalPosition - origin;
        if (_startX != null)     _startX.Value     = sRel.X / GRID;
        if (_startY != null)     _startY.Value     = sRel.Y / GRID;
        if (_startTheta != null) _startTheta.Value = Rad2Deg(StartGizmo.GlobalRotation);

        var gRel = GoalGizmo.GlobalPosition - origin;
        if (_goalX != null)      _goalX.Value      = gRel.X / GRID;
        if (_goalY != null)      _goalY.Value      = gRel.Y / GRID;
        if (_goalTheta != null)  _goalTheta.Value  = Rad2Deg(GoalGizmo.GlobalRotation);
    }

    private void ApplyDefaultsToUIAndWorld()
    {
        if (_turnRadiusInput != null) _turnRadiusInput.Value = DefaultTurnRadiusGrid;
        if (_startX != null)          _startX.Value          = DefaultStartX;
        if (_startY != null)          _startY.Value          = DefaultStartY;
        if (_startTheta != null)      _startTheta.Value      = DefaultStartThetaDeg;
        if (_goalX != null)           _goalX.Value           = DefaultGoalX;
        if (_goalY != null)           _goalY.Value           = DefaultGoalY;
        if (_goalTheta != null)       _goalTheta.Value       = DefaultGoalThetaDeg;

        OnTurnRadiusCommitted();
        OnStartPoseCommitted();
        OnGoalPoseCommitted();
    }

    private void OnTurnRadiusCommitted()
    {
        if (_turnRadiusInput == null) return;
        TurnRadius = (float)_turnRadiusInput.Value * GRID; // grid -> pixels
        ComputeAndDraw();
    }

    private void OnStartPoseCommitted()
    {
        var origin = OriginScreen();

        float xPx   = (_startX != null   ? (float)_startX.Value   * GRID : (StartGizmo.GlobalPosition - origin).X);
        float yPx   = (_startY != null   ? (float)_startY.Value   * GRID : (StartGizmo.GlobalPosition - origin).Y);
        float thRad = (_startTheta != null? Deg2Rad((float)_startTheta.Value) : StartGizmo.GlobalRotation);

        StartGizmo.GlobalPosition = origin + new Vector2(xPx, yPx);
        StartGizmo.GlobalRotation = thRad;

        ComputeAndDraw();
    }

    private void OnGoalPoseCommitted()
    {
        var origin = OriginScreen();

        float xPx   = (_goalX != null   ? (float)_goalX.Value   * GRID : (GoalGizmo.GlobalPosition - origin).X);
        float yPx   = (_goalY != null   ? (float)_goalY.Value   * GRID : (GoalGizmo.GlobalPosition - origin).Y);
        float thRad = (_goalTheta != null? Deg2Rad((float)_goalTheta.Value) : GoalGizmo.GlobalRotation);

        GoalGizmo.GlobalPosition = origin + new Vector2(xPx, yPx);
        GoalGizmo.GlobalRotation = thRad;

        ComputeAndDraw();
    }

    private void OnResetPressed()
    {
        ApplyDefaultsToUIAndWorld();
    }

    private void ComputeAndDraw()
    {
        if (StartGizmo == null || GoalGizmo == null || BestPath == null)
        {
            GD.PrintErr("World: Missing reference(s).");
            return;
        }

        // 1) Read start/goal in Godot (global), convert to math (y-up)
        var startG = ((double)StartGizmo.GlobalPosition.X,
                      (double)StartGizmo.GlobalPosition.Y,
                      (double)StartGizmo.GlobalRotation);
        var goalG  = ((double)GoalGizmo.GlobalPosition.X,
                      (double)GoalGizmo.GlobalPosition.Y,
                      (double)GoalGizmo.GlobalRotation);

        var startM = ToMath(startG);
        var goalM  = ToMath(goalG);

        // 2) Normalize XY by R for planning (headings stay radians)
        double R = TurnRadius;
        var startNorm = (startM.x / R, startM.y / R, startM.th);
        var goalNorm  = (goalM.x  / R, goalM.y  / R, goalM.th);

        // 3) Get best RS path (in normalized units)
        var best = ReedsSheppPaths.GetOptimalPath(startNorm, goalNorm);
        if (best == null || best.Count == 0)
        {
            GD.Print("No RS path found.");
            BestPath.Points = Array.Empty<Vector2>();
            return;
        }

        // ---- DEBUG: print the normalized path sequence (as before)
        double total = 0;
        GD.Print("--- Best path (normalized) ---");
        foreach (var e in best)
        {
            GD.Print($"{e.Steering} {e.Gear} param={e.Param:F4}");
            total += e.Param;
        }
        GD.Print($"Total (sum of params) = {total:F4}");

        // 4) Sample in local-normalized frame (start at 0,0,0)
        var ptsLocalNorm = RsSampler.SamplePolylineExact(
            (0.0, 0.0, 0.0), best, 1.0, SampleStep / R);

        // 5) Transform back to world-math (pixels): scale by R, rotate by start θ, translate by start (x,y)
        var ptsWorldMath = new List<Vector2>(ptsLocalNorm.Length);
        double c0 = Math.Cos(startM.th);
        double s0 = Math.Sin(startM.th);

        foreach (var p in ptsLocalNorm)
        {
            double sx = p.X * R, sy = p.Y * R;
            double wx = startM.x + (sx * c0 - sy * s0);
            double wy = startM.y + (sx * s0 + sy * c0);
            ptsWorldMath.Add(new Vector2((float)wx, (float)wy));
        }

        // 6) Draw (math y-up -> Godot y-down), in screen/global coords
        var ptsGodot = new Vector2[ptsWorldMath.Count];
        for (int i = 0; i < ptsWorldMath.Count; i++)
            ptsGodot[i] = ToGodot2D((ptsWorldMath[i].X, ptsWorldMath[i].Y));

        BestPath.Points = ptsGodot;
        BestPath.Width = 3;
        BestPath.DefaultColor = new Color(0.2f, 1f, 0.2f, 1f);

        // 7) Local-normalized ending pose and error (same as your original output)
        var reachedLocal = ptsLocalNorm[^1];
        double thEndLocal = 0.0;
        foreach (var e in best)
        {
            if (e.Steering == Steering.STRAIGHT) continue;
            int steer = e.Steering == Steering.LEFT ? +1 : -1;
            int gear  = e.Gear     == Gear.FORWARD ? +1 : -1;
            thEndLocal += e.Param * steer * gear; // radians
        }
        thEndLocal = Utils.M(thEndLocal);

        var goalLocal = Utils.ChangeOfBasis(startNorm, goalNorm);
        GD.Print($"Reached (local norm): x={reachedLocal.X:F4}, y={reachedLocal.Y:F4}, th={thEndLocal:F4}");
        GD.Print($"Goal    (local norm): x={goalLocal.x:F4}, y={goalLocal.y:F4}, th={goalLocal.theta:F4}");
        GD.Print($"Error   (local norm): dx={(reachedLocal.X - goalLocal.x):F4}, dy={(reachedLocal.Y - goalLocal.y):F4}, dth={(Utils.M(thEndLocal - goalLocal.theta)):F4}");

        var lastWorld = ptsWorldMath[^1];
        GD.Print($"End error (world pixels): dx={(lastWorld.X - goalM.x):F2}, dy={(lastWorld.Y - goalM.y):F2}");
    }
}
