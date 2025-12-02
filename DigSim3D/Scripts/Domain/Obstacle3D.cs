using Godot;
using System;

namespace DigSim3D.Domain
{
    /// <summary>
    /// Abstract class for obstacles
    /// </summary>
    public abstract partial class Obstacle3D : Node3D
    {
        // Optional visualization
        [Export] public Color DebugColor = new Color(1, 0, 0, 0.3f);

        public abstract bool ContainsPoint(Vector3 point);
        public abstract bool IntersectsSegment(Vector3 start, Vector3 end);
    }
}