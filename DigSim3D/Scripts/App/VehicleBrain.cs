using Godot;

namespace DigSim3D.App
{
    public partial class VehicleBrain : Node
    {
        public VehicleVisualizer Agent { get; private set; } = null!;

        public override void _Ready()
        {
            Agent = GetParent<VehicleVisualizer>();
        }
    }
}
