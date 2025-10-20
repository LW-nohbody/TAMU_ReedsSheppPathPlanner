using SimCore.Core;
namespace SimCore.Services;
public interface IScheduler {
  ITask NextTask(VehicleSpec spec, WorldState world, bool payloadFull);
}