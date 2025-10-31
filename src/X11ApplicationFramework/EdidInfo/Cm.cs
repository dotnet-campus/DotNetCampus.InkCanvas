namespace X11ApplicationFramework.EdidInfo;

public readonly record struct Cm(uint Value)
{
    public override string ToString() => $"{Value} cm";
}