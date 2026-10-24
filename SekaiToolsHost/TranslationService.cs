using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsHost;

public sealed record TranslationRow(int Id, string Kind, string OriginalSpeaker, string Original,
    string Speaker, string Content);
public sealed record TranslationLoad(string Script, string Translation = "");
public sealed record TranslationSave(string Script, string Output, TranslationRow[] Rows);

public static class TranslationService
{
    public static TranslationRow[] Load(TranslationLoad args)
    {
        var script = new GameScript(args.Script);
        var translation = new TranslationData(string.IsNullOrEmpty(args.Translation) ? null : args.Translation);
        if (!translation.IsApplicable(script)) throw new InvalidDataException("翻译不适用于此剧本");
        return Rows(new Story(script, translation));
    }

    private static TranslationRow[] Rows(Story story) => story.Events.Select((item, index) =>
    {
        var dialog = item as DialogStoryEvent;
        return new TranslationRow(index, item.Type, dialog?.CharacterOriginal ?? "", item.BodyOriginal,
            dialog?.FinalCharacter ?? "", item.FinalContent);
    }).ToArray();

    public static string Export(TranslationSave args)
    {
        var script = new GameScript(args.Script);
        var originals = Rows(new Story(script, new TranslationData(null)));
        if (args.Rows.Length != originals.Length) throw new InvalidDataException("翻译条目数量与剧本不一致");
        var values = new List<Translation>();
        for (var i = 0; i < originals.Length; i++)
        {
            var row = args.Rows[i];
            var original = originals[i];
            if (row.Id != i || row.Kind != original.Kind || row.Original != original.Original ||
                row.OriginalSpeaker != original.OriginalSpeaker)
                throw new InvalidDataException($"第 {i + 1} 项与原剧本不一致，请重新载入");
            values.Add(row.Kind == "Dialog" ? new DialogTranslate(row.Speaker, row.Content) : new EffectTranslate(row.Content));
        }
        return TranslationFormatV2.Export(script, values);
    }
}
