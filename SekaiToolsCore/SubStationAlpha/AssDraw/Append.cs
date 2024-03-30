namespace SekaiToolsCore.SubStationAlpha.AssDraw;

public class Append(AssDrawPoint p) : AssDrawPart("p")
{
    public override void Move(int x, int y) => p.Move(x, y);
    public override void Scale(double ratio) => p.Scale(ratio);
    public override void Scale(float ratio) => p.Scale(ratio);

    public override string ToString() => $"{Type} {p.X} {p.Y}";

    public Append(int x, int y) : this(new AssDrawPoint(x, y))
    {
    }
}