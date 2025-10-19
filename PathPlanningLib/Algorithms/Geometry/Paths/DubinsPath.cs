namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.PathElements;

/// Represents a Dubins path consisting of DubinsElements.
public class DubinsPath : Path<DubinsElement>
{
    /// Default constructor: empty path
    public DubinsPath() : base() { }

    /// Constructor from an enumerable of DubinsElements
    public DubinsPath(IEnumerable<DubinsElement> elements) : base(elements) { }

    /// Adds a new DubinsElement to the path
    public override void Add(DubinsElement element)
    {
        base.Add(element); // base class Add
    }

    /// Removes the first occurrence of a given element from the path.
    public override bool Remove(DubinsElement element)
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
            // distances are always non-negative
            total += e.Param;
        }

        Length = total;
    }
    
    public override PosePath Sample(double stepSize)
        => throw new InvalidOperationException("Dubins sampling requires a turning radius and start pose parameter.");

    // stepSize = real-world step distance
    public PosePath Sample(double stepSize, double turningRadius, Pose startPose)
    {
        var poses = new List<Pose>();
        var current = startPose;
        poses.Add(current);

        foreach (var elem in Elements)
        {
            double dir = elem.Steering == Steering.LEFT ? 1.0 :
                        elem.Steering == Steering.RIGHT ? -1.0 : 0.0;

            double s = 0.0;
            while (s < elem.Param)
            {
                double ds = Math.Min(stepSize, elem.Param - s);

                double newX = current.X;
                double newY = current.Y;
                double newTheta = current.Theta;

                if (elem.Steering == Steering.STRAIGHT)
                {
                    // Move forward in current heading
                    newX += ds * Math.Cos(current.Theta);
                    newY += ds * Math.Sin(current.Theta);
                }
                else
                {
                    // Turning arc (constant curvature = 1/turningRadius)
                    double dtheta = dir * (ds / turningRadius);
                    newX += turningRadius * (Math.Sin(current.Theta + dtheta) - Math.Sin(current.Theta));
                    newY -= turningRadius * (Math.Cos(current.Theta + dtheta) - Math.Cos(current.Theta));
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