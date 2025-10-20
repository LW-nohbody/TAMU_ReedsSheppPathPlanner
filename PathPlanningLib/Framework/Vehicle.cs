namespace PathPlanningLib.Framework;

using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Framework.Kinematics;

// vehicle class 
public class Vehicle
{
    public double? Width { get; set; }
    public double? Length { get; set; }
    public Pose? Pose { get; set; } //should a vehicle have a pose?
    public IKinematicModel<Path<PathElement>, PathElement>? KinematicModel { get; private set; }
    private IPathPlanner<Path<PathElement>, PathElement>? _planner;
    public IPathPlanner<Path<PathElement>, PathElement>? Planner
    {
        get => _planner;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (KinematicModel == null)
                throw new InvalidOperationException("KinematicModel must be set before setting Planner.");
            if (!KinematicModel.CompatiblePlanners.Contains(value.GetType()))
                throw new InvalidOperationException(
                    $"Planner {value.GetType().Name} is not compatible with {KinematicModel} kinematics."
                );
            _planner = value;
        }
    }

    public Vehicle(
        IKinematicModel<Path<PathElement>, PathElement> model = null,
        IPathPlanner<Path<PathElement>, PathElement> planner = null,
        double? width = null,
        double? length = null,
        Pose pose = null)
    {
        Width = width;
        Length = length;
        Pose = pose;

        KinematicModel = model ?? (IKinematicModel<Path<PathElement>, PathElement>) new NonHolonomicKinematics();

        Planner = planner ?? KinematicModel.OptimalPlanner; 
    }
    
    public IReadOnlyList<Type> GetCompatiblePlanners()
    {
        return KinematicModel.CompatiblePlanners;
    }

    public Path<PathElement> PlanPath(Pose start, Pose end)
    {
        if (Planner == null)
            throw new InvalidOperationException("No path planning algorithm set.");

        return Planner.GetOptimalPath(start, end);
    }

    public PosePath PlanPosePath(Pose start, Pose end, double stepSize)
    {
        if (Planner == null)
            throw new InvalidOperationException("No path planning algorithm set.");

        return Planner.GetOptimalPath(start, end).Sample(stepSize);
    }
}
