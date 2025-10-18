namespace PathPlanningLib.Vehicles.Kinematics;
using PathPlanningLib.Geometry;

/// <summary>
/// Defines how a vehicle's pose evolves over time under a given control input.
/// </summary>
public interface IKinematicModel
{
    // int DegreesOfFreedom { get; }
    // bool IsHolonomic { get; }

    /// <summary>
    /// Simulates how the vehicle moves given its current pose, control input, and elapsed time.
    /// </summary>
    /// <param name="currentPose">The vehicle's current pose.</param>
    /// <param name="control">The control input (velocity, steering, etc.).</param>
    /// <param name="deltaTime">Elapsed time in seconds.</param>
    /// <returns>The updated pose after applying the control.</returns>
    Pose Propagate(Pose currentPose, ControlInput control, double deltaTime);
}
