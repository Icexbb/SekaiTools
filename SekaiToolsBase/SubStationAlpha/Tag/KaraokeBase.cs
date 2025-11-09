namespace SekaiToolsBase.SubStationAlpha.Tag;

public abstract class KaraokeBase : Tag
{
    public KaraokeBase(int duration)
    {
        if (duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), "The duration must be greater than or equal to 0.");

        Duration = duration;
    }

    public abstract override string Name { get; }
    public int Duration { get; }

    public override string ToString()
    {
        return $"\\{Name}{Duration}";
    }
}

public class Karaoke(int duration) : KaraokeBase(duration)
{
    public override string Name => "k";
}

public class KaraokeFade(int duration) : KaraokeBase(duration)
{
    public override string Name => "kf";
}

public class KaraokeOutline(int duration) : KaraokeBase(duration)
{
    public override string Name => "ko";
}