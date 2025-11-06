using Godot;

namespace DigSim3D.Config
{
    /// <summary>
    /// Global simulation configuration that can be modified at runtime via UI
    /// Syncs with underlying systems (terrain, vehicles, etc.)
    /// </summary>
    public static class SimulationConfig
    {
        private static float _maxDigDepth = 0.08f;
        private static float _maxRobotSpeed = 1.0f;
        private static float _robotLoadCapacity = 0.5f;

        /// <summary>
        /// Max dig depth per operation (meters)
        /// Controls how much terrain is removed in each dig operation
        /// </summary>
        public static float MaxDigDepth
        {
            get => _maxDigDepth;
            set => _maxDigDepth = value;
        }

        /// <summary>
        /// Max robot speed multiplier (1x = default)
        /// Scales robot movement speed in the simulation
        /// </summary>
        public static float MaxRobotSpeed
        {
            get => _maxRobotSpeed;
            set => _maxRobotSpeed = value;
        }

        /// <summary>
        /// Robot load capacity (cubic meters)
        /// Maximum dirt volume a robot can carry before dumping
        /// </summary>
        public static float RobotLoadCapacity
        {
            get => _robotLoadCapacity;
            set => _robotLoadCapacity = Mathf.Max(0.1f, value); // Minimum 0.1m³
        }

        /// <summary>
        /// Reset all settings to defaults
        /// </summary>
        public static void ResetToDefaults()
        {
            MaxDigDepth = 0.08f;
            MaxRobotSpeed = 1.0f;
            RobotLoadCapacity = 0.5f;
        }
    }
}
