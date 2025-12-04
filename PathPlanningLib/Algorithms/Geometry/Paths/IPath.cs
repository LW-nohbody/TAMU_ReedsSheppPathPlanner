namespace PathPlanningLib.Algorithms.Geometry.Paths;

public interface IPath
{
    double Length { get; }
    void ComputeLength();
    bool IsEmpty();
}