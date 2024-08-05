namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class ShadBase : Tag, INestableTag
{
    protected ShadBase(float value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public abstract override string Name { get; }
    public float Value { get; }

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}

public class Shad(float value) : ShadBase(value)
{
    public override string Name => "shad";
}

public class XShad(float value) : ShadBase(value)
{
    public override string Name => "xshad";
}

public class YShad(float value) : ShadBase(value)
{
    public override string Name => "yshad";
}