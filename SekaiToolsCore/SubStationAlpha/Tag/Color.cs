namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class ColorBase(SubStationAlpha.Color color) : Tag, INestableTag
{
    protected ColorBase(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public abstract override string Name { get; }

    public SubStationAlpha.Color Color { get; } = color;

    public override string ToString()
    {
        return $"\\{Name}&H{Color.B:X2}{Color.G:X2}{Color.R:X2}&";
    }
}

public class Color(SubStationAlpha.Color color) : ColorBase(color)
{
    public Color(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string Name => "c";
}

public class PrimaryColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public PrimaryColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string Name => "1c";
}

public class SecondaryColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public SecondaryColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string Name => "2c";
}

public class OutlineColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public OutlineColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string Name => "3c";
}

public class ShadowColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public ShadowColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string Name => "4c";
}