using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Fetch.Data;

public class Card
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int CharacterId { get; set; }
    public string CardRarityType { get; set; }
    public int SpecialTrainingPower1BonusFixed { get; set; }
    public int SpecialTrainingPower2BonusFixed { get; set; }
    public int SpecialTrainingPower3BonusFixed { get; set; }
    public string Attr { get; set; }
    public string SupportUnit { get; set; }
    public int SkillId { get; set; }
    public string CardSkillName { get; set; }
    public string Prefix { get; set; }
    public string AssetbundleName { get; set; }
    public string GachaPhrase { get; set; }
    public string FlavorText { get; set; }
    public int ReleaseAt { get; set; }
    public int ArchivePublishedAt { get; set; }

    public static Card FromJson(JObject json)
    {
        return new Card
        {
            Id = json["id"]!.ToObject<int>(),
            Seq = json["seq"]!.ToObject<int>(),
            CharacterId = json["characterId"]!.ToObject<int>(),
            CardRarityType = json["cardRarityType"]!.ToObject<string>()!,
            SpecialTrainingPower1BonusFixed = json["specialTrainingPower1BonusFixed"]!.ToObject<int>(),
            SpecialTrainingPower2BonusFixed = json["specialTrainingPower2BonusFixed"]!.ToObject<int>(),
            SpecialTrainingPower3BonusFixed = json["specialTrainingPower3BonusFixed"]!.ToObject<int>(),
            Attr = json["attr"]!.ToObject<string>()!,
            SupportUnit = json["supportUnit"]!.ToObject<string>()!,
            SkillId = json["skillId"]!.ToObject<int>(),
            CardSkillName = json["cardSkillName"]!.ToObject<string>()!,
            Prefix = json["prefix"]!.ToObject<string>()!,
            AssetbundleName = json["assetbundleName"]!.ToObject<string>()!,
            GachaPhrase = json["gachaPhrase"]!.ToObject<string>()!,
            FlavorText = json["flavorText"]!.ToObject<string>()!,
            ReleaseAt = json["releaseAt"]!.ToObject<int>(),
            ArchivePublishedAt = json["archivePublishedAt"]!.ToObject<int>()
        };
    }
}