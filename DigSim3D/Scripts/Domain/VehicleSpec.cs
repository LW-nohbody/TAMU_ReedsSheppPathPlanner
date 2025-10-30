namespace DigSim3D.Domain;

public enum KinematicType { ReedsShepp, Dubins, DiffDrive, ArticulatedBus }
public readonly record struct VehicleSpec(
  string Name, KinematicType Kin, float Length, float Width, float Height,
  float TurnRadius, float MaxSpeed);