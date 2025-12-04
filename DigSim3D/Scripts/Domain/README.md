# Simulation Defintions

Contains the definitions for various aspects of the simulation - including obstacles, vehicles, and the world

+ [CylinderObstacle](CylinderObstacle.cs) - Cylindrical Obstacle class, contains intersection checking
+ [DigConfig](DigConfig.cs) - Contains parameters for digging ie) dig radius, dig depth, etc
+ [DigScoring](DigScoring.cs) - Assigns weights to dig sites - (Currently, favors higher points)
+ [DigState](DigState.cs) - Tracks dig status of robot
+ [Nameplate3D](Nameplate3D.cs) - Handles the name plates for each robot within the simulation
+ [Obstacle3D](Obstacle3D.cs) - Abstract class defining obstacles
+ [PlannedPath](PlannedPath.cs) - Defines planned paths
+ [Pose](Pose.cs) - Pose struct for robot positions
+ [Tasks](Tasks.cs) - Contains definitions for each robot task
+ [VehicleSpec](VehicleSpec.cs) - Defines vehicle specs, including kinematic models
+ [WorldState](WorldState.cs) - Tank definition, inlcudes dump site, obstacles, and terrain