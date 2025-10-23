namespace PathPlanningLib.Algorithms;

using System.Collections.Generic;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Algorithms.Geometry.PathElements;

/// Generic planner interface shared by Dubins / Reeds–Shepp (and future planners).
public interface IPathPlanner<TPath, TElement>
    where TPath : Path<TElement>
    where TElement : PathElement
{
    IEnumerable<TPath> GetAllPaths(Pose start, Pose end);
    TPath GetOptimalPath(Pose start, Pose end);
}