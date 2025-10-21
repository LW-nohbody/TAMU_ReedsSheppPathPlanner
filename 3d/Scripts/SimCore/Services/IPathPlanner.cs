using SimCore.Core;
namespace SimCore.Services;
public interface IPathPlanner {
  PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world);
}