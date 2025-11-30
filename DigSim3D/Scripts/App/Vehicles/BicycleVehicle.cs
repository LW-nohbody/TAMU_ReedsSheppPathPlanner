using Godot;

namespace DigSim3D.App.Vehicles;

/// <summary>
/// Represents a simple bicycle vehicle model for simulation.
/// Handles movement, steering, and basic physics.
/// </summary>
public partial class BicycleVehicle : VehicleBody3D
{
    // private float _steeringAngle = 0f; // Radians
    // private float _speed = 0f; // Meters per second

    // private bool IsAccelerating = false;
    // private bool IsBraking = false;
    // private bool IsSteeringLeft = false;
    // private bool IsSteeringRight = false;

    // [Export] public float MaxSteeringAngle = MathF.PI / 4; // 45 degrees
    // [Export] public float MaxSpeed = 10f; // m/s
    // [Export] public float Acceleration = 2f; // m/s²
    // [Export] public float Deceleration = 4f; // m/s²
    // [Export] public float WheelBase = 1.0f; // Distance between front and rear axles in meters

    // How hard it drives forward
    [Export] public float DriveForce = 20f;

    // Steering angle in radians (0.3 ≈ 17 degrees)
    [Export] public float SteerAngle = 0.3f;

    public override void _PhysicsProcess(double delta)
    {
        // Steering
         // Constant forward engine force
        EngineForce = DriveForce;

        // Constant steering angle → drives in a circle
        Steering = SteerAngle;

        // No user input needed.
        Brake = 0f;

        // // Update speed based on acceleration/deceleration inputs
        // if (IsAccelerating)
        // {
        //     _speed += Acceleration * delta;
        // }
        // else if (IsBraking)
        // {
        //     _speed -= Deceleration * delta;
        // }
        // _speed = MathF.Clamp(_speed, 0f, MaxSpeed);

        // // Update steering angle based on input
        // if (IsSteeringLeft)
        // {
        //     _steeringAngle += (MaxSteeringAngle / 1.0f) * delta; // Full turn in 1 second
        // }
        // else if (IsSteeringRight)
        // {
        //     _steeringAngle -= (MaxSteeringAngle / 1.0f) * delta;
        // }
        // _steeringAngle = MathF.Clamp(_steeringAngle, -MaxSteeringAngle, MaxSteeringAngle);

        // // Calculate turning radius and update position/orientation
        // if (_steeringAngle != 0f)
        // {
        //     float turnRadius = WheelBase / MathF.Tan(_steeringAngle);
        //     float angularVelocity = _speed / turnRadius; // radians per second

        //     Rotation += angularVelocity * delta;
        // }

        // // Move forward based on current speed and orientation
        // Vector3 forward = new Vector3(MathF.Sin(Rotation), 0, MathF.Cos(Rotation));
        // GlobalPosition += forward * _speed * delta;
    }
}
