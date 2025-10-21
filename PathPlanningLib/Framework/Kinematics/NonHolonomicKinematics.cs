namespace PathPlanningLib.Framework.Kinematics;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Algorithms.ReedsShepp;
using PathPlanningLib.Algorithms.Dubins;

// Default Kinematic Model type
public class NonHolonomicKinematics : IKinematicModel<ReedsSheppPath, ReedsSheppElement>
{
    private double? _turningRadius;
    private double? _maxVelocity;
    public double? TurningRadius
    {
        get => _turningRadius;
        set
        {
            _turningRadius = value;
            UpdateMissingParameters();
        }
    }
    public double? MaxVelocity { 
        get => _maxVelocity;
        set
        {
            _maxVelocity = value;
            UpdateMissingParameters();
        } 
    }
    public bool ParametersMissing { get; private set; } = true;

    public IEnumerable<string> MissingParameters
    {
        get
        {
            if (TurningRadius == null)
                yield return "TurningRadius";

            if (MaxVelocity == null)
                yield return "MaxVelocity";
        }
    }

    public IPathPlanner<ReedsSheppPath, ReedsSheppElement> OptimalPlanner { get; } = new ReedsShepp();
    public IReadOnlyList<Type> CompatiblePlanners { get; } = new List<Type>
    {
        typeof(ReedsShepp),
        typeof(Dubins)
    };

    public NonHolonomicKinematics() { }
    public NonHolonomicKinematics(double turningRadius, double maxVelocity)
    {
        TurningRadius = turningRadius;
        MaxVelocity = maxVelocity;
    }

    private void UpdateMissingParameters()
    {
        ParametersMissing = (_turningRadius == null || _maxVelocity == null);
    }

}

