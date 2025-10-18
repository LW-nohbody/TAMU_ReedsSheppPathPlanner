namespace PathPlanningLib.Framework;
using PathPlanningLib.Geometry;
using PathPlanningLib.Vehicles.Kinematics;

// vehicle class 
public class Vehicle
{
    public double width { get; set; }
    public double length { get; set; }
    public Pose pose { get; set; }
    public IKinematicModel KinematicModel { get; set; }

    public Vehicle(IKinematicModel? kinematics = null, Pose? initialPose = null)
    {
        Kinematics = kinematics;
        Pose = initialPose ?? new Pose(0, 0, 0);
    }

    public virtual void Update(ControlInput control, double deltaTime)
    {
        Pose = Kinematics.Propagate(Pose, control, deltaTime);
    }
}
