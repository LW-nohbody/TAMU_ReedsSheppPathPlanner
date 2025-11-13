namespace DigSim3D.Domain;

public enum KinematicType { ReedsShepp, Dubins, DiffDrive, CenterArticulated }
public readonly record struct VehicleSpec(
  string Name, KinematicType Kin, float Length, float Width, float Height,
  float TurnRadius, float MaxSpeed);