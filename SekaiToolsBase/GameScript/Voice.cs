namespace SekaiToolsBase.GameScript;

public struct Voice()
{
    public Voice(int character2DId, string voiceId) : this()
    {
        Character2DId = character2DId;
        VoiceId = voiceId;
    }

    public int Character2DId { get; set; } = 0;
    public string VoiceId { get; set; } = "";

    public int GetCharacterId()
    {
        var charaIdFromCharaL2dId =
            Constants.C2dIdToCid.GetValueOrDefault(Character2DId, 0);
        if (charaIdFromCharaL2dId is >= 1 and <= 26) return charaIdFromCharaL2dId;
        var idSplit = VoiceId.Split('_');
        List<int> idList = [];
        foreach (var id in idSplit)
            if (int.TryParse(id, out var result))
                idList.Add(result);

        return idList.Count == 0 ? 0 : idList[^1];
    }
}