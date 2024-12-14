using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SekaiToolsCore.Story.Game;

public class GameData()
{
    public GameData(string jsonFilePath) : this()
    {
        if (!File.Exists(jsonFilePath)) throw new Exception("File not found");

        var jsonString = File.ReadAllText(jsonFilePath);
        var data = JsonSerializer.Deserialize<GameData>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Json parse error");

        TalkData = data.TalkData;
        Snippets = data.Snippets;
        SpecialEffectData = data.SpecialEffectData;

        List<int> shakeIndex = [];
        var talkDataCount = 0;
        var spEffCount = 0;
        var shaking = false;
        foreach (var item in Snippets)
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

    public Snippet[] Snippets { get; set; } = [];
    public SpecialEffect[] SpecialEffectData { get; set; } = [];
    public Talk[] TalkData { get; set; } = [];

    public bool Empty()
    {
        return TalkData.Length + SpecialEffectData.Length == 0;
    }
}