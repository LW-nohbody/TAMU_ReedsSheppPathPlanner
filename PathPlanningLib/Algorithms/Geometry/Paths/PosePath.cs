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
    public override void Add(Pose pose)
    {
        base.Add(pose); // use base class Add
    }

    /// Removes the first occurrence of a given element from the path.
    public override bool Remove(Pose pose)
    {
        return base.Remove(pose);
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

    // Only allows oversampling right now
    public override PosePath Sample(double stepSize)
    {
        if (stepSize <= 0)
            throw new ArgumentException("Step size must be positive.", nameof(stepSize));

        // use interpolation for sampling
        var sampledPoses = new List<Pose>();
        if (this.Count == 0)
            return new PosePath(sampledPoses);

        Pose lastPose = this.Elements[0];
        sampledPoses.Add(lastPose);

        for (int i = 1; i < this.Count; i++)
        {
            Pose current = this.Elements[i];
            double dx = current.X - lastPose.X;
            double dy = current.Y - lastPose.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            int nSteps = Math.Max(1, (int)Math.Floor(dist / stepSize));
            for (int s = 1; s <= nSteps; s++)
            {
                // interpolation factor [0,1]
                double t = (double)s / nSteps;

                // perform interpolation
                double interpX = lastPose.X + t * dx;
                double interpY = lastPose.Y + t * dy;
                double interpTheta = MathUtils.NormalizeAngle(
                    lastPose.Theta + t * MathUtils.ShortestAngularDistance(lastPose.Theta, current.Theta)
                );
                sampledPoses.Add(Pose.Create(interpX, interpY, interpTheta));
            }

            lastPose = current;
        }

        return new PosePath(sampledPoses);
    }
}

