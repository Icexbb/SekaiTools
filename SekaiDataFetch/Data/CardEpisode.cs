using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class Cost
{
    public int ResourceId { get; set; }
    public string ResourceType { get; set; }
    public int Quantity { get; set; }

    public static Cost FromJson(JObject json)
    {
        return new Cost
        {
            ResourceId = json["resourceId"]!.ToObject<int>(),
            ResourceType = json["resourceType"]!.ToObject<string>()!,
            Quantity = json["quantity"]!.ToObject<int>()
        };
    }
}

public class CardEpisode
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int CardId { get; set; }
    public string Title { get; set; }
    public string ScenarioId { get; set; }
    public string AssetBundleName { get; set; }
    public int ReleaseConditionId { get; set; }
    public int Power1BonusFixed { get; set; }
    public int Power2BonusFixed { get; set; }
    public int Power3BonusFixed { get; set; }
    public List<int> RewardResourceBoxIds { get; set; }
    public List<Cost> Costs { get; set; }
    public string CardEpisodePartType { get; set; }

    public static CardEpisode FromJson(JObject json)
    {
        return new CardEpisode
        {
            Id = json["id"]!.ToObject<int>(),
            Seq = json["seq"]!.ToObject<int>(),
            CardId = json["cardId"]!.ToObject<int>(),
            Title = json["title"]!.ToObject<string>()!,
            ScenarioId = json["scenarioId"]!.ToObject<string>()!,
            AssetBundleName = json["assetbundleName"]!.ToObject<string>()!,
            ReleaseConditionId = json["releaseConditionId"]!.ToObject<int>(),
            Power1BonusFixed = json["power1BonusFixed"]!.ToObject<int>(),
            Power2BonusFixed = json["power2BonusFixed"]!.ToObject<int>(),
            Power3BonusFixed = json["power3BonusFixed"]!.ToObject<int>(),
            RewardResourceBoxIds = json["rewardResourceBoxIds"]!.ToObject<int[]>()?.ToList() ?? new List<int>(),
            Costs = json["costs"]!.ToObject<JObject[]>()!.Select(Cost.FromJson).ToList(),
            CardEpisodePartType = json["cardEpisodePartType"]!.ToObject<string>()!
        };
    }
}