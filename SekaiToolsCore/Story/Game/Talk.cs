using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Game;

public struct TalkCharacters(int character2dId)
{
    public readonly int Character2dId = character2dId;

    public static TalkCharacters FromJson(JObject json) => new(json.GetInt("Character2dId"));
}

public struct Talk(
    string name,
    string body,
    int close,
    Voice[]? voices = null,
    TalkCharacters[]? characters = null,
    bool shake = false)
{
    private readonly TalkCharacters[] _characters = characters ?? [];
    private readonly Voice[] _voices = voices ?? [];

    public readonly string Body = body;
    public readonly string WindowDisplayName = name;
    public readonly int WhenFinishCloseWindow = close;
    public bool Shake = shake;

    public readonly int GetCharacterId()
    {
        if (_voices.Length == 0 && _characters.Length == 0) return 0;
        if (_voices.Length > 1 || _characters.Length > 1) return 0;
        if (_voices.Length != 1 && _characters.Length != 1) return 0;

        if (_characters.Length == 1)
        {
            var charaIdFromCharaL2dId =
                Constants.C2dIdToCid.GetValueOrDefault(_characters[0].Character2dId, 0);
            if (charaIdFromCharaL2dId is >= 1 and <= 26)
                return charaIdFromCharaL2dId;
        }
        else if (_voices.Length == 1)
        {
            var charaIdFromCharaL2dId =
                Constants.C2dIdToCid.GetValueOrDefault(_voices[0].Character2DId, 0);
            if (charaIdFromCharaL2dId is >= 1 and <= 26)
                return charaIdFromCharaL2dId;
        }

        return 0;
    }

    public static Talk FromJson(JObject json)
    {
        return new Talk(
            json.GetString("WindowDisplayName"),
            json.GetString("Body"),
            json.GetInt("WhenFinishCloseWindow"),
            json.Get("Voices", Array.Empty<JObject>()).Select(Voice.FromJson).ToArray(),
            json.Get("Characters", Array.Empty<JObject>()).Select(TalkCharacters.FromJson).ToArray()
        );
    }
}