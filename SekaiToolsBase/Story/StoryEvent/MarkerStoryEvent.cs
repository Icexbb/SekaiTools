namespace SekaiToolsBase.Story.StoryEvent;

public class MarkerStoryEvent(string bodyOriginal, int index) : BaseStoryEvent("Marker", index, bodyOriginal)
{
    public override object Clone()
    {
        var cloned = new MarkerStoryEvent(BodyOriginal, Index) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}