namespace SekaiToolsBase.Data;

public class GameEvent : ICloneable
{
    public int Id { get; set; }
    public string EventType { get; set; } = "";
    public string Name { get; set; } = "";
    public string AssetBundleName { get; set; } = "";
    public string BgmAssetBundleName { get; set; } = "";
    public long EventOnlyComponentDisplayStartAt { get; set; }
    public long StartAt { get; set; }
    public long AggregateAt { get; set; }
    public long RankingAnnounceAt { get; set; }
    public long DistributionStartAt { get; set; }
    public long EventOnlyComponentDisplayEndAt { get; set; }
    public long ClosedAt { get; set; }
    public long DistributionEndAt { get; set; }
    public int VirtualLiveId { get; set; }
    public string Unit { get; set; } = "";

    public object Clone()
    {
        return new GameEvent
        {
            Id = Id,
            EventType = EventType,
            Name = Name,
            AssetBundleName = AssetBundleName,
            BgmAssetBundleName = BgmAssetBundleName,
            EventOnlyComponentDisplayStartAt = EventOnlyComponentDisplayStartAt,
            StartAt = StartAt,
            AggregateAt = AggregateAt,
            RankingAnnounceAt = RankingAnnounceAt,
            DistributionStartAt = DistributionStartAt,
            EventOnlyComponentDisplayEndAt = EventOnlyComponentDisplayEndAt,
            ClosedAt = ClosedAt,
            DistributionEndAt = DistributionEndAt,
            VirtualLiveId = VirtualLiveId,
            Unit = Unit
        };
    }
}