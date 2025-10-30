namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Diagnostics;

// Represents a Reeds-Shepp path consisting of ReedsSheppElements
public class ReedsSheppPath : Path<ReedsSheppElement>
{
    /// Default constructor: empty path
    public ReedsSheppPath() : base() { }

    /// Constructor from an enumerable of ReedsSheppElements
    public ReedsSheppPath(IEnumerable<ReedsSheppElement> elements) : base(elements) { }

    /// Adds a new ReedsSheppElement to the path
    public override void Add(ReedsSheppElement element)
    {
        base.Add(element); // base class Add
    }

    /// Removes the first occurrence of a given element from the path.
    public override bool Remove(ReedsSheppElement element)
    {
        return base.Remove(element);
    }

    /// Clears the path
    public override void Clear()
    {
        base.Clear();
    }


    /// Recalculates the total path length (normalized by turning radius)
    public override void ComputeLength()
    {
        double total = 0.0;
        foreach (var e in _elements)
        {
            // Distance is always non-negative
            total += Math.Abs(e.Param);
        }

        Length = total;
    }

    // Reverses gear for time-flip symmetry
    public ReedsSheppPath Timeflip()
    {
        var flipped = new ReedsSheppPath(_elements.Select(e => e.ReverseGear()));
        return flipped;
    }

    // Reverses steering for reflection symmetry
    public ReedsSheppPath Reflect()
    {
        var reflected = new ReedsSheppPath(
            _elements.Select(e => e with { Steering = (Steering)(-(int)e.Steering) })
        );
        return reflected;
    }

    public override PosePath Sample(double stepSize)
        => throw new InvalidOperationException("Reeds-Shepp sampling requires turning radius and start pose parameter");

    // stepSize = real-world step distance --> must always be greater than 0 --> reccomended to always be less than longest path segment in RS path
    public PosePath Sample(double stepSize, double turningRadius, Pose startPose)
    {
        if (stepSize <= 0)
            throw new ArgumentException("Step size must be positive.", nameof(stepSize));
        if (turningRadius <= 0)
            throw new ArgumentException("Turning radius must be positive and greater than 0.", nameof(turningRadius));

        List<Pose> poses = new List<Pose>();
        double x = startPose.X;
        double y = startPose.Y;
        double theta = startPose.Theta;
        poses.Add(startPose);

        foreach (var elem in Elements)
        {
            double s_norm = elem.Param;
            double s_world = s_norm * turningRadius;

            // heuristically set that each segment must have at least 2 steps
            int nSteps = Math.Max(2, (int)Math.Ceiling(s_world / stepSize));
            double dsWorld = s_world / nSteps;

            for (int i = 0; i < nSteps; i++)
            {
                if (elem.Steering == Steering.STRAIGHT)
                {
                    double move = dsWorld * (int)elem.Gear;
                    x += move * Math.Cos(theta);
                    y += move * Math.Sin(theta);
                }
                else
                {
                    double steerSign = (int)elem.Steering; // +1 left, -1 right
                    double gearSign = (int)elem.Gear;     // +1 forward, -1 backward
                    double dtheta = steerSign * (dsWorld / turningRadius) * gearSign;
                    // rotate about instantaneous center
                    double thetaPrev = theta;
                    theta += dtheta;

                    // arc-based incremental update (exact integration of circular arc)
                    // center-based incremental:
                    double cx = x - steerSign * turningRadius * Math.Sin(thetaPrev);
                    double cy = y + steerSign * turningRadius * Math.Cos(thetaPrev);

                    double newX = cx + steerSign * turningRadius * Math.Sin(theta);
                    double newY = cy - steerSign * turningRadius * Math.Cos(theta);

                    x = newX;
                    y = newY;
                }
                poses.Add(Pose.Create(x, y, theta));
            }
        }
        return new PosePath(poses);
    }

