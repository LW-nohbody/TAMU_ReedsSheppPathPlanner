namespace PathPlanningLib.Geometry;
using System.Collections.Generic;

/// <summary>
/// Represents a geometric path as a sequence of poses.
/// </summary>
public class Path
{
    /// <summary>
    /// The ordered list of poses that make up the path.
    /// </summary>
    public List<Pose> Poses { get; }

    /// <summary>
    /// Optional: The total length of the path (could be computed on demand).
    /// </summary>
    public double Length { get; private set; }

    /// <summary>
    /// Constructs an empty path.
    /// </summary>
    public Path()
    {
        Poses = new List<Pose>();
        Length = 0.0;
    }

    /// <summary>
    /// Constructs a path from a list of poses.
    /// </summary>
    /// <param name="poses">Initial sequence of poses</param>
    public Path(IEnumerable<Pose> poses)
    {
        Poses = new List<Pose>(poses);
        ComputeLength();
    }

    /// <summary>
    /// Adds a new pose to the path and updates the total length.
    /// </summary>
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

    /// <summary>
    /// Recalculates the total path length.
    /// </summary>
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

    /// <summary>
    /// Returns true if the path has no poses.
    /// </summary>
    public bool IsEmpty() => Poses.Count == 0;

    /// <summary>
    /// Clears the path.
    /// </summary>
    public void Clear()
    {
        Poses.Clear();
        Length = 0.0;
    }
}
