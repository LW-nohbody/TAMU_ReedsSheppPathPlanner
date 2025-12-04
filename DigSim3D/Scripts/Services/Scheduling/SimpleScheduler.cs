using Godot;
using DigSim3D.Domain;
namespace DigSim3D.Services;

/// <summary>
/// Assigns tasks to vehicle based on vehicle and world state, implements the IScheduler interface
/// </summary>
public sealed class SimpleScheduler : IScheduler
{
  private int _nextSite = 0;

  /// <summary>
  /// Implemented from IScheduler
  /// Assigns the vehicle with its next task based on vehicle and world state
  /// </summary>
  /// <param name="_"></param>
  /// <param name="world"></param>
  /// <param name="payloadFull"></param>
  /// <returns></returns>
  public ITask NextTask(VehicleSpec _, WorldState world, bool payloadFull)
  {
    if (payloadFull) return new DumpTask(world.DumpCenter);
    if (world.DigSites.Count == 0) return new IdleTask();
    var site = world.DigSites[_nextSite % world.DigSites.Count];
    _nextSite++;
    return new DigTask(site, ToolRadius: 0.6f, Depth: 0.12f);
  }
}