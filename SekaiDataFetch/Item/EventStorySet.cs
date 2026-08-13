using SekaiToolsBase.DataList;

namespace SekaiDataFetch.Item;

public class EventStorySet(EventStory es, GameEvent ge, int index, int[]? bannerGameCharacterIds = null) : ICloneable
{
    public readonly EventStory EventStory = es;
    public readonly GameEvent GameEvent = ge;
    public readonly int Index = index;
    public readonly int[] BannerGameCharacterIds = bannerGameCharacterIds ?? [];

    public object Clone()
    {
        return new EventStorySet((EventStory)EventStory.Clone(), (GameEvent)GameEvent.Clone(), Index,
            BannerGameCharacterIds.ToArray());
    }
}
