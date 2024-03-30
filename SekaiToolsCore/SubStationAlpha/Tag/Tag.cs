namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class Tag
{
    public abstract string Name { get; }
    public abstract override string ToString();

    public static Tags operator +(Tag tag1, Tag tag2) => new(tag1, tag2);
}