using Godot;
using DigSim3D.Domain;
using DigSim3D.UI;
using DigSim3D.Services;

namespace DigSim3D.App.Vehicles;

/// <summary>
/// Represents a simple bicycle vehicle model for simulation.
/// Handles movement, steering, and basic physics.
/// </summary>
public partial class BicycleVehicle : VehicleBody3D, IVehicle
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


    // Vehicle Specs
    [Export] public float VehicleLength { get; set; } = 2.0f;
    [Export] public float VehicleWidth { get; set; } =  0.7f;
    [Export] public float VehicleHeight { get; set; }    =  0.7f;

    // How hard it drives forward (reduced for stability)
    [Export] public float DriveForce = 150f;

    // Steering angle in radians (reduced for stability: 0.2 ≈ 11 degrees)
    [Export] public float SteerAngle = 0.2f;
    
    // Stability settings
    [Export] public float MaxSpeed = 1f; // Maximum speed in m/s
    [Export] public float AngularDamping = 2.0f; // Resist rotation (prevents tipping)
    [Export] public float TiltCorrectionStrength = 5.0f; // How aggressively to correct tilt
    [Export] public float MaxTiltAngle = 25f; // Maximum tilt in degrees before correction
    [Export] public bool EnableTiltCorrection = true; // Toggle auto-stabilization
    [Export] private VehicleNameplate _nameplate = null!;

    public bool isPaused { get; set; } = false;
    public VehicleSpec Spec => new VehicleSpec(
        KinematicType.Bicycle,
        0.5f,
        10f);

    private Vector3[] _path = Array.Empty<Vector3>();
    private int[] _gears = Array.Empty<int>();
    private int _i = 0;
    private bool _done = true;
    private Vector3 _finalPosXZ = Vector3.Zero;  // last waypoint XZ
    private Vector3 _finalAimXZ = Vector3.Zero;  // unit XZ forward to settle to

    public void SetPath(Vector3[] pts, int[] gears)
    {
        _path = pts ?? Array.Empty<Vector3>();
        _gears = gears ?? Array.Empty<int>();
        _i = 0;
        _done = _path.Length == 0;

        // Smooth landing setup
        _finalPosXZ = (_path.Length > 0) ? _path[^1].WithY(0) : Vector3.Zero;
        _finalAimXZ = Vector3.Zero;
    }
    public void SetPath(Vector3[] pts) => SetPath(pts, Array.Empty<int>());

    public void Activate()
    {
        this.Visible = true;
        ProcessMode = ProcessModeEnum.Inherit; // Change from Disabled (4) to Inherit
        SetPhysicsProcess(true);
        SetProcess(true);
        Sleeping = false;
        
        // Force the vehicle to be affected by physics
        Freeze = false;
        CanSleep = false; // Prevent sleeping immediately after spawn
        
        // Force physics activation
        ContactMonitor = true;
        MaxContactsReported = 4;
        
        // Apply stability settings
        AngularDamp = AngularDamping; // Resist unwanted rotation
        LinearDamp = 0.2f; // Slight linear damping for smoother movement
        
        // Lower center of mass for better stability
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0, -0.3f, 0); // Lower the center of mass
    }

    public void Deactivate()
    {
        this.Visible = false;
        ProcessMode = ProcessModeEnum.Disabled;
        SetPhysicsProcess(false);
        SetProcess(false);
        Sleeping = true;
        Freeze = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (isPaused)
            return;
        
        // Ensure physics is active
        if (Sleeping)
        {
            Sleeping = false;
        }
        
        // Apply tilt correction to prevent tipping
        if (EnableTiltCorrection)
        {
            ApplyTiltCorrection();
        }
        
        // Limit maximum speed for stability
        float currentSpeed = LinearVelocity.Length();
        if (currentSpeed > MaxSpeed)
        {
            LinearVelocity = LinearVelocity.Normalized() * MaxSpeed;
        }
            
        // Steering
        // Constant forward engine force (with speed limiting)
        if (currentSpeed < MaxSpeed)
        {
            EngineForce = DriveForce;
        }
        else
        {
            EngineForce = 0f; // Stop accelerating at max speed
        }

        // Constant steering angle → drives in a circle
        Steering = SteerAngle;

        // No user input needed.
        Brake = 0f;        // // Update speed based on acceleration/deceleration inputs
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
    
    /// <summary>
    /// Apply corrective torque to keep vehicle upright on uneven terrain
    /// </summary>
    private void ApplyTiltCorrection()
    {
        // Get the vehicle's up vector in world space
        Vector3 vehicleUp = GlobalTransform.Basis.Y;
        Vector3 worldUp = Vector3.Up;
        
        // Calculate tilt angle
        float tiltAngle = Mathf.RadToDeg(Mathf.Acos(vehicleUp.Dot(worldUp)));
        
        // Only apply correction if tilted beyond threshold
        if (tiltAngle > MaxTiltAngle)
        {
            // Calculate correction axis (perpendicular to both up vectors)
            Vector3 correctionAxis = vehicleUp.Cross(worldUp).Normalized();
            
            // Apply counter-torque to straighten the vehicle
            if (correctionAxis.LengthSquared() > 0.01f) // Avoid near-zero vectors
            {
                float correctionMagnitude = (tiltAngle - MaxTiltAngle) * TiltCorrectionStrength;
                ApplyTorque(correctionAxis * correctionMagnitude);
            }
        }
        
        // Also dampen excessive angular velocity to prevent wild spinning
        if (AngularVelocity.Length() > 3.0f)
        {
            AngularVelocity *= 0.9f; // Dampen by 10% each frame when spinning too fast
        }
    }

    public void InitializeID(int ID)
    {
        if (_nameplate != null)
        {
            if (ID < 10) {
                _nameplate.SetText($"Bicycle-0{ID}");
            } else {
                _nameplate.SetText($"Bicycle-{ID}"); 
            }
            
        } else {
            GD.PushError("[BicycleVehicle] Nameplate node is not assigned.");
        }
    }
}
