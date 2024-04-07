using System.Text.RegularExpressions;
using SekaiToolsCore.Story.Game;

namespace SekaiToolsCore.Story.Translation;

public partial class TranslationData
{
    public readonly List<Translation> Translations = [];

    public TranslationData(string? filePath)
    {
        if (filePath is null) return;
        if (!File.Exists(filePath)) throw new Exception("File not found");

        var fileStrings = File.ReadAllLines(filePath).ToList();
        fileStrings.Select(line => line.Trim()).ToList().RemoveAll(line => line == "");
        foreach (var line in fileStrings)
        {
            if (line.Length == 0) continue;
            var matches = DialogPattern().Match(line);
            if (matches.Success)
                Translations.Add(new DialogTranslate(matches.Groups[1].Value, matches.Groups[2].Value));
            else
                Translations.Add(new EffectTranslate(line));
        }
    }

    public bool IsEmpty() => Translations.Count == 0;

    private int DialogCount() => Translations.Count(translation => translation is DialogTranslate);

    private int EffectCount() => Translations.Count(translation => translation is EffectTranslate);

    public bool IsApplicable(GameData gameData)
    {
        if (gameData.Empty()) return true;
        if (IsEmpty()) return true;

        if (DialogCount() != gameData.TalkData.Length) return false;
        if (EffectCount() != gameData.SpecialEffectData.Length) return false;

        for (var i = 0; i < gameData.Snippets.Length; i++)
        {
            switch (gameData.Snippets[i].Action)
            {
                case 1:
                    if (Translations[i] is not DialogTranslate) return false;
                    break;
                case 6:
                    if (Translations[i] is not EffectTranslate) return false;
                    break;
            }
        }

        return true;
    }

    [GeneratedRegex("^([^：]+)：(.*)$")]
    private static partial Regex DialogPattern();
}