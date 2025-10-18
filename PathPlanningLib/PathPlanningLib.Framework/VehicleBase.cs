namespace PathPlanningLib.Vehicles;
using PathPlanningLib.Geometry;
using PathPlanningLib.Vehicles.Kinematics;

// vehicle base class which each kinematic type vehicle will inherit from
public abstract class VehicleBase<TKinematics> : IVehicle<TKinematics>
    where TKinematics : IKinematicModel
{
    public double Width { get; set; }
    public double Length { get; set; }
    public Pose Pose { get; set; }
    public TKinematics Kinematics { get; }

    protected VehicleBase(TKinematics kinematics, Pose? initialPose = null)
    {
        Kinematics = kinematics;
        Pose = initialPose ?? new Pose(0, 0, 0);
    }

    public virtual void Update(ControlInput control, double deltaTime)
    {
        Pose = Kinematics.Propagate(Pose, control, deltaTime);
    }
}
