namespace PathPlanningLib.Vehicles
{
    using PathPlanningLib.Geometry;
    using PathPlanningLib.Vehicles.Kinematics;

    // Vehicle interface which defines what each vehicle should implement
    public interface IVehicle<TKinematics> where TKinematics : IKinematicModel
    {
        /// <summary>
        /// The current pose (position + orientation) of the vehicle.
        /// </summary>
        Pose Pose { get; set; }

        /// <summary>
        /// The underlying kinematic model governing motion constraints.
        /// </summary>
        TKinematics Kinematics { get; }

        /// <summary>
        /// Updates the vehicle’s pose based on control inputs and delta time.
        /// </summary>
        /// <param name="control">Control input (steering, speed, etc.)</param>
        /// <param name="deltaTime">Elapsed time in seconds</param>
        void Update(ControlInput control, double deltaTime);
    }
}
