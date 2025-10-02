using System;
using System.Collections.Generic;

/// Exact, non-sampled application of RS segments in *math* space (y-up, CCW+), units normalized (R=1).
public static class PoseStepper
{
    // Pose is (x,y,th) with th in radians.
    public static (double x, double y, double th) ApplyPath(
        (double x, double y, double th) start,
        List<PathElement> path)
    {
        double x = start.x, y = start.y, th = start.th;

        foreach (var seg in path)
        {
            if (seg.Steering == Steering.STRAIGHT)
            {
                // Straight distance in normalized units
                double s = seg.Param * (seg.Gear == Gear.FORWARD ? 1.0 : -1.0);
                x += s * Math.Cos(th);
                y += s * Math.Sin(th);
            }
            else
            {
                int steer = seg.Steering == Steering.LEFT ? +1 : -1;
                int gear  = seg.Gear     == Gear.FORWARD   ? +1 : -1;

                // Signed heading change (radians), R=1 in normalized space
                double dth = seg.Param * steer * gear;

                double thPrev = th;
                th += dth;

                // Closed-form chord update for constant-curvature arc with R=1
                // x += ∫ cos(theta) ds, with ds = dtheta (since R=1)  -> sin(th) - sin(thPrev)
                // y += ∫ sin(theta) ds                                 -> -cos(th) + cos(thPrev)
                x += Math.Sin(th) - Math.Sin(thPrev);
                y += -Math.Cos(th) + Math.Cos(thPrev);
            }
        }

        // Wrap angle to [0, 2π)
        th = Utils.M(th);
        return (x, y, th);
    }
}
