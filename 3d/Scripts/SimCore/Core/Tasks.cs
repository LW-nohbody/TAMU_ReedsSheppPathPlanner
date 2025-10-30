using Godot;
namespace SimCore.Core;
public interface ITask { }
public sealed record DigTask(DigSite Site, float ToolRadius, float Depth) : ITask;
public sealed record DumpTask(Vector3 DumpPoint) : ITask;
public sealed record TransitTask(Pose Goal) : ITask;
public sealed record IdleTask() : ITask;