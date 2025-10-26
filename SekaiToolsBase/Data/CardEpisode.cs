namespace SekaiToolsBase.Data;

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
}