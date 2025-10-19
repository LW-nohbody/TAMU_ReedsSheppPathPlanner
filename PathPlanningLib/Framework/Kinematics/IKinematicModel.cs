namespace PathPlanningLib.Framework.Kinematics;

using PathPlanningLib.Algorithms.Geometry;

/// Defines how a vehicle's pose evolves over time under a given control input.
public interface IKinematicModel
{
    // int DegreesOfFreedom { get; }
    // bool IsHolonomic { get; }
    //Pose Propagate(Pose currentPose, ControlInput control, double deltaTime);
}
