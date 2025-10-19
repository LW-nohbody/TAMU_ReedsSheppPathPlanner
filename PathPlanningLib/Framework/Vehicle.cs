namespace PathPlanningLib.Framework;

using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Framework.Kinematics;

// vehicle class 
public class Vehicle
{
    public double width { get; set; }
    public double length { get; set; }
    public Pose pose { get; set; } //should a vehicle have a pose?
    public IKinematicModel KinematicModel { get; set; }

    // public Vehicle(IKinematicModel? kinematics = null, Pose? initialPose = null)
    // {
    //     Kinematics = kinematics;
    //     Pose pose = initialPose ?? new Pose(0, 0, 0);
    // }

    // public virtual void Update(ControlInput control, double deltaTime)
    // {
    //     Pose pose = Kinematics.Propagate(Pose, control, deltaTime);
    // }
}
