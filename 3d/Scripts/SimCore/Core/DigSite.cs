using Godot;
namespace SimCore.Core;
public sealed record DigSite(Vector3 Center, float RemainingVolume, float ToolRadius, float Depth);