    public (PosePath path, List<int> gears) SampleWithGears(double stepSize, double turningRadius, Pose startPose)
    {
        if (stepSize <= 0) throw new ArgumentException("Step size must be positive.", nameof(stepSize));
        if (turningRadius <= 0) throw new ArgumentException("Turning radius must be positive.", nameof(turningRadius));

        var poses = new List<Pose>();
        var gears = new List<int>();

        double x = startPose.X, y = startPose.Y, theta = startPose.Theta;
        poses.Add(startPose); gears.Add(+1);

        // DEBUG: begin + args
        string pid = DebugPath.Begin("lib.sample_gears", 0, 0);
        DebugPath.Check(pid, "args",
            ("stepSize", stepSize),
            ("turningRadius", turningRadius),
            ("start.x", x), ("start.y", y), ("start.th", theta));

        // DEBUG: path summary (normalized length)
        double totalLenNorm = 0.0;
        foreach (var e0 in Elements) totalLenNorm += Math.Abs(e0.Param);
        DebugPath.Check(pid, "segments",
            ("count", Elements.Count),
            ("totalLenNorm", totalLenNorm));


        foreach (var elem in Elements)
        {
            int gearSign = (int)elem.Gear;
            double s_norm = elem.Param;
            double s_world = s_norm * turningRadius;

            // DEBUG: segment start
            DebugPath.Check(pid, "seg_start",
                ("steer", elem.Steering),   // STRAIGHT, LEFT, RIGHT
                ("gear", gearSign),         // +1 / -1
                ("lenNorm", s_norm),
                ("lenWorld", s_world));


            int nSteps = Math.Max(2, (int)Math.Ceiling(s_world / stepSize));
            double dsWorld = s_world / nSteps;

            if (elem.Steering == Steering.STRAIGHT)
            {
                for (int i = 0; i < nSteps; i++)
                {
                    double move = dsWorld * gearSign;
                    x += move * Math.Cos(theta);
                    y += move * Math.Sin(theta);
                    poses.Add(Pose.Create(x, y, theta));
                    gears.Add(gearSign);

                    // DEBUG (throttle to avoid spam)
                    if ((i % 10) == 0)
                        DebugPath.Check(pid, "step",
                            ("steer", "S"), ("i", i),
                            ("x", x), ("y", y), ("th", theta));

                }
            }
            else
            {
                int steerSign = (int)elem.Steering;
                double dthetaMag = dsWorld / turningRadius;

                for (int i = 0; i < nSteps; i++)
                {
                    double dth = steerSign * gearSign * dthetaMag;
                    double thetaPrev = theta;
                    theta += dth;

                    double cx = x - steerSign * turningRadius * Math.Sin(thetaPrev);
                    double cy = y + steerSign * turningRadius * Math.Cos(thetaPrev);
                    x = cx + steerSign * turningRadius * Math.Sin(theta);
                    y = cy - steerSign * turningRadius * Math.Cos(theta);

                    poses.Add(Pose.Create(x, y, theta));
                    gears.Add(gearSign);

                    // DEBUG (throttle to avoid spam)
                    if ((i % 10) == 0)
                        DebugPath.Check(pid, "step",
                            ("steer", steerSign > 0 ? "L" : "R"), ("i", i),
                            ("x", x), ("y", y), ("th", theta));

                }
            }
            // DEBUG: segment end
            DebugPath.Check(pid, "seg_end",
                ("x", x), ("y", y), ("th", theta));
        }

        // Ensure exact terminal pose (append only if tiny drift exists) and keep gears aligned
        {
            double ex = startPose.X, ey = startPose.Y, eth = startPose.Theta;
            int lastGear = gears.Count > 0 ? gears[^1] : +1;

            foreach (var e in Elements)
            {
                int gearS = (int)e.Gear;       // <-- int, not double
                int steerS = (int)e.Steering;   // <-- int, not double
                double segWorld = Math.Abs(e.Param) * turningRadius;

                if (e.Steering == Steering.STRAIGHT)
                {
                    double move = segWorld * gearS;
                    ex += move * Math.Cos(eth);
                    ey += move * Math.Sin(eth);
                }
                else
                {
                    double dthetaSnap = steerS * gearS * (segWorld / turningRadius); // <-- renamed (no 'dth' shadow)
                    double thPrev = eth;
                    eth += dthetaSnap;

                    double cx = ex - steerS * turningRadius * Math.Sin(thPrev);
                    double cy = ey + steerS * turningRadius * Math.Cos(thPrev);
                    ex = cx + steerS * turningRadius * Math.Sin(eth);
                    ey = cy - steerS * turningRadius * Math.Cos(eth);
                }

                lastGear = gearS;  // int -> int, no cast required
            }

            var last = poses[^1];
            double dx = ex - last.X, dy = ey - last.Y, dthEnd = eth - last.Theta;   // <-- unique name
            if (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9 || Math.Abs(Math.IEEERemainder(dthEnd, 2 * Math.PI)) > 1e-9)
            {
                poses.Add(Pose.Create(ex, ey, eth));
                gears.Add(lastGear);
            }
        }

        // DEBUG: end
        DebugPath.End(pid, "ok",
            ("nPts", poses.Count), ("nGears", gears.Count),
            ("last.x", poses[^1].X), ("last.y", poses[^1].Y), ("last.th", poses[^1].Theta));

        return (new PosePath(poses), gears);
    }
}

