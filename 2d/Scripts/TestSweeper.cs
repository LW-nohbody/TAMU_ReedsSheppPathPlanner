using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public partial class TestSweeper : Node
{
    // ---------- Sweep configuration ----------
    [ExportGroup("Sweep Ranges (pixels, degrees, pixels)")]
    [Export] public Vector2I StartXRange = new Vector2I(100, 700);   // inclusive
    [Export] public int      StartXStep = 200;

    [Export] public Vector2I StartYRange = new Vector2I(100, 500);
    [Export] public int      StartYStep = 200;

    [Export] public Vector2I GoalXRange  = new Vector2I(300, 1000);
    [Export] public int      GoalXStep   = 200;

    [Export] public Vector2I GoalYRange  = new Vector2I(100, 500);
    [Export] public int      GoalYStep   = 200;

    [Export] public Vector2I StartThetaDegRange = new Vector2I(0, 330); // 0..330
    [Export] public int      StartThetaStepDeg  = 30;

    [Export] public Vector2I GoalThetaDegRange = new Vector2I(0, 330);
    [Export] public int      GoalThetaStepDeg  = 30;

    [ExportGroup("Turn Radius (pixels)")]
    [Export] public float[] TurnRadiiPx = new float[] { 80f, 120f, 160f, 240f };

    [ExportGroup("Sampling")]
    [Export] public float SampleStepPx = 4f; // same meaning as in World.cs

    [ExportGroup("Runtime")]
    [Export] public bool RunOnReady = true;
    [Export] public string CsvFilename = "rs_sweep.csv";
    [Export] public bool PrintEachPath = false; // set true to see per-case path summary

    // ---------- Helpers ----------
    private static (double x, double y, double th) ToMath((double x, double y, double th) g)
        => (g.x, -g.y, -g.th);

    private static Vector2 ToGodot2D((double x, double y) m)
        => new Vector2((float)m.x, (float)(-m.y));

    private static float Deg2Rad(float deg) => (float)(Math.PI / 180.0) * deg;
    private static double WrapAngle(double a)
    {
        // Equivalent to Utils.M (wrap to [-π, π))
        a = Math.IEEERemainder(a, 2.0 * Math.PI);
        if (a < -Math.PI) a += 2.0 * Math.PI;
        if (a >= Math.PI) a -= 2.0 * Math.PI;
        return a;
    }

    public override void _Ready()
    {
        if (!RunOnReady) return;
        RunSweep();
    }

    public void RunSweep()
    {
        var t0 = Time.GetTicksMsec();

        var rows = new List<string>();
        rows.Add("R_px,start_x,start_y,start_th_deg,goal_x,goal_y,goal_th_deg,ok,total_norm_len,dx_px,dy_px,dth_rad");

        int count = 0, ok = 0;
        double worstAbsDth = 0, worstAbsDx = 0, worstAbsDy = 0;
        string worstCase = "";

        foreach (var Rpx in TurnRadiiPx)
        {
            for (int sx = StartXRange.X; sx <= StartXRange.Y; sx += StartXStep)
            for (int sy = StartYRange.X; sy <= StartYRange.Y; sy += StartYStep)
            for (int gx = GoalXRange.X;  gx <= GoalXRange.Y;  gx += GoalXStep)
            for (int gy = GoalYRange.X;  gy <= GoalYRange.Y;  gy += GoalYStep)
            for (int sdeg = StartThetaDegRange.X; sdeg <= StartThetaDegRange.Y; sdeg += StartThetaStepDeg)
            for (int gdeg = GoalThetaDegRange.X;  gdeg <= GoalThetaDegRange.Y;  gdeg += GoalThetaStepDeg)
            {
                count++;

                // 1) Build start/goal in Godot screen coords (pixels, radians)
                var startG = ((double)sx, (double)sy, (double)Deg2Rad(sdeg));
                var goalG  = ((double)gx, (double)gy, (double)Deg2Rad(gdeg));

                // 2) Convert to math (y-up, CCW positive)
                var startM = ToMath(startG);
                var goalM  = ToMath(goalG);

                // 3) Normalize by R for planning
                double R = Rpx;
                var startN = (startM.x / R, startM.y / R, startM.th);
                var goalN  = (goalM.x  / R, goalM.y  / R, goalM.th);

                // 4) Compute optimal path
                var best = ReedsSheppPaths.GetOptimalPath(startN, goalN);
                if (best == null || best.Count == 0)
                {
                    rows.Add($"{Rpx},{sx},{sy},{sdeg},{gx},{gy},{gdeg},0,NaN,NaN,NaN,NaN");
                    if (PrintEachPath) GD.Print($"No path: R={Rpx} s=({sx},{sy},{sdeg}) g=({gx},{gy},{gdeg})");
                    continue;
                }

                // Optional per-path dump (normalized)
                if (PrintEachPath)
                {
                    double tot = 0;
                    GD.Print("--- Best path (normalized) ---");
                    foreach (var e in best) { GD.Print($"{e.Steering} {e.Gear} param={e.Param:F4}"); tot += e.Param; }
                    GD.Print($"Total (sum of params) = {tot:F4}");
                }

                // 5) Sample in local normalized coordinates
                var ptsLocalNorm = RsSampler.SamplePolylineExact((0.0, 0.0, 0.0), best, 1.0, SampleStepPx / R);

                // 6) Transform back to world-math pixels (scale, rotate by start θ, translate by start(x,y))
                var ptsWorldMath = new List<Vector2>(ptsLocalNorm.Length);
                double c0 = Math.Cos(startM.th), s0 = Math.Sin(startM.th);
                foreach (var p in ptsLocalNorm)
                {
                    double sxp = p.X * R, syp = p.Y * R;
                    double wx = startM.x + (sxp * c0 - syp * s0);
                    double wy = startM.y + (sxp * s0 + syp * c0);
                    ptsWorldMath.Add(new Vector2((float)wx, (float)wy));
                }

                // 7) Compute terminal orientation change in local-norm space
                double thEndLocal = 0.0;
                foreach (var e in best)
                {
                    if (e.Steering == Steering.STRAIGHT) continue;
                    int steer = e.Steering == Steering.LEFT ? +1 : -1;
                    int gear  = e.Gear     == Gear.FORWARD ? +1 : -1;
                    thEndLocal += e.Param * steer * gear; // radians
                }
                thEndLocal = WrapAngle(thEndLocal);

                // 8) Terminal world-math pose
                var last = ptsWorldMath[^1];
                double endThWorld = WrapAngle(startM.th + thEndLocal);

                // 9) Error vs goal (in world-math)
                double dx = last.X - goalM.x;
                double dy = last.Y - goalM.y;
                double dth = WrapAngle(endThWorld - goalM.th);

                // norm-length for quick correlation
                double totalNorm = 0;
                foreach (var e in best) totalNorm += e.Param;

                // Accept as OK if small error (tune thresholds as needed)
                bool okCase = Math.Abs(dx) < 1.5 && Math.Abs(dy) < 1.5 && Math.Abs(dth) < 0.02; // ~1.5 px, ~1.1°
                if (okCase) ok++;

                rows.Add($"{Rpx},{sx},{sy},{sdeg},{gx},{gy},{gdeg},{(okCase?1:0)},{totalNorm:F4},{dx:F3},{dy:F3},{dth:F5}");

                // Track worst
                if (Math.Abs(dth) > worstAbsDth || Math.Abs(dx) > worstAbsDx || Math.Abs(dy) > worstAbsDy)
                {
                    worstAbsDth = Math.Max(worstAbsDth, Math.Abs(dth));
                    worstAbsDx  = Math.Max(worstAbsDx,  Math.Abs(dx));
                    worstAbsDy  = Math.Max(worstAbsDy,  Math.Abs(dy));
                    worstCase = $"R={Rpx} start=({sx},{sy},{sdeg}) goal=({gx},{gy},{gdeg}) -> dx={dx:F2}, dy={dy:F2}, dth={dth:F3}";
                }
            }
        }

        // Write CSV
        var path = ProjectSettings.GlobalizePath($"user://{CsvFilename}");
        using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            foreach (var r in rows) sw.WriteLine(r);

        var t1 = Time.GetTicksMsec();
        GD.Print($"Sweep done: {ok}/{count} ok, time={(t1 - t0)} ms");
        GD.Print($"Worst case: {worstCase}");
        GD.Print($"CSV: {path}");
    }
}