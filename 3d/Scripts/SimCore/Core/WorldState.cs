using System.Collections.Generic;
using Godot;
namespace SimCore.Core;
public sealed class WorldState {
  public readonly List<Vector3> DigSites = new(); // centers
  public Vector3 DumpCenter;
}