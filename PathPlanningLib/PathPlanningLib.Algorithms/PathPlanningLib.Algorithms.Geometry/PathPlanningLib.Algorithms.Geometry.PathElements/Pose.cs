namespace PathPlanningLib.Algorithms.Geometry.PathElements;

using System;

// Pose PathElement class where theta is in **RADIANS**
public record Pose : PathElement
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Theta { get; init; }

    public static Pose Create(double x, double y, double theta)
        => new Pose { X = x, Y = y, Theta = theta };

    // Rotates the Pose by degrees degrees
    public Pose Rotate(double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        double newX = X * cos - Y * sin;
        double newY = X * sin + Y * cos;
        double newTheta = Theta + radians;

        // Normalize Theta to [0, 2π)
        newTheta = (newTheta % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);

        return this with { X = newX, Y = newY, Theta = newTheta };
    }

    public override string ToString()
        => $"Pose(X: {Math.Round(X, 3)}, Y: {Math.Round(Y, 3)}, Θ: {Math.Round(Theta, 2)} radians)";
}
