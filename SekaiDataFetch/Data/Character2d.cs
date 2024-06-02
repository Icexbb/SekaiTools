using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class Character2d
{
    public int Id { get; set; }
    public string CharacterType { get; set; }
    public bool IsNextGrade { get; set; }
    public int CharacterId { get; set; }
    public string Unit { get; set; }
    public bool IsEnabledFlipDisplay { get; set; }
    public string AssetName { get; set; }

    public static Character2d FromJson(JObject json)
    {
        return new Character2d
        {
            Id = json["id"]!.ToObject<int>(),
            CharacterType = json["characterType"]!.ToObject<string>()!,
            IsNextGrade = json["isNextGrade"]!.ToObject<bool>(),
            CharacterId = json["characterId"]!.ToObject<int>(),
            Unit = json["unit"]!.ToObject<string>()!,
            IsEnabledFlipDisplay = json["isEnabledFlipDisplay"]!.ToObject<bool>(),
            AssetName = json["assetName"]!.ToObject<string>()!
        };
    }
}