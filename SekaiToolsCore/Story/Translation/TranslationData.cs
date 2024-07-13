using SekaiToolsCore.Story.Game;

namespace SekaiToolsCore.Story.Translation;

public class TranslationData
{
    public readonly List<Translation> Translations = [];

    public TranslationData(string? filePath)
    {
        if (filePath is null) return;
        if (!File.Exists(filePath)) throw new Exception("File not found");

        var fileStrings = File.ReadAllLines(filePath).ToList();

        fileStrings = fileStrings.Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList();
        fileStrings.ForEach(line =>
        {
            Translations.Add(line.Contains('：')
                ? new DialogTranslate(line.Split('：', 2)[0], line.Split('：', 2)[1])
                : new EffectTranslate(line));
        });
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
}