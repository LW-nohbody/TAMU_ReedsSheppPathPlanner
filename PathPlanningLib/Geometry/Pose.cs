namespace PathPlanningLib.Geometry
{
    public struct Pose
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Theta { get; set; }

        public Pose(double x, double y, double theta)
        {
            X = x;
            Y = y;
            Theta = theta;
        }
    }
}