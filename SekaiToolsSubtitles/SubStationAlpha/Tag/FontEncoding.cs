namespace SekaiToolsBase.SubStationAlpha.Tag;

public class FontEncoding(int encoding) : Tag
{
    public override string Name => "fe";
    public int Encoding { get; } = encoding;

    public override string ToString()
    {
        return $"\\{Name}{Encoding}";
    }
}