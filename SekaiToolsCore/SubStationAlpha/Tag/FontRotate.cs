namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class FontRotateBase(int value) : Tag, INestableTag
{
    public int Value = value;
    public abstract override string Name { get; }

    public override string ToString() => $"\\{Name}{Value}";
}

public class FontRotate(int value) : FontRotateBase(value)
{
    public override string Name => "fr";
}

public class FontRotateX(int value) : FontRotateBase(value)
{
    public override string Name => "frx";
}

public class FontRotateY(int value) : FontRotateBase(value)
{
    public override string Name => "fry";
}

public class FontRotateZ(int value) : FontRotateBase(value)
{
    public override string Name => "frz";
}