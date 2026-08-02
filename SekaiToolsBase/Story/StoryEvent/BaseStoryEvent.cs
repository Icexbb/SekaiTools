namespace SekaiToolsBase.Story.StoryEvent;

public abstract class BaseStoryEvent(
    string type,
    int index,
    string origin,
    string translated = "",
    int storyIndex = -1) : ICloneable
{
    public readonly string BodyOriginal = origin;
    public readonly int Index = index;
    public readonly int StoryIndex = storyIndex;

    public readonly string Type = type;
    public string BodyTranslated = translated;

    public int EffectiveStoryIndex => StoryIndex >= 0 ? StoryIndex : Index;
    public string FinalContent => BodyTranslated.Length > 0 ? BodyTranslated : BodyOriginal;

    public abstract object Clone();
}
