using DigSim3D.Domain;
namespace DigSim3D.Services;

public interface IScheduler
{
  ITask NextTask(VehicleSpec spec, WorldState world, bool payloadFull);
}