namespace SekaiToolsBase.SubStationAlpha.AssDraw;

public abstract class AssDrawPart(string type)
{
    protected readonly string Type = type;

    public abstract void Move(int x, int y);
    public abstract void Scale(double ratio);
    public abstract void Scale(float ratio);
    public abstract override string ToString();
}