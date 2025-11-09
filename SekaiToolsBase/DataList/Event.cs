namespace SekaiToolsBase.DataList;

public class EventRankingReward : ICloneable
{
    public int Id { get; set; }
    public int EventRankingRewardRangeId { get; set; }
    public int ResourceBoxId { get; set; }

    public object Clone()
    {
        return new EventRankingReward
        {
            Id = Id,
            EventRankingRewardRangeId = EventRankingRewardRangeId,
            ResourceBoxId = ResourceBoxId
        };
    }
}

public class EventRankingRewardRange : ICloneable
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int FromRank { get; set; }
    public int ToRank { get; set; }
    public bool IsToRankBorder { get; set; }
    public EventRankingReward[] EventRankingRewards { get; set; } = [];


    public object Clone()
    {
        return new EventRankingRewardRange
        {
            Id = Id,
            EventId = EventId,
            FromRank = FromRank,
            ToRank = ToRank,
            IsToRankBorder = IsToRankBorder,
            EventRankingRewards = EventRankingRewards.Select(x => (EventRankingReward)x.Clone()).ToArray()
        };
    }
}

public class Event : ICloneable
{
    public int Id { get; set; }
    public string EventType { get; set; } = "";
    public string Name { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string BgmAssetbundleName { get; set; } = "";
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
    public EventRankingRewardRange[] EventRankingRewardRanges { get; set; } = [];

    public object Clone()
    {
        return new Event
        {
            Id = Id,
            EventType = EventType,
            Name = Name,
            AssetbundleName = AssetbundleName,
            BgmAssetbundleName = BgmAssetbundleName,
            EventOnlyComponentDisplayStartAt = EventOnlyComponentDisplayStartAt,
            StartAt = StartAt,
            AggregateAt = AggregateAt,
            RankingAnnounceAt = RankingAnnounceAt,
            DistributionStartAt = DistributionStartAt,
            EventOnlyComponentDisplayEndAt = EventOnlyComponentDisplayEndAt,
            ClosedAt = ClosedAt,
            DistributionEndAt = DistributionEndAt,
            VirtualLiveId = VirtualLiveId,
            Unit = Unit,
            EventRankingRewardRanges =
                EventRankingRewardRanges.Select(x => (EventRankingRewardRange)x.Clone()).ToArray()
        };
    }
}