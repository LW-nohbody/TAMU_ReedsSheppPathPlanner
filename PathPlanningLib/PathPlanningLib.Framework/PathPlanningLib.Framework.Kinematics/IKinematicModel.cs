namespace PathPlanningLib.Framework.Kinematics;
using PathPlanningLib.Geometry;

/// <summary>
/// Defines how a vehicle's pose evolves over time under a given control input.
/// </summary>
public interface IKinematicModel
{
    // int DegreesOfFreedom { get; }
    // bool IsHolonomic { get; }
    Pose Propagate(Pose currentPose, ControlInput control, double deltaTime);
}
