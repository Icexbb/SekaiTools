namespace SekaiToolsBase.Story.Translation;

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
                ? new DialogTranslate(line.Split('：', 2)[0], line.Split('：', 2)[1].Replace("…", "..."))
                : new EffectTranslate(line));
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
}