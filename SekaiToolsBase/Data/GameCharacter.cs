namespace SekaiToolsBase.Data;

public class GameCharacter : ICloneable
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public int ResourceId { get; set; }
    public string FirstName { get; set; } = "";
    public string GivenName { get; set; } = "";

    public string FirstNameRuby { get; set; } = "";
    public string GivenNameRuby { get; set; } = "";
    public string Gender { get; set; } = "";
    public double Height { get; set; }
    public double Live2dHeightAdjustment { get; set; }
    public string Figure { get; set; } = "";
    public string BreastSize { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string SupportUnitType { get; set; } = "";

    public object Clone()
    {
        return new GameCharacter
        {
            Id = Id,
            Seq = Seq,
            ResourceId = ResourceId,
            FirstName = FirstName,
            GivenName = GivenName,
            FirstNameRuby = FirstNameRuby,
            GivenNameRuby = GivenName,
            Gender = Gender,
            Height = Height,
            Live2dHeightAdjustment = Live2dHeightAdjustment,
            Figure = Figure,
            BreastSize = BreastSize,
            ModelName = ModelName,
            Unit = Unit,
            SupportUnitType = SupportUnitType
        };
    }
}