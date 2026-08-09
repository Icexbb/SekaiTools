namespace SekaiToolsBase.SubStationAlpha.Tag.Modded;

public abstract class LeadingBase(int leading = 0) : Tag, INestableTag
{
    public abstract override string Name { get; }
    public int Leading { get; } = leading;

    public override string ToString()
    {
        return $"\\{Name}{Leading}";
    }
}

public class LeadingVertical(int leading = 0) : LeadingBase(leading)
{
    public override string Name => "fsvp";
}

public class LeadingHorizontal(int leading = 0) : LeadingBase(leading)
{
    public override string Name => "fshp";
}