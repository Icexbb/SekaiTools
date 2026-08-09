namespace SekaiToolsBase.SubStationAlpha.Tag;

public abstract class AlphaBase(SubStationAlpha.Alpha a) : Tag, INestableTag
{
    public AlphaBase(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public SubStationAlpha.Alpha A { get; } = a;

    public abstract override string Name { get; }

    public override string ToString()
    {
        return $"\\{Name}{A}";
    }
}

public class Alpha(SubStationAlpha.Alpha a) : AlphaBase(a)
{
    public Alpha(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public override string Name => "alpha";
}

public class PrimaryAlpha(SubStationAlpha.Alpha a) : AlphaBase(a)
{
    public PrimaryAlpha(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public override string Name => "1a";
}

public class SecondaryAlpha(SubStationAlpha.Alpha a) : AlphaBase(a)
{
    public SecondaryAlpha(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public override string Name => "2a";
}

public class OutlineAlpha(SubStationAlpha.Alpha a) : AlphaBase(a)
{
    public OutlineAlpha(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public override string Name => "3a";
}

public class ShadowAlpha(SubStationAlpha.Alpha a) : AlphaBase(a)
{
    public ShadowAlpha(int a) : this(new SubStationAlpha.Alpha(a))
    {
    }

    public override string Name => "4a";
}