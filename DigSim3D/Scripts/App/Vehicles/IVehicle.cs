using Godot;
using DigSim3D.Domain;

namespace DigSim3D.App.Vehicles;

public interface IVehicle
{
    public bool isPaused { get; set; }
    public Transform3D GlobalTransform { get; set; }
    public VehicleSpec Spec { get; }
    public float VehicleLength { get; }
   public float VehicleWidth { get; }

    void Activate();
    void Deactivate();
    // void TeleportTo(Vector3 pos, Quaternion rot);

    public void SetPhysicsProcess(bool enable);
    public void SetPath(Vector3[] pts, int[] gears);
    public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());
}