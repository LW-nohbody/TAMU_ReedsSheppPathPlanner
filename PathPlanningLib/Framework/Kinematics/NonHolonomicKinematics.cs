namespace PathPlanningLib.Framework.Kinematics;

using PathPlanningLib.Algorithms.ReedsShepp;
using PathPlanningLib.Algorithms.Dubins;

// Default Kinematic Model type
public class NonHolonomicKinematics : IKinematicModel
{
    double TurningRadius { get; set; }
    double MaxVelocity { get; set; }

    public NonHolonomicKinematics(double turningRadius, double maxVelocity)
    {
        TurningRadius = turningRadius;
        MaxVelocity = maxVelocity;
    }
    public IPathPlanner<ReedsSheppPath, ReedsSheppElement> OptimalPlanner { get; } = new ReedsShepp();
    public IReadOnlyList<Type> CompatiblePlanners { get; } = new List<Type>
    {
        typeof(ReedsShepp),
        typeof(Dubins)
    };
}

