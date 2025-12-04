using Godot;
using DigSim3D.Domain;
using DigSim3D.Services;
using PathPlanningLib.Algorithms;
using PathPlanningLib.Algorithms.Geometry;
using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;

namespace DigSim3D.App.Vehicles;


public interface IVehicle
{
    public Transform3D GlobalTransform { get; set; }
    public Vector3 GlobalPosition { get; set; }
    public float VehicleLength { get; }
    public float VehicleWidth { get; }
    public KinematicType KinType { get; }
    public float TurnRadiusInMeters { get; set; }
    public float MaxSpeedMetersPerSecond { get; set; }
    IHybridPlanner PathPlanner { get; }
    void Activate();
    void Activate(Transform3D transform);
    void Deactivate();

    public void SetPhysicsProcess(bool enable);
    public void SetPath(IPath path);
    public void InitializeID(int ID);
    public void FreezePhysics();
    public void UnfreezePhysics();
    public void addLayerToCollisionMask(int layer);
}
