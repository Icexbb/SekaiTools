using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class Cost : ICloneable
{
    public int ResourceId { get; set; }
    public string ResourceType { get; set; } = "";
    public int Quantity { get; set; }

    public object Clone()
    {
        return new Cost
        {
            ResourceId = ResourceId,
            ResourceType = ResourceType,
            Quantity = Quantity
        };
    }

    public static Cost FromJson(JObject json)
    {
        return new Cost
        {
            ResourceId = json.Get("resourceId", 0),
            ResourceType = json.Get("resourceType", ""),
            Quantity = json.Get("quantity", 0)
        };
    }
}

public class CardEpisode : ICloneable
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int CardId { get; set; }
    public string Title { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string AssetBundleName { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public int Power1BonusFixed { get; set; }
    public int Power2BonusFixed { get; set; }
    public int Power3BonusFixed { get; set; }
    public int[] RewardResourceBoxIds { get; set; } = [];
    public Cost[] Costs { get; set; } = [];
    public string CardEpisodePartType { get; set; } = "";

    public object Clone()
    {
        return new CardEpisode
        {
            Id = Id,
            Seq = Seq,
            CardId = CardId,
            Title = Title,
            ScenarioId = ScenarioId,
            AssetBundleName = AssetBundleName,
            ReleaseConditionId = ReleaseConditionId,
            Power1BonusFixed = Power1BonusFixed,
            Power2BonusFixed = Power2BonusFixed,
            Power3BonusFixed = Power3BonusFixed,
            RewardResourceBoxIds = RewardResourceBoxIds,
            Costs = Costs.Select(x => (Cost)x.Clone()).ToArray(),
            CardEpisodePartType = CardEpisodePartType
        };
    }

    public static CardEpisode FromJson(JObject json)
    {
        return new CardEpisode
        {
            Id = json.Get("id", 0),
            Seq = json.Get("seq", 0),
            CardId = json.Get("cardId", 0),
            Title = json.Get("title", ""),
            ScenarioId = json.Get("scenarioId", ""),
            AssetBundleName = json.Get("assetbundleName", ""),
            ReleaseConditionId = json.Get("releaseConditionId", 0),
            Power1BonusFixed = json.Get("power1BonusFixed", 0),
            Power2BonusFixed = json.Get("power2BonusFixed", 0),
            Power3BonusFixed = json.Get("power3BonusFixed", 0),
            RewardResourceBoxIds = json.Get("rewardResourceBoxIds", Array.Empty<int>()),
            Costs = json.Get("costs", Array.Empty<JObject>()).Select(Cost.FromJson).ToArray(),
            CardEpisodePartType = json.Get("cardEpisodePartType", "")
        };
    }
}