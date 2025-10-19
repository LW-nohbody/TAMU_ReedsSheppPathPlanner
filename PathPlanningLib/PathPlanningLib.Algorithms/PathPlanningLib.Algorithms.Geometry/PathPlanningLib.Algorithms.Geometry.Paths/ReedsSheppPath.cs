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
    public void AddElement(ReedsSheppElement element)
    {
        base.Add(element); // base class Add
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

    /// Clears the path
    public override void Clear()
    {
        base.Clear();
    }
}

