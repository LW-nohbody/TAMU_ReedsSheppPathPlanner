namespace PathPlanningLib.Algorithms.Geometry.PathElements;

public record DubinsElement : PathElement
{
    public double Distance { get; set; }
    public Steering Steering { get; set; }


    public static DubinsElement Create(double distance, Steering steering)
        => (distance >= 0)
           ? new DubinsElement(distance, steering)
           : throw new ArgumentOutOfRangeException(
                nameof(distance),
                distance,
                "Distance must be non-negative.");

    public DubinsElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tdistance: {Math.Round(Distance, 3)} }}";
}