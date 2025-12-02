namespace DigSim3D.Domain;

/// <summary>
/// Defines the pose struct for robots
/// </summary>
/// <param name="X"></param>
/// <param name="Z"></param>
/// <param name="Yaw"></param>
public readonly record struct Pose(double X, double Z, double Yaw);