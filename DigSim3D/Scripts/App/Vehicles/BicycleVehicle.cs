using Godot;
using DigSim3D.Domain;
using DigSim3D.UI;
using DigSim3D.Services;
using PathPlanningLib.Algorithms.Geometry.Paths;
using PathPlanningLib.Algorithms.Geometry.PathElements;

namespace DigSim3D.App.Vehicles;

/// <summary>
/// Represents a simple bicycle vehicle model for simulation.
/// Handles movement, steering, and basic physics.
/// </summary>
public partial class BicycleVehicle : VehicleBody3D, IVehicle
{
    // Vehicle Specs
    public float VehicleLength { get; } = 1.1f;
    public float VehicleWidth { get; } = 0.5f;
    public float VehicleHeight { get; } = 0.7f;
    public float Wheelbase { get; } = 0.35f;
    public IHybridPlanner PathPlanner { get; } = new HybridReedsSheppPlanner();

    // How hard it drives forward (reduced for stability)
    [Export] public float DriveForce = 150f;

    // Turn Radius (meters) -> controls steering angle
    [Export] public float InitialTurnRadiusInMeters = 1.0f;
    private float _turnRadiusInMeters = 1.0f;
    public float TurnRadiusInMeters { 
        get => _turnRadiusInMeters; 
        set{
            _turnRadiusInMeters = value;
            SteeringAngle = Mathf.Atan(VehicleLength / value);
        }
    }
    
    [Export] public float InitialMaxSpeed = 1f; // Maximum speed in m/s
    private float _maxSpeedMetersPerSecond = 1f;
    public float MaxSpeedMetersPerSecond { 
        get => _maxSpeedMetersPerSecond; 
        set{
            _maxSpeedMetersPerSecond = value;
        }
    }
    public KinematicType KinType { get; } = KinematicType.Bicycle;

    private float SteeringAngle { get; set; } = 0f;
    
    // Stability settings
    [Export] public float AngularDamping = 2.0f; // Resist rotation (prevents tipping)
    [Export] public float TiltCorrectionStrength = 5.0f; // How aggressively to correct tilt
    [Export] public float MaxTiltAngle = 25f; // Maximum tilt in degrees before correction
    [Export] public bool EnableTiltCorrection = true; // Toggle auto-stabilization
    [Export] private VehicleNameplate _nameplate = null!;
    private ReedsSheppPath _path = null!;
    private int _i = 0;
    private bool _done = true;

    public void SetPath(IPath path)
    {
        _path = path as ReedsSheppPath;
        _i = 0;
        _done = false;
    }

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
        
        // Lower center of mass for better stability (Change back to auto for more realism)
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0, -0.3f, 0);
    }

    public void Activate(Transform3D transform)
    {
        GlobalTransform = transform;
        Activate();
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

    public override void _Ready()
    {
        SteeringAngle = Mathf.Atan(VehicleLength / InitialTurnRadiusInMeters);
    }

    public override void _PhysicsProcess(double delta)
    {   
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
        if (currentSpeed > MaxSpeedMetersPerSecond)
        {
            LinearVelocity = LinearVelocity.Normalized() * MaxSpeedMetersPerSecond;
        }
            
        // Steering
        // Constant forward engine force (with speed limiting)
        if (currentSpeed < MaxSpeedMetersPerSecond)
        {
            EngineForce = DriveForce;
        }
        else
        {
            EngineForce = 0f; // Stop accelerating at max speed
        }

        // Constant steering angle → drives in a circle
        Steering = SteeringAngle;

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

    public void FreezePhysics()
    {
        Freeze = true;
    }

    public void UnfreezePhysics()
    {
        Freeze = false;
    }

    public void addLayerToCollisionMask(int layer)
    {
        // Clamp layer to valid 1..32 range to avoid undefined shifts and keep within uint bits
        if (layer < 1) layer = 1;
        if (layer > 32) layer = 32;
        // Use unsigned literal so the shift result is uint and matches CollisionMask type
        uint layerBit = 1u << (layer - 1);
        CollisionMask |= layerBit;

        GD.Print($"[BicycleVehicle] Collision mask set to {layer} (layer {layerBit}) as {CollisionMask}.");
    }
}
