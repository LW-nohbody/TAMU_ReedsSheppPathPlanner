using System.Collections.Generic;
using Godot;

namespace DigSim3D.Domain
{
    public sealed class PlannedPath
    {
        public readonly List<Vector3> Points = new();
        public readonly List<int> Gears = new();   // +1 forward, -1 reverse
    }
}