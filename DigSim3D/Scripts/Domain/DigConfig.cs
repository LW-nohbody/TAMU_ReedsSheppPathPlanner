using Godot;

namespace DigSim3D.Domain
{
    /// <summary>
    /// Dig scoring and parameters for the DigService.
    /// </summary>
    public sealed class DigConfig
    {
        /// <summary> Dig rate (m³/second) </summary>
        public float DigRatePerSecond = 2.0f;  // Balanced rate for strategic digging

        /// <summary> Radius of excavation cone (meters) </summary>
        public float DigRadius = 1.2f;  // Smaller radius for more precise digging

        /// <summary> Depth of excavation cone (meters) </summary>
        public float DigDepth = 0.3f;  // Original per-frame depth

        /// <summary> Distance threshold to consider "at dig site" (meters) </summary>
        public float AtSiteThreshold = 0.5f;

        /// <summary> Distance threshold to consider "at dump center" (meters) </summary>
        public float AtDumpThreshold = 0.5f;

        /// <summary> Minimum height change to visualize (prevents over-smoothing) </summary>
        public float MinHeightChange = 0.01f;

        public static DigConfig Default => new DigConfig();
    }
}
