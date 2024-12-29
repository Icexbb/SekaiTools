namespace SekaiDataFetch.Data;

public class CharacterProfile : ICloneable
{
    public int CharacterId { get; set; }
    public string CharacterVoice { get; set; } = "";
    public string Birthday { get; set; } = "";
    public string Height { get; set; } = "";
    public string School { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public string Hobby { get; set; } = "";
    public string SpecialSkill { get; set; } = "";
    public string FavoriteFood { get; set; } = "";
    public string HatedFood { get; set; } = "";
    public string Weak { get; set; } = "";
    public string Introduction { get; set; } = "";
    public string ScenarioId { get; set; } = "";

    public object Clone()
    {
        return new CharacterProfile
        {
            CharacterId = CharacterId,
            CharacterVoice = CharacterVoice,
            Birthday = Birthday,
            Height = Height,
            School = School,
            SchoolYear = SchoolYear,
            Hobby = Hobby,
            SpecialSkill = SpecialSkill,
            FavoriteFood = FavoriteFood,
            HatedFood = HatedFood,
            Weak = Weak,
            Introduction = Introduction,
            ScenarioId = ScenarioId
        };
    }
}