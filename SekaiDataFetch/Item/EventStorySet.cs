using SekaiToolsBase.Data;

namespace SekaiDataFetch.Item;

public class EventStorySet(EventStory es, GameEvent ge, int index) : ICloneable
{
    public readonly EventStory EventStory = es;
    public readonly GameEvent GameEvent = ge;
    public readonly int Index = index;

    public object Clone()
    {
        return new EventStorySet((EventStory)EventStory.Clone(), (GameEvent)GameEvent.Clone(), Index);
    }
}