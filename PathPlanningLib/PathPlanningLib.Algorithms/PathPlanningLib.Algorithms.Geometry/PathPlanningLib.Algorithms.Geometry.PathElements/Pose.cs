namespace PathPlanningLib.Algorithms.Geometry.PathElements;

using System;

public record Pose : PathElement
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Theta { get; init; } // in degrees

    public static Pose Create(double x, double y, double thetaDegrees)
        => new Pose { X = x, Y = y, Theta = thetaDegrees };

    // Rotates the Pose by degrees degrees
    public Pose Rotate(double degrees)
    {
        double radians = degrees * Math.PI / 180.0;

        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        double newX = X * cos - Y * sin;
        double newY = X * sin + Y * cos;
        double newTheta = Theta + degrees;

        // Normalize Theta to [0, 360)
        newTheta = (newTheta % 360 + 360) % 360;

        return this with { X = newX, Y = newY, Theta = newTheta };
    }

    public override string ToString()
        => $"Pose(X: {Math.Round(X, 3)}, Y: {Math.Round(Y, 3)}, Θ: {Math.Round(Theta, 2)}°)";
}
