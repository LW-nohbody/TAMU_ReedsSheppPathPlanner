namespace PathPlanningLib.Framework;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Path;
using PathPlanningLib.Framework.Kinematics;

// vehicle class 
public class Vehicle
{
    public double? Width { get; set; }
    public double? Length { get; set; }
    public Pose Pose { get; set; } //should a vehicle have a pose?
    public IKinematicModel KinematicModel { get; private set; }
    private IPathPlanner<Path, PathElement> _planner;
    public IPathPlanner<Path, PathElement> Planner
    {
        get => _planner;
        set
        {
            if (!KinematicModel.CompatiblePlanners.Contains(value.GetType()))
                throw new InvalidOperationException(
                    $"Planner {value.GetType().Name} is not compatible with {KinematicModel} kinematics."
                );
            _planner = value;
        }
    }

    public Vehicle(
        IKinematicModel model = null,
        IPathPlanner planner = null,
        double? width = null,
        double? length = null,
        Pose pose = null)
    {
        Width = width;
        Length = length;
        Pose = pose;

        KinematicModel = model ?? new NonHolonomicKinematics();

        Planner = planner ?? KinematicModel.OptimalPlanner; 
    }
    
    public IReadOnlyList<Type> GetCompatiblePlanners()
    {
        return KinematicModel.CompatiblePlanners;
    }

    public Path PlanPath(Pose start, Pose end)
    {
        if (Planner == null)
            throw new InvalidOperationException("No planner set.");

        return Planner.GetOptimalPath(start, end);
    }

    public PosePath PlanPosePath(Pose start, Pose end, double stepSize)
    {
        if (Planner == null)
            throw new InvalidOperationException("No planner set.");

        return Planner.GetOptimalPath(start, end).Sample(stepSize);
    }
}
