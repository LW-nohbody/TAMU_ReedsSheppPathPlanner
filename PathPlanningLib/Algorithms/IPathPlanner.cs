namespace PathPlanningLib.Algorithms;

using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;

public interface IPathPlanner<out TPath, out TElement>
    where TPath : Path<TElement>
    where TElement : PathElement
{
    IEnumerable<TPath> GetAllPaths(Pose start, Pose end);
    TPath GetOptimalPath(Pose start, Pose end);
}
