using Godot;

namespace SimCore.Core
{
    /// <summary>
    /// Global simulation configuration that can be modified at runtime via UI
    /// Syncs with underlying systems (SimpleDigLogic, VehicleAgent3D, etc.)
    /// </summary>
    public static class SimulationConfig
    {
        private static float _maxDigDepth = 0.08f;
        private static float _maxRobotSpeed = 1.0f;
        private static float _robotLoadCapacity = 0.5f;

        /// <summary>
        /// Max dig depth per operation (meters)
        /// Syncs with SimpleDigLogic.DIG_AMOUNT
        /// </summary>
        public static float MaxDigDepth
        {
            get => _maxDigDepth;
            set
            {
                _maxDigDepth = value;
                SimpleDigLogic.DIG_AMOUNT = value;
            }
        }

        /// <summary>
        /// Max robot speed multiplier (1x = default)
        /// Syncs with VehicleAgent3D.GlobalSpeedMultiplier
        /// </summary>
        public static float MaxRobotSpeed
        {
            get => _maxRobotSpeed;
            set
            {
                _maxRobotSpeed = value;
                VehicleAgent3D.GlobalSpeedMultiplier = value;
            }
        }

        /// <summary>
        /// Robot load capacity (cubic meters)
        /// Used by SimplifiedDigBrain.MaxPayload
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
