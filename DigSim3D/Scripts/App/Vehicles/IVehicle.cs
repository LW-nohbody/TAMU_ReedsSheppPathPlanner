using Godot;
using DigSim3D.Domain;

namespace DigSim3D.App.Vehicles;

public interface IVehicle
{
    public bool isPaused { get; set; }
    public Transform3D GlobalTransform { get; set; }
    public Vector3 GlobalPosition { get; set; }
    public VehicleSpec Spec { get; }
    public float VehicleLength { get; }
   public float VehicleWidth { get; }

    void Activate();
    void Activate(Transform3D transform);
    void Deactivate();

    public void SetPhysicsProcess(bool enable);
    public void SetPath(Vector3[] pts, int[] gears);
    public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());
    public void InitializeID(int ID);
    public void FreezePhysics();
    public void UnfreezePhysics();
}