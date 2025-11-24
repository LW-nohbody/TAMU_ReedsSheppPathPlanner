# Application Behavior

Contains files defining the overall behavior of the simulation application. Includes the simulation director, and vehicle brains which guide the simulation operation and each vehicle's operation respectively.

+ [DynamicObstacleManager](DynamicObstacleManager.cs) - Tracks dynamic obstacles (vehicles) for avoidance, each obstacle will report its world position
+ [ObstacleManager](ObstacleManager.cs) - Manages all static obstacles in simulation
+ [PathVisualizer](PathVisualizer.cs) - Visualizes all vehicles' paths
+ [PlannedPathVisualizer](PlannedPathVisualizer.cs) - Visualizes all vehicles' planned paths and color-codes them based on gear
+ [RobotStatusPanel](RobotStatusPanel.cs) - Displays robot's status in real time
+ [SectorVisualizer](SectorVisualizer.cs) - Creates radial lines from center to visualize each robot's sector
+ [SimulationDirector](SimulationDirector.cs) - Main control for the simulation, handles or initializes simulation operations
+ [TerrainDisk](TerrainDisk.cs) - Creates the terrain for the simulation
+ [VehicleBrain](VehicleBrain.cs) - Controls each vehicle
+ [VehicleVisualizer](VehicleVisualizer.cs) - Visualizes vehicle and ensure it tracks with terrain