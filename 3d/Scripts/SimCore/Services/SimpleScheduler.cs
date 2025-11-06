using Godot;
using SimCore.Core;
namespace SimCore.Services;
public sealed class SimpleScheduler : IScheduler {
  private int _nextSite = 0;
  public ITask NextTask(VehicleSpec _, WorldState world, bool payloadFull)
  {
    if (payloadFull) return new DumpTask(world.DumpCenter);
    if (world.DigSites.Count == 0) return new IdleTask();
    var site = world.DigSites[_nextSite % world.DigSites.Count];
    _nextSite++;
    return new DigTask(site, ToolRadius:0.6f, Depth:0.12f);
  }
}