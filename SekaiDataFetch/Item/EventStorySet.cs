using SekaiDataFetch.Data;

namespace SekaiDataFetch.Item;

public class EventStorySet(EventStory es, GameEvent ge) : ICloneable
{
    public readonly EventStory EventStory = es;
    public readonly GameEvent GameEvent = ge;

    public object Clone()
    {
        return new EventStorySet((EventStory)EventStory.Clone(), (GameEvent)GameEvent.Clone());
    }
}