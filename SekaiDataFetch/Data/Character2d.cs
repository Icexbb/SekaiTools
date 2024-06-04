using System.Globalization;
using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class Character2d
{
    public int Id { get; set; }
    public string CharacterType { get; set; }="";
    public bool IsNextGrade { get; set; }
    public int CharacterId { get; set; }
    public string Unit { get; set; }="";
    public bool IsEnabledFlipDisplay { get; set; }
    public string AssetName { get; set; }="";

    public static Character2d FromJson(JObject json)
    {
        return new Character2d
        {
            Id = json.Get("id", 0),
            CharacterType = json.Get("characterType", ""),
            IsNextGrade = json.Get("isNextGrade", false),
            CharacterId = json.Get("characterId", 0),
            Unit = json.Get("unit", ""),
            IsEnabledFlipDisplay = json.Get("isEnabledFlipDisplay", false),
            AssetName = json.Get("assetName", ""),
        };
    }
}