namespace SimCore.Core
{
    public struct DigScoring
    {
        public float InnerR;      // where to start searching (m)
        public float OuterR;      // where to stop  (m)
        public int   RadialSteps; // radial samples per sector
        public int   ArcSteps;    // angular samples per sector

        // weights for score = +wH*height - wS*slope - wD*dist - wA*heading
        public float WHeight;     // prefer higher ground
        public float WSlope;      // penalize steep
        public float WDist;       // mild distance penalty
        public float WHeading;    // penalize facing misalignment

        public static DigScoring Default => new()
        {
            InnerR = 3.0f, OuterR = 14.0f,
            RadialSteps = 12, ArcSteps = 12,
            WHeight = 1.0f, WSlope = 0.6f, WDist = 0.1f, WHeading = 0.4f
        };
    }
}
