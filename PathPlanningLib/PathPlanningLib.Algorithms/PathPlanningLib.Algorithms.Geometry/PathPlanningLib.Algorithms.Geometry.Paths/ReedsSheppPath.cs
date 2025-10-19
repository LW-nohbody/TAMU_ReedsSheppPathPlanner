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


    /// Recalculates the total path length
    public override void ComputeLength()
    {
        double total = 0.0;
        foreach (var e in _elements)
        {
            // Distance is always non-negative
            total += Math.Abs(e.Distance);
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
}

