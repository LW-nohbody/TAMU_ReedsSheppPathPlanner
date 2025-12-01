namespace DigSim3D.Domain;

public enum KinematicType { Bicycle, DiffDrive, CenterArticulated, ScrewPropelled }
public readonly record struct VehicleSpec(
  KinematicType KinType,
  double? TurnRadius, double? MaxSpeed);