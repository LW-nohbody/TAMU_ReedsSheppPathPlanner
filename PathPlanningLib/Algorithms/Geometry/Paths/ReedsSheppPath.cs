namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.PathElements;

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

    // stepSize = real-world step distance
    public PosePath Sample(double stepSize, double turningRadius, Pose startPose)
    {
        List<Pose> poses = new List<Pose>();
        Pose current = startPose;
        poses.Add(current);

        foreach (var elem in Elements)
        {
            double sign = elem.Gear == Gear.FORWARD ? 1.0 : -1.0;
            double dir = elem.Steering == Steering.LEFT ? 1.0 :
                        elem.Steering == Steering.RIGHT ? -1.0 : 0.0;

            double s = 0.0;
            while (s < elem.Param)
            {
                double ds = Math.Min(stepSize, elem.Param - s); // avoid overshooting

                double newX = current.X;
                double newY = current.Y;
                double newTheta = current.Theta;

                if (elem.Steering == Steering.STRAIGHT)
                {
                    newX += sign * ds * Math.Cos(current.Theta);
                    newY += sign * ds * Math.Sin(current.Theta);
                }
                else
                {
                    double dtheta = sign * dir * (ds / turningRadius);
                    newX += turningRadius * (Math.Sin(current.Theta + dtheta) - Math.Sin(current.Theta)) * sign;
                    newY -= turningRadius * (Math.Cos(current.Theta + dtheta) - Math.Cos(current.Theta)) * sign;
                    newTheta = MathUtils.NormalizeAngle(current.Theta + dtheta);
                }

                current = Pose.Create(newX, newY, newTheta);
                poses.Add(current);
                s += ds;
            }
        }

        return new PosePath(poses);
    }
}

