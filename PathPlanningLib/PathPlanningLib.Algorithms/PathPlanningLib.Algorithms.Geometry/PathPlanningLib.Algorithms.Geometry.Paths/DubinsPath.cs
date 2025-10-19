namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Geometry.PathElements;

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

    /// Recalculates the total path length
    public override void ComputeLength()
    {
        double total = 0.0;
        foreach (var e in _elements)
        {
            // distances are always non-negative
            total += e.Distance; 
        }

        Length = total;
    }
}