namespace SekaiDataFetch.Data;

public class UnitProfile : ICloneable
{
    public string Unit { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string UnitProfileName { get; set; } = "";
    public int Seq { get; set; }
    public string ProfileSentence { get; set; } = "";
    public string ColorCode { get; set; } = "";

    public object Clone()
    {
        return new UnitProfile
        {
            Unit = Unit,
            UnitName = UnitName,
            UnitProfileName = UnitProfileName,
            Seq = Seq,
            ProfileSentence = ProfileSentence,
            ColorCode = ColorCode
        };
    }
}