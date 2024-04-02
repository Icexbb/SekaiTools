using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Fetch.Data;

public class GameEvent
{
    public int Id { get; set; }
    public string EventType { get; set; }
    public string Name { get; set; }
    public string AssetBundleName { get; set; }
    public string BgmAssetBundleName { get; set; }
    public int EventOnlyComponentDisplayStartAt { get; set; }
    public int StartAt { get; set; }
    public int AggregateAt { get; set; }
    public int RankingAnnounceAt { get; set; }
    public int DistributionStartAt { get; set; }
    public int EventOnlyComponentDisplayEndAt { get; set; }
    public int ClosedAt { get; set; }
    public int DistributionEndAt { get; set; }
    public int VirtualLiveId { get; set; }
    public string Unit { get; set; }

    public static GameEvent FromJson(JObject json)
    {
        return new GameEvent
        {
            Id = json["id"]!.ToObject<int>(),
            EventType = json["eventType"]!.ToObject<string>()!,
            Name = json["name"]!.ToObject<string>()!,
            AssetBundleName = json["assetBundleName"]!.ToObject<string>()!,
            BgmAssetBundleName = json["bgmAssetBundleName"]!.ToObject<string>()!,
            EventOnlyComponentDisplayStartAt = json["eventOnlyComponentDisplayStartAt"]!.ToObject<int>(),
            StartAt = json["startAt"]!.ToObject<int>(),
            AggregateAt = json["aggregateAt"]!.ToObject<int>(),
            RankingAnnounceAt = json["rankingAnnounceAt"]!.ToObject<int>(),
            DistributionStartAt = json["distributionStartAt"]!.ToObject<int>(),
            EventOnlyComponentDisplayEndAt = json["eventOnlyComponentDisplayEndAt"]!.ToObject<int>(),
            ClosedAt = json["closedAt"]!.ToObject<int>(),
            DistributionEndAt = json["distributionEndAt"]!.ToObject<int>(),
            VirtualLiveId = json["virtualLiveId"]!.ToObject<int>(),
            Unit = json["unit"]!.ToObject<string>()!
        };
    }
}