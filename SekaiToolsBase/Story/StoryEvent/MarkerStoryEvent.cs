namespace SekaiToolsBase.Story.StoryEvent;

public class MarkerStoryEvent(string bodyOriginal, int index, int storyIndex = -1)
    : BaseStoryEvent("Marker", index, bodyOriginal, storyIndex: storyIndex)
{
    public override object Clone()
    {
        var cloned = new MarkerStoryEvent(BodyOriginal, Index, StoryIndex) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}
