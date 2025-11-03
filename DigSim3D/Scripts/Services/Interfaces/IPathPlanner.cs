using DigSim3D.Domain;
namespace DigSim3D.Services;

public interface IPathPlanner
{
  PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world);
}