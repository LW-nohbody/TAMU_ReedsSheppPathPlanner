namespace PathPlanningLib.Algorithms.Geometry.PathElements;

public record ReedsSheppElement : PathElement
{
    public double Param { get; set; }
    public Steering Steering { get; set; }
    public Gear Gear { get; set; }

    private ReedsSheppElement(double param, Steering steering, Gear gear)
    {
        Param = param;
        Steering = steering;
        Gear = gear;
    }

    public static ReedsSheppElement Create(double param, Steering steering, Gear gear)
        => (param >= 0)
           ? new ReedsSheppElement(param, steering, gear)
           : new ReedsSheppElement(-param, steering, gear).ReverseGear();

    public ReedsSheppElement ReverseSteering() => this with { Steering = (Steering)(-(int)Steering) };
    public ReedsSheppElement ReverseGear() => this with { Gear = (Gear)(-(int)Gear) };

    public override string ToString()
        => $"{{ Steering: {Steering}\tGear: {Gear}\tNormalized Length: {Math.Round(Param, 3)} }}";
}