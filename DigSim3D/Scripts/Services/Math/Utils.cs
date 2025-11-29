namespace DigSim3D.Services;

public enum Steering { LEFT = -1, RIGHT = 1, STRAIGHT = 0 }
public enum Gear { FORWARD = 1, BACKWARD = -1 }

public record PathElement(double Param, Steering Steering, Gear Gear)
{
    public static PathElement Create(double param, Steering steering, Gear gear)
        => (param >= 0)
            ? new PathElement(param, steering, gear)
            : new PathElement(-param, steering, gear).ReverseGear();

    public PathElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };
    public PathElement ReverseGear() => this with { Gear = (Gear)(-(int)Gear) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Param, 3)} }}";
}

public static class Utils
{
    public static double M(double angle) // wrap to [0,2π)
    {
        double twoPi = 2 * Math.PI;
        angle %= twoPi;
        if (angle < 0) angle += twoPi;
        return angle;
    }

    public static (double rho, double theta) R(double x, double y)
    {
        double rho = Math.Sqrt(x * x + y * y);
        double theta = M(Math.Atan2(y, x));
        return (rho, theta);
    }

    // start/end: (x,y,thetaRadians). Returns end in start's local frame, theta in radians.
    public static (double x, double y, double theta) ChangeOfBasis(
        (double x, double y, double theta) start,
        (double x, double y, double theta) end)
    {
        double dx = end.x - start.x;
        double dy = end.y - start.y;
        double dtheta = M(end.theta - start.theta);

        double cos = Math.Cos(-start.theta);
        double sin = Math.Sin(-start.theta);
        double xNew = dx * cos - dy * sin;
        double yNew = dx * sin + dy * cos;
        return (xNew, yNew, dtheta);
    }
}
