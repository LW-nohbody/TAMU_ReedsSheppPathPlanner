namespace PathPlanningLib.Framework.Kinematics;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;

/// Defines a vehicle's motion via kinematics 
public interface IKinematicModel<TPath, TElement>
    where TPath : Path<TElement>
    where TElement : PathElement
{
    // int DegreesOfFreedom { get; }
    public IPathPlanner<TPath, TElement> OptimalPlanner { get; }
    public IReadOnlyList<Type> CompatiblePlanners { get; }
    public IEnumerable<string> MissingParameters { get; }
    public bool ParametersMissing { get; }
}
