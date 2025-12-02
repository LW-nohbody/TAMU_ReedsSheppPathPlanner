using DigSim3D.Services;

namespace DigSim3D.Domain;


public readonly record struct VehicleSpec(
  KinematicType KinType,
  double? TurnRadius, double? MaxSpeed);