using Godot;
namespace DigSim3D.Domain;

/// <summary>
/// Definitions for each robot task
/// </summary>
public interface ITask { }
public sealed record DigTask(Vector3 SiteCenter, float ToolRadius, float Depth) : ITask;
public sealed record DumpTask(Vector3 DumpPoint) : ITask;
public sealed record TransitTask(Pose Goal) : ITask;
public sealed record IdleTask() : ITask;