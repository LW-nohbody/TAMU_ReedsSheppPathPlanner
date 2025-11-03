using Godot;

namespace SimCore.Core
{
    public partial class VehicleBrain : Node
    {
        public VehicleAgent3D Agent { get; private set; }

        public override void _Ready()
        {
            Agent = GetParent<VehicleAgent3D>();
        }
    }
}
