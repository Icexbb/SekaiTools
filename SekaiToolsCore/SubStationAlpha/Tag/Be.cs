namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Be : Tag,INestableTag
{
    public override string Name => "be";
    public int Value;

    public Be(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public override string ToString() => $"\\{Name}{Value}";
}