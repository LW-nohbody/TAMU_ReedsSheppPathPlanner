using PathPlanningLib.Algorithms.Geometry.PathElements;
using PathPlanningLib.Algorithms.Geometry.Paths;
using DigSim3D.App.Vehicles;
using DigSim3D.Domain;

namespace DigSim3D.Services;

public interface IHybridPlanner
{
    IPath Plan(Pose start, Pose goal, VehicleSpec spec, WorldState world);
}

