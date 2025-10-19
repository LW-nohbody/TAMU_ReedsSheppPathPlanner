namespace PathPlanningLib.Algorithms.Geometry;

using PathPlanningLib.Algorithms.Geometry.PathElements;

public class MathUtils
{
    // Normalizes angle to [0,2π)
    public static double NormalizeAngle(double angle) 
    {
        double twoPi = 2 * Math.PI;
        angle %= twoPi;
        if (angle < 0) angle += twoPi;
        return angle;
    }

    // Converts Cartesian coordinates (x, y) into polar coordinates (rho, theta), with the angle wrapped to [0, 2π).
    public static (double rho, double theta) CartesianToPolar(double x, double y)
    {
        double rho = Math.Sqrt(x * x + y * y);
        double theta = NormalizeAngle(Math.Atan2(y, x));
        return (rho, theta);
    }

    // Pose-based ChangeOfBasis: returns end relative to start, in start's local frame
    public static Pose ChangeOfBasis(Pose start, Pose end)
    {
        // Vector from start to end
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;

        // Rotate into start's frame
        double cos = Math.Cos(-start.Theta);
        double sin = Math.Sin(-start.Theta);
        double xLocal = dx * cos - dy * sin;
        double yLocal = dx * sin + dy * cos;

        // Relative orientation
        double thetaLocal = NormalizeAngle(end.Theta - start.Theta);

        return new Pose(xLocal, yLocal, thetaLocal);
    }
}
