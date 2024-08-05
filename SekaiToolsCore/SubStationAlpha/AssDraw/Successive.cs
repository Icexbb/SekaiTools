namespace SekaiToolsCore.SubStationAlpha.AssDraw;

public class Successive(params AssDrawPoint[] parts) : AssDrawPart("s")
{
    private readonly List<AssDrawPoint> _parts = parts.ToList();

    public override void Move(int x, int y)
    {
        foreach (var part in _parts) part.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        foreach (var part in _parts) part.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        foreach (var part in _parts) part.Scale(ratio);
    }

    public override string ToString()
    {
        return $"{Type} {string.Join(" ", _parts.Select(part => part.ToString()))}";
    }
}