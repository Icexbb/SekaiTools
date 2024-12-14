namespace SekaiToolsCore.Story.Game;

public struct TalkCharacter(int character2dId)
{
    public int Character2dId { get; set; } = character2dId;
}

public record struct Talk()
{
    public Talk(string WindowDisplayName,
        string Body,
        int WhenFinishCloseWindow,
        Voice[]? Voices = null,
        TalkCharacter[]? TalkCharacters = null) : this()
    {
        this.Body = Body;
        this.WindowDisplayName = WindowDisplayName;
        this.WhenFinishCloseWindow = WhenFinishCloseWindow;
        this.TalkCharacters = TalkCharacters ?? [];
        this.Voices = Voices ?? [];
    }

    public string Body { get; set; }
    public string WindowDisplayName { get; set; }
    public int WhenFinishCloseWindow { get; set; }
    public TalkCharacter[] TalkCharacters { get; set; }
    public Voice[] Voices { get; set; }
    public bool Shake { get; set; } = false;

    public readonly int GetCharacterId()
    {
        if (Voices.Length == 0 && TalkCharacters.Length == 0) return 0;
        if (Voices.Length > 1 || TalkCharacters.Length > 1) return 0;
        if (Voices.Length != 1 && TalkCharacters.Length != 1) return 0;

        if (TalkCharacters.Length == 1)
        {
            var charaIdFromCharaL2dId =
                Constants.C2dIdToCid.GetValueOrDefault(TalkCharacters[0].Character2dId, 0);
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