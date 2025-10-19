namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.PathElements;

using System;
using System.Collections.Generic;

// Represents a geometric path as a sequence of poses.
public class PosePath : Path<Pose>
{
    /// Default constructor: empty path
    public PosePath() : base() { }

    /// Constructor from an enumerable of poses
    public PosePath(IEnumerable<Pose> poses) : base(poses) { }

    /// Adds a pose to the path
    public void AddPose(Pose pose)
    {
        base.Add(pose); // use base class Add
    }

    /// Clears the path
    public override void Clear()
    {
        base.Clear(); // clears _elements and sets Length to 0
    }

    /// Computes the total path length
    public override void ComputeLength()
    {
        double total = 0.0;
        for (int i = 1; i < _elements.Count; i++)
        {
            double dx = _elements[i].X - _elements[i - 1].X;
            double dy = _elements[i].Y - _elements[i - 1].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }
        Length = total;
    }
}

