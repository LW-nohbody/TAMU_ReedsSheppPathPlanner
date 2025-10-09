namespace PathPlanningLib.Vehicles.Kinematics;
using System;

public class DifferentialDriveKinematics : IKinematicModel
{
    public double WheelRadius { get; }
    public double WheelSeparation { get; }
    public double MinTurningRadius => WheelSeparation / 2.0;

    public DifferentialDriveKinematics(double wheelRadius, double wheelSeparation)
    {
        WheelRadius = wheelRadius;
        WheelSeparation = wheelSeparation;
    }

    public Pose Propagate(Pose start, ControlInput input, double detlaTime)
    {
        // Determine movement distance based on gear
        double distance = input.Distance * Math.Sign(input.Gear);

        double theta = start.Theta;
        double x = start.X;
        double y = start.Y;

        // If steering is nearly straight, use straight-line approximation
        if (Math.Abs(input.SteeringAngle) < 1e-6)
        {
            double dx = distance * Math.Cos(theta);
            double dy = distance * Math.Sin(theta);
            return new Pose(x + dx, y + dy, theta);
        }
        else
        {
            // Turning radius based on steering angle
            double radius = distance / input.SteeringAngle; // simple approximation
            double dtheta = input.SteeringAngle;

            double cx = x - radius * Math.Sin(theta);
            double cy = y + radius * Math.Cos(theta);

            double newTheta = theta + dtheta;
            double newX = cx + radius * Math.Sin(newTheta);
            double newY = cy - radius * Math.Cos(newTheta);

            return new Pose(newX, newY, newTheta);
        }
    }
}

