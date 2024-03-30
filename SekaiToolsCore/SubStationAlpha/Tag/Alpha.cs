namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class AlphaBase(SubtitleAlpha a) : Tag, INestableTag
{
    public SubtitleAlpha A { get; } = a;

    public AlphaBase(int a) : this(new SubtitleAlpha(a))
    {
    }

    public abstract override string Name { get; }

    public override string ToString() => $"\\{Name}{A}";
}

public class Alpha(SubtitleAlpha a) : AlphaBase(a)
{
    public Alpha(int a) : this(new SubtitleAlpha(a))
    {
    }

    public override string Name => "alpha";
}

public class PrimaryAlpha(SubtitleAlpha a) : AlphaBase(a)
{
    public PrimaryAlpha(int a) : this(new SubtitleAlpha(a))
    {
    }

    public override string Name => "1a";
}

public class SecondaryAlpha(SubtitleAlpha a) : AlphaBase(a)
{
    public SecondaryAlpha(int a) : this(new SubtitleAlpha(a))
    {
    }

    public override string Name => "2a";
}

public class OutlineAlpha(SubtitleAlpha a) : AlphaBase(a)
{
    public OutlineAlpha(int a) : this(new SubtitleAlpha(a))
    {
    }

    public override string Name => "3a";
}

public class ShadowAlpha(SubtitleAlpha a) : AlphaBase(a)
{
    public ShadowAlpha(int a) : this(new SubtitleAlpha(a))
    {
    }

    public override string Name => "4a";
}