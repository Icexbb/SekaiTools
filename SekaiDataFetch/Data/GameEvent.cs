using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class GameEvent
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

    public static GameEvent FromJson(JObject json)
    {
        return new GameEvent
        {
            Id = json.Get("id", 0),
            EventType = json.Get("eventType", ""),
            Name = json.Get("name", ""),
            AssetBundleName = json.Get("assetBundleName", ""),
            BgmAssetBundleName = json.Get("bgmAssetBundleName", ""),
            EventOnlyComponentDisplayStartAt = json.Get("eventOnlyComponentDisplayStartAt", 0L),
            StartAt = json.Get("startAt", 0L),
            AggregateAt = json.Get("aggregateAt", 0L),
            RankingAnnounceAt = json.Get("rankingAnnounceAt", 0L),
            DistributionStartAt = json.Get("distributionStartAt", 0L),
            EventOnlyComponentDisplayEndAt = json.Get("eventOnlyComponentDisplayEndAt", 0L),
            ClosedAt = json.Get("closedAt", 0L),
            DistributionEndAt = json.Get("distributionEndAt", 0L),
            VirtualLiveId = json.Get("virtualLiveId", 0),
            Unit = json.Get("unit", "")
        };
    }
}