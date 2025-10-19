namespace PathPlanningLib.Algorithms.Geometry.PathElements;

public record ReedsSheppElement : PathElement
{

    public double Distance { get; set; }
    public Steering Steering { get; set; }
    public Gear Gear { get; set; }


    public static ReedsSheppElement Create(double distance, Steering steering, Gear gear)
        => (distance >= 0)
           ? new PathElement(distance, steering, gear)
           : new PathElement(-distance, steering, gear).ReverseGear();

    public ReedsSheppElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };
    public ReedsSheppElement ReverseGear() => this with { Gear = (Gear)(-(int)Gear) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tGear: {Gear}\tdistance: {Math.Round(Distance, 3)} }}";
}