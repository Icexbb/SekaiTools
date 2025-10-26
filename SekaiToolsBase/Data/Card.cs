namespace SekaiToolsBase.Data;

public class Card : ICloneable
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int CharacterId { get; set; }
    public string CardRarityType { get; set; } = "";
    public int SpecialTrainingPower1BonusFixed { get; set; }
    public int SpecialTrainingPower2BonusFixed { get; set; }
    public int SpecialTrainingPower3BonusFixed { get; set; }
    public string Attr { get; set; } = "";
    public string SupportUnit { get; set; } = "";
    public int SkillId { get; set; }
    public string CardSkillName { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string GachaPhrase { get; set; } = "";
    public string FlavorText { get; set; } = "";
    public long ReleaseAt { get; set; }
    public long ArchivePublishedAt { get; set; }


    public object Clone()
    {
        return new Card
        {
            Id = Id,
            Seq = Seq,
            CharacterId = CharacterId,
            CardRarityType = CardRarityType,
            SpecialTrainingPower1BonusFixed = SpecialTrainingPower1BonusFixed,
            SpecialTrainingPower2BonusFixed = SpecialTrainingPower2BonusFixed,
            SpecialTrainingPower3BonusFixed = SpecialTrainingPower3BonusFixed,
            Attr = Attr,
            SupportUnit = SupportUnit,
            SkillId = SkillId,
            CardSkillName = CardSkillName,
            Prefix = Prefix,
            AssetbundleName = AssetbundleName,
            GachaPhrase = GachaPhrase,
            FlavorText = FlavorText,
            ReleaseAt = ReleaseAt,
            ArchivePublishedAt = ArchivePublishedAt
        };
    }
}