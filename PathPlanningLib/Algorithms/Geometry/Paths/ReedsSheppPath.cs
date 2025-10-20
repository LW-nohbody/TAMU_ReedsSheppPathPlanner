namespace PathPlanningLib.Algorithms.Geometry.Paths;

using System.Security.Cryptography.X509Certificates;
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
}

