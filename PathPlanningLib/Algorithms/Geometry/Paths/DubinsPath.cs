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
                    double move = dsWorld;
                    x += move * Math.Cos(theta);
                    y += move * Math.Sin(theta);
                }
                else
                {
                    double steerSign = (int)elem.Steering; // +1 left, -1 right
                    double dtheta = steerSign * (dsWorld / turningRadius);
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
}