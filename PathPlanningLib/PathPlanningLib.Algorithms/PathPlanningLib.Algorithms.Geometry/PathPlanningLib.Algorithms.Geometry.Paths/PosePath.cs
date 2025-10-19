namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.Paths;
using System;
using System.Collections.Generic;

// Represents a geometric path as a sequence of poses.
public class PosePath : Path
{
    // The ordered list of poses that make up the path.
    public List<Pose> Poses { get; }

    //The total length of the path (computed on demand with ComputeLength()).
    public double Length { get; private set; }

    // Constructs an empty path.
    public PosePath()
    {
        Poses = new List<Pose>();
        Length = 0.0;
    }

    // Constructs a path from a list of poses.
    public PosePath(IEnumerable<Pose> poses)
    {
        Poses = new List<Pose>(poses);
        ComputeLength();
    }

    // Adds a new pose to the path and updates the total length.
    public void AddPose(Pose pose)
    {
        if (Poses.Count > 0)
        {
            var lastPose = Poses[^1];
            double dx = pose.X - lastPose.X;
            double dy = pose.Y - lastPose.Y;
            Length += Math.Sqrt(dx * dx + dy * dy);
        }

        Poses.Add(pose);
    }

    // Recalculates the total path length.
    public void ComputeLength()
    {
        Length = 0.0;
        for (int i = 1; i < Poses.Count; i++)
        {
            double dx = Poses[i].X - Poses[i - 1].X;
            double dy = Poses[i].Y - Poses[i - 1].Y;
            Length += Math.Sqrt(dx * dx + dy * dy);
        }
    }

    // Returns true if the path has no poses.
    public bool IsEmpty() => Poses.Count == 0;

    // Clears the path.
    public void Clear()
    {
        Poses.Clear();
        Length = 0.0;
    }
}
