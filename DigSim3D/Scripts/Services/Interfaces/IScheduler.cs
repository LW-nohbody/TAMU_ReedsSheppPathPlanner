using DigSim3D.Domain;
namespace DigSim3D.Services;

/// <summary>
/// Interface for scheduling tasks, allows for polymorphism with vehicles
/// </summary>
public interface IScheduler
{
  ITask NextTask(VehicleSpec spec, WorldState world, bool payloadFull);
}