using System.Text;

namespace SekaiToolsBase.Story.Translation;

public class TranslationData
{
    public readonly List<Translation> Translations = [];
    public bool IsV2 { get; private set; }
    public string? ScenarioId { get; private set; }
    private List<TranslationFormatV2.Entry> _entries = [];

    public TranslationData(string? filePath)
    {
        if (filePath is null) return;
        if (!File.Exists(filePath)) throw new Exception("File not found");

        var fileStrings = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
        if (fileStrings.Any(line => new[] { "@version", "@scenario:", "@dialog:", "@banner:", "@marker:" }
                .Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))))
        {
            IsV2 = true;
            var parsed = TranslationFormatV2.Parse(fileStrings.ToArray());
            ScenarioId = parsed.Scenario;
            _entries = parsed.Entries;
            Translations.AddRange(_entries.Select(entry => entry.Value));
            return;
        }

        fileStrings = fileStrings.Where(l => l.Trim().Length > 0).Where(l => !l.StartsWith('#')).Select(l => l.Trim())
            .ToList();
        fileStrings.ForEach(line =>
        {
            if (!line.Contains('：'))
            {
                Translations.Add(new EffectTranslate(line.Replace("\\N", "\n")));
                return;
            }

            var parts = line.Split('：', 2);
            Translations.Add(new DialogTranslate(parts[0], parts[1].Replace("…", "...").Replace("\\N", "\n")));
        });
    }

    public bool IsEmpty()
    {
        return Translations.Count == 0;
    }

    private int DialogCount()
    {
        return Translations.Count(translation => translation is DialogTranslate);
    }

    private int EffectCount()
    {
        return Translations.Count(translation => translation is EffectTranslate);
    }

    public bool IsApplicable(GameScript.GameScript gameScript)
    {
        if (IsV2)
        {
            ValidateV2(gameScript);
            return true;
        }
        if (gameScript.Empty()) return true;
        if (IsEmpty()) return true;

        if (DialogCount() != gameScript.TalkData.Length) return false;
        if (EffectCount() != gameScript.SpecialEffectData.Length) return false;

        for (var i = 0; i < gameScript.Snippets.Length; i++)
            switch (gameScript.Snippets[i].Action)
            {
                case 1:
                    if (Translations[i] is not DialogTranslate) return false;
                    break;
                case 6:
                    if (Translations[i] is not EffectTranslate) return false;
                    break;
            }

        return true;
    }

    public IReadOnlyList<Translation> ForScript(GameScript.GameScript script)
    {
        if (!IsV2) return Translations;
        ValidateV2(script);
        var lookup = _entries.ToDictionary(e => (e.Type, e.Number), e => e.Value);
        return TranslationFormatV2.Keys(script).Select(key => lookup[key]).ToArray();
    }

    private void ValidateV2(GameScript.GameScript script)
    {
        if (string.IsNullOrWhiteSpace(script.ScenarioId) || ScenarioId != script.ScenarioId)
            throw new FormatException($"翻译剧本标识 {ScenarioId} 与剧本 {script.ScenarioId} 不一致或剧本缺少 ScenarioId");
        var expected = TranslationFormatV2.Keys(script).ToHashSet();
        foreach (var entry in _entries)
            if (!expected.Remove((entry.Type, entry.Number)))
                throw new FormatException($"翻译文件第 {entry.Line} 行：未知编号或类型不匹配 {entry.Type}:{entry.Number}");
        if (expected.Count > 0)
            throw new FormatException("翻译缺少条目：" + string.Join("、", expected.Select(k => $"{k.Type}:{k.Number:D3}")));
    }
}
