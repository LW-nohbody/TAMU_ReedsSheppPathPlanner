namespace PathPlanningLib.Vehicles.Kinematics
{
    public interface INonholonomicKinematics : IKinematicModel
    {
        double MinTurningRadius { get; }
    }

    public interface IHolonomicKinematics : IKinematicModel
    {
        // Might allow lateral motion or omnidirectional control
    }
}