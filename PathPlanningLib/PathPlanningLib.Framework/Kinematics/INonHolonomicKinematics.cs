namespace PathPlanningLib.Vehicles.Kinematics;
public interface INonholonomicKinematics : IKinematicModel
{
    double MinTurningRadius { get; }
}

