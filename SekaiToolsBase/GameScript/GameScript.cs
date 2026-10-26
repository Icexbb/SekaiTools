using System.Text.Json;

namespace SekaiToolsBase.GameScript;

public class GameScript()
{
    public GameScript(string jsonFilePath) : this()
    {
        if (!File.Exists(jsonFilePath)) throw new Exception("File not found");

        var jsonString = File.ReadAllText(jsonFilePath);
        var data = JsonSerializer.Deserialize<GameScript>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Json parse error");

        ScenarioId = data.ScenarioId;
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
                        if (spEffCount >= SpecialEffectData.Length) break;
                        var eff = SpecialEffectData[spEffCount];
                        switch (eff.EffectType)
                        {
                            case 6:
                                if (talkDataCount > 0)
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

        foreach (var i in shakeIndex.Distinct())
            if ((uint)i < (uint)TalkData.Length)
                TalkData[i].Shake = true;

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
                        if (seCount >= SpecialEffectData.Length) break;
                        var seData = SpecialEffectData[seCount];
                        if (seData.EffectType is 8 or 18) sn.Add(snippet);
                        seCount += 1;
                        break;
                    }
            }

        Snippets = sn.ToArray();
        OriginalEffectIndices = SpecialEffectData.Select((effect, index) => (effect, index))
            .Where(item => item.effect.EffectType is 8 or 18).Select(item => item.index).ToArray();
        SpecialEffectData = SpecialEffectData
            .Where(seData => seData.EffectType is 8 or 18).ToArray();
    }

    public string ScenarioId { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public int[] OriginalEffectIndices { get; set; } = [];
    public Snippet[] Snippets { get; set; } = [];
    public SpecialEffect[] SpecialEffectData { get; set; } = [];
    public Talk[] TalkData { get; set; } = [];

    public bool Empty()
    {
        return TalkData.Length + SpecialEffectData.Length == 0;
    }
}
