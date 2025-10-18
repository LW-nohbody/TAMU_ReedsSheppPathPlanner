namespace PathPlanningLib.PathPlanners;
using PathPlanningLib.Geometry;
public interface IPathPlanner<TKinematics>
{
    Path PlanPath(
        Pose start,
        Pose goal,
        TKinematics model);
}
