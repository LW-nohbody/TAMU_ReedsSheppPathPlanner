# Vehicle Services

The main service providers for the vehicles. Will calculate paths, decide tasks, and create dig targets.

+ [DigService](DigService.cs) - Updates the terrain in simulation with results of digging
+ [RobotCoordinator](RobotCoordinator.cs) - Coordinates the vehicles to cover the full terrain, assigns their radial regions
+ [Adapters](Adapters/README.md) - Alters coordinate system and finds shortest path for each drive type
+ [Interfaces](Interfaces/README.md) - Defines the base interfaces used in Planning and Scheduling
+ [Math](Math/README.md) - Calculates and discretizes the vehicle paths: Reeds-Shepp, Dubins, Differential Drive, etc.
+ [Planning](Planning/README.md) - Plans the vehicle paths while avoiding obstacles
+ [Scheduling](Scheduling/README.md) - Assigns vehicle with next tasks