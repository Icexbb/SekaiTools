namespace SekaiToolsCore.Story.Event;

public class Banner(string bodyOriginal, int index) : Event("Banner", bodyOriginal)
{
    public readonly int Index = index;

    public override object Clone()
    {
        var cloned = new Banner(BodyOriginal, Index) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}