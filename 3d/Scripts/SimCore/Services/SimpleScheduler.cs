using Godot;
using SimCore.Core;
using System.Linq;

namespace SimCore.Services;
public sealed class SimpleScheduler : IScheduler {
  public ITask NextTask(VehicleSpec _, WorldState world, bool payloadFull)
  {
    if (payloadFull) return new DumpTask(world.DumpCenter);
    if (world.DigSites.Count == 0) return new IdleTask();
    // Select dig site with maximum remaining volume
    var site = world.DigSites.OrderByDescending(ds => ds.RemainingVolume).First();
    return new DigTask(site, site.ToolRadius, site.Depth);
  }
}