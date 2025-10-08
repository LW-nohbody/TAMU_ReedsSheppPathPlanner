namespace PathPlanningLib.Vehicles.Kinematics
{
    using PathPlanningLib.Geometry;
    using PathPlanningLib.Vehicles;
    using System;

    public class DifferentialDriveKinematics : INonholonomicKinematics
    {
        public double WheelRadius { get; }
        public double WheelSeparation { get; }
        public double MinTurningRadius => WheelSeparation / 2.0;

        public DifferentialDriveKinematics(double wheelRadius, double wheelSeparation)
        {
            WheelRadius = wheelRadius;
            WheelSeparation = wheelSeparation;
        }

        public Pose Propagate(Pose current, ControlInput control, double dt)
        {
            double v = control.LinearVelocity;
            double omega = control.AngularVelocity;

            double newX = current.X + v * Math.Cos(current.Theta) * dt;
            double newY = current.Y + v * Math.Sin(current.Theta) * dt;
            double newTheta = current.Theta + omega * dt;

            return new Pose(newX, newY, newTheta);
        }
    }
}
