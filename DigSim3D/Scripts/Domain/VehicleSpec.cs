namespace DigSim3D.Domain;

/// <summary>
/// Vehicle's kinemtatic types
/// </summary>
public enum KinematicType { ReedsShepp, Dubins, DiffDrive, CenterArticulated }

/// <summary>
/// Vehicle specifications
/// </summary>
/// <param name="Name"></param>
/// <param name="Kin"></param>
/// <param name="Length"></param>
/// <param name="Width"></param>
/// <param name="Height"></param>
/// <param name="TurnRadius"></param>
/// <param name="MaxSpeed"></param>
public readonly record struct VehicleSpec(
  string Name, KinematicType Kin, float Length, float Width, float Height,
  float TurnRadius, float MaxSpeed);