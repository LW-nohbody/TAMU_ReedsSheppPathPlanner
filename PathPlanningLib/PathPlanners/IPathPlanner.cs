namespace PathPlanningLib.PathPlanners
{
    public interface IPathPlanner<TKinematics>
    {
        Path PlanPath(
            Pose start,
            Pose goal,
            TKinematics model);
    }
}