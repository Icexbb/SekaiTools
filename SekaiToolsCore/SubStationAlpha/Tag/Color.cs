namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class ColorBase(SubStationAlpha.Color color) : Tag, INestableTag
{
    public abstract override string Name { get; }

    public SubStationAlpha.Color Color { get; } = color;

    protected ColorBase(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }

    public override string ToString() => $"\\{Name}&H{Color.B:X2}{Color.G:X2}{Color.R:X2}&";
}

public class Color(SubStationAlpha.Color color) : ColorBase(color)
{
    public override string Name => "c";

    public Color(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }
}

public class PrimaryColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public override string Name => "1c";

    public PrimaryColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }
}

public class SecondaryColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public override string Name => "2c";

    public SecondaryColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }
}

public class OutlineColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public override string Name => "3c";

    public OutlineColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }
}

public class ShadowColor(SubStationAlpha.Color color) : ColorBase(color)
{
    public override string Name => "4c";

    public ShadowColor(int r, int g, int b) : this(new SubStationAlpha.Color(r, g, b))
    {
    }
}