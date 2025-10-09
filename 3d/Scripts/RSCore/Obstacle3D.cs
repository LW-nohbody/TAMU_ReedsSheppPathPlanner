using Godot;
using System;


namespace RSCore
{
    public abstract partial class Obstacle3D : Node3D
    {
        // Optional visualization
        [Export] public Color DebugColor = new Color(1, 0, 0, 0.3f);

        public abstract bool ContainsPoint(Vector3 point);
        public abstract bool IntersectsSegment(Vector3 start, Vector3 end);
    }
}