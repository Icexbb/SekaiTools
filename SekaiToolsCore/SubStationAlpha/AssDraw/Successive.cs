namespace SekaiToolsCore.SubStationAlpha.AssDraw;

public class Successive(params AssDrawPoint[] parts) : AssDrawPart("s")
{
    private readonly List<AssDrawPoint> parts = parts.ToList();

    public override void Move(int x, int y)
    {
        foreach (var part in parts)
        {
            part.Move(x, y);
        }
    }

    public override void Scale(double ratio)
    {
        foreach (var part in parts)
        {
            part.Scale(ratio);
        }
    }

    public override void Scale(float ratio)
    {
        foreach (var part in parts)
        {
            part.Scale(ratio);
        }
    }

    public override string ToString() => $"{Type} {string.Join(" ", parts.Select(part => part.ToString()))}";
}