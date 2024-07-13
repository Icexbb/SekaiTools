using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Game;

public class GameData
{
    internal Snippet[] Snippets;
    internal SpecialEffect[] SpecialEffectData;
    internal readonly Talk[] TalkData;

    public GameData(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath)) throw new Exception("File not found");

        var jsonString = File.ReadAllText(jsonFilePath);
        var data = JsonConvert.DeserializeObject<JObject>(jsonString) ?? throw new Exception("Json parse error");

        TalkData = data.Get("TalkData", Array.Empty<JObject>())
            .Select(v => new Talk
            {
                WindowDisplayName = v.Get("WindowDisplayName", ""),
                Body = v.Get("Body", ""),
                WhenFinishCloseWindow = v.Get("WhenFinishCloseWindow", 0),
                Voices = v.Get("Voices", Array.Empty<JObject>()).Select(voice => new Voice
                {
                    Character2DId = voice.Get("Character2dId", 0),
                    VoiceId = voice.Get("VoiceId", ""),
                }).ToArray(),
                Characters = v.Get("Characters", Array.Empty<JObject>())
                    .Select(c => new Talk.TalkCharacters
                    {
                        Character2dId = c.Get("Character2dId", 0)
                    }).ToArray()
            }).ToArray();

        Snippets = data.Get("Snippets", Array.Empty<JObject>())
            .Select(Snippet.FromJObject)
            .ToArray();

        SpecialEffectData = data.Get("SpecialEffectData", Array.Empty<JObject>())
            .Select(SpecialEffect.FromJObject)
            .ToArray();
        Clean();
    }

    private void Clean()
    {
        List<int> shakeIndex = [];
        var talkDataCount = 0;
        var spEffCount = 0;
        var shaking = false;
        foreach (var item in Snippets)
        {
            switch (item.Action)
            {
                case 1:
                    if (shaking) shakeIndex.Add(talkDataCount);
                    talkDataCount += 1;
                    break;
                case 6:
                {
                    var eff = SpecialEffectData[spEffCount];
                    switch (eff.EffectType)
                    {
                        case 6:
                            shakeIndex.Add(talkDataCount - 1);
                            if (eff.Duration > 10) shaking = true;
                            break;
                        case 26:
                            shaking = false;
                            break;
                    }

                    spEffCount += 1;
                    break;
                }
            }
        }

        foreach (var i in shakeIndex) TalkData[i].Shake = true;

        List<Snippet> sn = [];
        var seCount = 0;
        foreach (var snippet in Snippets)
            switch (snippet.Action)
            {
                case 1:
                    sn.Add(snippet);
                    break;
                case 6:
                {
                    var seData = SpecialEffectData[seCount];
                    if (seData.EffectType is 8 or 18) sn.Add(snippet);
                    seCount += 1;
                    break;
                }
            }

        Snippets = sn.ToArray();
        SpecialEffectData = SpecialEffectData
            .Where(seData => seData.EffectType is 8 or 18).ToArray();
    }

    public bool Empty()
    {
        return TalkData.Length + Snippets.Length + SpecialEffectData.Length == 0;
    }
}