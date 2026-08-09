namespace SekaiToolsBase.SubStationAlpha.Tag.Modded;

public abstract class ImageBase(string path, int x = 0, int y = 0) : Tag, INestableTag
{
    public abstract override string Name { get; }

    public string Path { get; } = path;
    public int OffsetX { get; } = x;
    public int OffsetY { get; } = y;

    public override string ToString()
    {
        if (OffsetX == 0 && OffsetY == 0)
            return $"\\{Name}({Path})";
        return $"\\{Name}({Path},{OffsetX},{OffsetY})";
    }
}

public class PrimaryImage(string path, int x = 0, int y = 0) : ImageBase(path, x, y)
{
    public override string Name => "1img";
}

public class SecondaryImage(string path, int x = 0, int y = 0) : ImageBase(path, x, y)
{
    public override string Name => "2img";
}

public class OutlineImage(string path, int x = 0, int y = 0) : ImageBase(path, x, y)
{
    public override string Name => "3img";
}

public class ShadowImage(string path, int x = 0, int y = 0) : ImageBase(path, x, y)
{
    public override string Name => "4img";
}