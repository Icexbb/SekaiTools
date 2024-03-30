namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Blur : Tag, INestableTag
{
    public override string Name => "blur";
    public int Value;

    public Blur(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public override string ToString() => $"\\{Name}{Value}";
}