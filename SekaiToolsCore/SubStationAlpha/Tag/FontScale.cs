namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class FontScale(int value) : Tag, INestableTag
{
    public int Value = value;
    public abstract override string Name { get; }

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}

public class FontScaleX(int value) : FontScale(value)
{
    public override string Name => "fscx";
}

public class FontScaleY(int value) : FontScale(value)
{
    public override string Name => "fscy";
}