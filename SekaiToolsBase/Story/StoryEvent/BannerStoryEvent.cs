namespace SekaiToolsBase.Story.StoryEvent;

public class BannerStoryEvent(string bodyOriginal, int index, int totalIndex = -1) : BaseStoryEvent("Banner", index, bodyOriginal)
{
    public readonly int TotalIndex = totalIndex;

    public override object Clone()
    {
        var cloned = new BannerStoryEvent(BodyOriginal, Index, TotalIndex) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}