namespace SekaiToolsBase.SubStationAlpha.AssDraw;

public class AssDrawPoint(int x, int y)
{
    public int X = x, Y = y;

    public void Move(int x, int y)
    {
        X += x;
        Y += y;
    }

    public void Scale(float ratio)
    {
        X = (int)(X * ratio);
        Y = (int)(Y * ratio);
    }

    public void Scale(double ratio)
    {
        X = (int)(X * ratio);
        Y = (int)(Y * ratio);
    }

    public override string ToString()
    {
        return $"{X} {Y}";
    }
}