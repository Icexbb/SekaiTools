namespace SekaiToolsCore.Story.Game;

public struct Voice
{
    public int Character2DId;
    public string VoiceId;

    public readonly int GetCharacterId()
    {
        var charaIdFromCharaL2dId =
            Constants.C2dIdToCid.GetValueOrDefault(Character2DId, 0);
        if (charaIdFromCharaL2dId is >= 1 and <= 26) return charaIdFromCharaL2dId;
        var idSplit = VoiceId.Split('_');
        List<int> idList = [];
        foreach (var id in idSplit)
        {
            if (int.TryParse(id, out var result))
                idList.Add(result);
        }

        return idList.Count == 0 ? 0 : idList[^1];
    }
}