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

        TalkData = data["TalkData"]!
            .ToObject<JObject[]>()!
            .Select(v => new Talk
            {
                WindowDisplayName = v["WindowDisplayName"]!.ToObject<string>()!,
                Body = v["Body"]!.ToObject<string>()!,
                WhenFinishCloseWindow = v["WhenFinishCloseWindow"]!.ToObject<int>(),
                Voices = v["Voices"]!.ToObject<JObject[]>()!.Select(voice => new Voice
                {
                    Character2DId = voice["Character2dId"]!.ToObject<int>(),
                    VoiceId = voice["VoiceId"]!.ToObject<string>()!
                }).ToArray(),
                Characters = v["TalkCharacters"]!.ToObject<JObject[]>()!.Select(c => new Talk.TalkCharacters
                {
                    Character2dId = c["Character2dId"]!.ToObject<int>()
                }).ToArray()
            }).ToArray();

        Snippets = data["Snippets"]!
            .ToObject<JObject[]>()!
            .Select(v => new Snippet { Action = v["Action"]!.ToObject<int>() })
            .ToArray();

        SpecialEffectData = data["SpecialEffectData"]!
            .ToObject<JObject[]>()!
            .Select(v => new SpecialEffect
            {
                EffectType = v["EffectType"]!.ToObject<int>(),
                StringVal = v["StringVal"]!.ToObject<string>() ?? string.Empty
            })
            .ToArray();
        Clean();
    }

    private void Clean()
    {
        List<int> shakeIndex = [];
        var tdc = 0;
        var sec = 0;
        foreach (var item in Snippets)
        {
            switch (item.Action)
            {
                case 1:
                    tdc += 1;
                    break;
                case 6:
                {
                    if (SpecialEffectData[sec].EffectType is 6) shakeIndex.Add(tdc - 1);
                    sec += 1;
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