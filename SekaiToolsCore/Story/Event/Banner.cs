namespace SekaiToolsCore.Story.Event;

public class Banner(string bodyOriginal, int index, int totalIndex = -1) : Event("Banner", index, bodyOriginal)
{
    public int TotalIndex = totalIndex;

    public override object Clone()
    {
        var cloned = new Banner(BodyOriginal, Index, TotalIndex) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}