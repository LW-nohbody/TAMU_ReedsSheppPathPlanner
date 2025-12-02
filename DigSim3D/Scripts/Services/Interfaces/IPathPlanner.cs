using DigSim3D.Domain;
using DigSim3D.App.Vehicles;

namespace DigSim3D.Services;

public interface IPathPlanner
{
  PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world);
}