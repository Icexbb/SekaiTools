namespace SekaiToolsCore.Story.Game;

public struct Talk
{
    public struct TalkCharacters
    {
        public int Character2dId;
    }

    public TalkCharacters[] Characters;
    public string WindowDisplayName;

    public string Body;
    public int WhenFinishCloseWindow;
    public Voice[] Voices;
    public bool Shake;

    public readonly int GetCharacterId()
    {
        if (Voices.Length == 0 && Characters.Length == 0) return 0;
        if (Voices.Length > 1 || Characters.Length > 1) return 0;
        if (Voices.Length != 1 && Characters.Length != 1) return 0;

        if (Characters.Length == 1)
        {
            var charaIdFromCharaL2dId =
                Constants.C2dIdToCid.GetValueOrDefault(Characters[0].Character2dId, 0);
            if (charaIdFromCharaL2dId is >= 1 and <= 26)
                return charaIdFromCharaL2dId;
        }
        else if (Voices.Length == 1)
        {
            var charaIdFromCharaL2dId =
                Constants.C2dIdToCid.GetValueOrDefault(Voices[0].Character2DId, 0);
            if (charaIdFromCharaL2dId is >= 1 and <= 26)
                return charaIdFromCharaL2dId;
        }

        return 0;
    }
}