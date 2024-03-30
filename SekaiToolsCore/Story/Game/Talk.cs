namespace SekaiToolsCore.Story.Game;

public struct Talk
{
    public string WindowDisplayName;
    public string Body;
    public int WhenFinishCloseWindow;
    public Voice[] Voices;
    public bool Shake;

    public readonly int GetCharacterId()
    {
        if (Voices.Length != 1) return 0;
        var cid = Voices[0].GetCharacterId();
        return cid is >= 0 and <= 26 ? cid : 0;
    }
}