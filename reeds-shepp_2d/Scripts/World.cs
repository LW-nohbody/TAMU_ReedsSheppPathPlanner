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

    public override void _Ready() => ComputeAndDraw();

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept")) ComputeAndDraw();
    }

    // Switching from y_down and CW rotation to y_up and CCW for our math functions
    // Godot (x, y_down, +CW) -> math (x, y_up, +CCW)
    static (double x, double y, double th) ToMath((double x, double y, double th) g)
        => (g.x, -g.y, -g.th);

    // Fucnton to flip y back to down before drawing
    static Vector2 ToGodot2D((double x, double y) m)
        => new Vector2((float)m.x, (float)(-m.y));

    private void ComputeAndDraw()
    {
        if (StartGizmo == null || GoalGizmo == null || BestPath == null)
        {
            GD.PrintErr("World: Missing reference(s).");
            return;
        }

        // 1) Read start/goal in Godot, convert to math space (radians, y-up)
        var startG = ((double)StartGizmo.GlobalPosition.X,
                      (double)StartGizmo.GlobalPosition.Y,
                      (double)StartGizmo.GlobalRotation);
        var goalG  = ((double)GoalGizmo.GlobalPosition.X,
                      (double)GoalGizmo.GlobalPosition.Y,
                      (double)GoalGizmo.GlobalRotation);

        var startM = ToMath(startG);
        var goalM  = ToMath(goalG);

        // 2) Normalize XY by R for planning (headings stay in radians)
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

        // ---- DEBUG: print the normalized path we’re going to follow
        double total = 0;
        GD.Print("--- Best path (normalized) ---");
        foreach (var e in best)
        {
            GD.Print($"{e.Steering} {e.Gear} param={e.Param:F4}");
            total += e.Param;
        }
        GD.Print($"Total (sum of params) = {total:F4}");

        // 4) SAMPLE IN LOCAL *NORMALIZED* FRAME, starting at (0,0,theta_start)
        //    Use R=1 here because we’re in normalized space.
        var ptsLocalNorm = RsSampler.SamplePolylineExact(
            (0.0, 0.0, 0.0),        // start heading must be 0 in local normalized frame
            best,
            1.0,                    // R = 1 in normalized space
            SampleStep / R
        );

        // 5) TRANSFORM samples back to WORLD-MATH (pixels): scale by R, rotate by start θ, translate by start (x,y)
        var ptsWorldMath = new List<Vector2>(ptsLocalNorm.Length);
        double c0 = Math.Cos(startM.th);
        double s0 = Math.Sin(startM.th);

        foreach (var p in ptsLocalNorm)
        {
            double lx = p.X; // local normalized x
            double ly = p.Y; // local normalized y

            // scale to pixels first
            double sx = lx * R;
            double sy = ly * R;

            // rotate by start heading (y-up, CCW+)
            double wx = startM.x + (sx * c0 - sy * s0);
            double wy = startM.y + (sx * s0 + sy * c0);

            ptsWorldMath.Add(new Vector2((float)wx, (float)wy));
        }

        // 6) Draw (convert world-math -> Godot y-down)
        var ptsGodot = new Vector2[ptsWorldMath.Count];
        for (int i = 0; i < ptsWorldMath.Count; i++)
            ptsGodot[i] = ToGodot2D((ptsWorldMath[i].X, ptsWorldMath[i].Y));

        BestPath.Points = ptsGodot;
        BestPath.Width = 3;
        BestPath.DefaultColor = new Color(0.2f, 1f, 0.2f, 1f);

        // 7) Report errors in *both* local-normalized and world-pixel frames
        // Local-normalized end pose (relative to start)
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

        // Goal in local-normalized:
        var goalLocal = Utils.ChangeOfBasis(startNorm, goalNorm);

        GD.Print($"Reached (local norm): x={reachedLocal.X:F4}, y={reachedLocal.Y:F4}, th={thEndLocal:F4}");
        GD.Print($"Goal    (local norm): x={goalLocal.x:F4}, y={goalLocal.y:F4}, th={goalLocal.theta:F4}");
        GD.Print($"Error   (local norm): dx={(reachedLocal.X - goalLocal.x):F4}, dy={(reachedLocal.Y - goalLocal.y):F4}, dth={(Utils.M(thEndLocal - goalLocal.theta)):F4}");

        // World pixel error (use transformed last point)
        var lastWorld = ptsWorldMath[^1];
        GD.Print($"End error (world pixels): dx={(lastWorld.X - goalM.x):F2}, dy={(lastWorld.Y - goalM.y):F2}");
    }
}