namespace SekaiToolsBase.DataList;

public class Character2d : ICloneable
{
    public int Id { get; set; }
    public string CharacterType { get; set; } = "";
    public bool IsNextGrade { get; set; }
    public int CharacterId { get; set; }
    public string Unit { get; set; } = "";
    public bool IsEnabledFlipDisplay { get; set; }
    public string AssetName { get; set; } = "";

    public object Clone()
    {
        return new Character2d
        {
            Id = Id,
            CharacterType = CharacterType,
            IsNextGrade = IsNextGrade,
            CharacterId = CharacterId,
            Unit = Unit,
            IsEnabledFlipDisplay = IsEnabledFlipDisplay,
            AssetName = AssetName
        };
    }
}