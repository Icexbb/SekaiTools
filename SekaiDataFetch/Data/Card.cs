using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class Card
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int CharacterId { get; set; }
    public string CardRarityType { get; set; }="";
    public int SpecialTrainingPower1BonusFixed { get; set; }
    public int SpecialTrainingPower2BonusFixed { get; set; }
    public int SpecialTrainingPower3BonusFixed { get; set; }
    public string Attr { get; set; }="";
    public string SupportUnit { get; set; }="";
    public int SkillId { get; set; }
    public string CardSkillName { get; set; }="";
    public string Prefix { get; set; }="";
    public string AssetbundleName { get; set; }="";
    public string GachaPhrase { get; set; }="";
    public string FlavorText { get; set; }="";
    public long ReleaseAt { get; set; }
    public long ArchivePublishedAt { get; set; }

    public static Card FromJson(JObject json)
    {
        return new Card
        {
            Id = json.Get("id", 0),
            Seq = json.Get("seq", 0),
            CharacterId = json.Get("characterId", 0),
            CardRarityType = json.Get("cardRarityType", ""),
            SpecialTrainingPower1BonusFixed = json.Get("specialTrainingPower1BonusFixed", 0),
            SpecialTrainingPower2BonusFixed = json.Get("specialTrainingPower2BonusFixed", 0),
            SpecialTrainingPower3BonusFixed = json.Get("specialTrainingPower3BonusFixed", 0),
            Attr = json.Get("attr", ""),
            SupportUnit = json.Get("supportUnit", ""),
            SkillId = json.Get("skillId", 0),
            CardSkillName = json.Get("cardSkillName", ""),
            Prefix = json.Get("prefix", ""),
            AssetbundleName = json.Get("assetbundleName", ""),
            GachaPhrase = json.Get("gachaPhrase", ""),
            FlavorText = json.Get("flavorText", ""),
            ReleaseAt = json.Get("releaseAt", 0L),
            ArchivePublishedAt = json.Get("archivePublishedAt", 0L),
        };
    }
}