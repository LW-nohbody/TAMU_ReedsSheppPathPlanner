using DigSim3D.Domain;
namespace DigSim3D.Services;

/// <summary>
/// Interface for the HybridPathPlanners, allows for polymorphism with vehicles
/// </summary>
public interface IPathPlanner
{
  PlannedPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world);
}