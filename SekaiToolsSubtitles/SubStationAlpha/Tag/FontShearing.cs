namespace SekaiToolsBase.SubStationAlpha.Tag;

public abstract class FontShearing(float factor) : Tag, INestableTag
{
    public float Factor { get; } = factor;

    public abstract override string Name { get; }

    public override string ToString()
    {
        return $"\\{Name}{Factor}";
    }
}

public class FontShearingX(float factor) : FontShearing(factor)
{
    public override string Name => "fax";
}

public class FontShearingY(float factor) : FontShearing(factor)
{
    public override string Name => "fay";
}