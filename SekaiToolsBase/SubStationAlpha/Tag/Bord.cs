namespace SekaiToolsBase.SubStationAlpha.Tag;

public abstract class BordBase : Tag, INestableTag
{
    protected BordBase(float value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public abstract override string Name { get; }
    public float Value { get; set; }

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}

public class Bord(float value) : BordBase(value)
{
    public override string Name => "bord";
}

public class XBord(float value) : BordBase(value)
{
    public override string Name => "xbord";
}

public class YBord(float value) : BordBase(value)
{
    public override string Name => "ybord";
}