using Godot;

namespace DigSim3D.Domain
{
    /// <summary>
    /// Parameters for the DigService.
    /// </summary>
    public sealed class DigConfig
    {
        /// <summary>Radius of excavation circle (m)</summary>
        public float DigRadius = 1.0f;

        /// <summary>Desired depth per full dig at a site (m). Used for UI/estimates.</summary>
        public float DigDepth = 1.0f;

        /// <summary>Vertical cut rate (m of depth per second within radius).</summary>
        public float DepthRatePerSecond = 0.15f;

        /// <summary>Swell factor (in-situ → loose volume)</summary>
        public float SwellFactor = 1.25f;

        public float AtSiteThreshold = 0.5f;
        public float AtDumpThreshold = 0.5f;
        public float MinHeightChange = 0.01f;

        public static DigConfig Default => new DigConfig();
    }
}
