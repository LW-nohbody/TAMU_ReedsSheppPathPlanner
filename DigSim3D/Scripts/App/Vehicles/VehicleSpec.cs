using DigSim3D.Services;

namespace DigSim3D.App.Vehicles;


public readonly record struct VehicleSpec(
  KinematicType KinType,
  double? TurnRadius, double? MaxSpeed);