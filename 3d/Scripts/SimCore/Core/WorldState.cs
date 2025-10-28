using System.Collections.Generic;
using Godot;
namespace SimCore.Core;
public sealed class WorldState {
  public readonly List<DigSite> DigSites = new(); // dig sites with volume, radius, depth
  public Vector3 DumpCenter;
  public float TotalDirtExtracted = 0f; // cumulative dirt removed from world
}