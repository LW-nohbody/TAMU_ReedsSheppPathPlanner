namespace PathPlanningLib.Framework.Kinematics;

using PathPlanningLib.Algorithms.Geometry;

/// Defines how a a vehicle's motion via kinematics 
public interface IKinematicModel
{
    // int DegreesOfFreedom { get; }
    IPathPlanner<Path, PathElement> OptimalPlanner { get; }
    IReadOnlyList<Type> CompatiblePlanners { get; }
}
