namespace PathPlanningLib.Algorithms.Geometry.PathElements;

public record DubinsElement : PathElement
{
    public double Param { get; set; }
    public Steering Steering { get; set; }

    private DubinsElement(double param, Steering steering)
    {
        Param = param;
        Steering = steering;
    }

    public static DubinsElement Create(double param, Steering steering)
        => (param >= 0)
           ? new DubinsElement(param, steering)
           : throw new ArgumentOutOfRangeException(
                nameof(param),
                param,
                "Distance must be non-negative.");

    public DubinsElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tNormalized Length: {Math.Round(Param, 3)} }}";
}