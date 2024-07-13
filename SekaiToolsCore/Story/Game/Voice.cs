using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Game;

public readonly struct Voice(int character2DId, string voiceId)
{
    public readonly int Character2DId = character2DId;

    public int GetCharacterId()
    {
        var charaIdFromCharaL2dId =
            Constants.C2dIdToCid.GetValueOrDefault(Character2DId, 0);
        if (charaIdFromCharaL2dId is >= 1 and <= 26) return charaIdFromCharaL2dId;
        var idSplit = voiceId.Split('_');
        List<int> idList = [];
        foreach (var id in idSplit)
            if (int.TryParse(id, out var result))
                idList.Add(result);

        return idList.Count == 0 ? 0 : idList[^1];
    }

    public static Voice FromJson(JObject json)
    {
        return new Voice(character2DId: json.GetInt("Character2dId"), voiceId: json.GetString("VoiceId"));
    }
}