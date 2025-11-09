using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsBase.Story;

public class Story
{
    [Flags]
    public enum StoryEventType
    {
        Dialog = 0b001,
        Banner = 0b010,
        Marker = 0b100
    }

    public readonly BaseStoryEvent[] Events;

    public Story(GameScript.GameScript gameScript, TranslationData translationData)
    {
        List<BaseStoryEvent> events = [];
        if (!gameScript.Empty())
        {
            int dialogCount = 0, effectCount = 0;
            int bannerCount = 0, markerCount = 0;
            foreach (var snippet in gameScript.Snippets)
                switch (snippet.Action)
                {
                    case 1:
                    {
                        var talkData = gameScript.TalkData[dialogCount];

                        if (dialogCount < gameScript.TalkData.Length)
                        {
                            var storyDialogEvent = new DialogStoryEvent(
                                dialogCount,
                                talkData.Body, talkData.GetCharacterId(),
                                talkData.WindowDisplayName,
                                talkData.WhenFinishCloseWindow == 1,
                                talkData.Shake
                            );
                            events.Add(storyDialogEvent);
                        }

                        dialogCount += 1;
                        break;
                    }
                    case 6:
                    {
                        var seData = gameScript.SpecialEffectData[effectCount];
                        switch (seData.EffectType)
                        {
                            case 8:
                                events.Add(new BannerStoryEvent(seData.StringVal, bannerCount, events.Count));
                                bannerCount++;
                                break;
                            case 18:
                                events.Add(new MarkerStoryEvent(seData.StringVal, markerCount));
                                markerCount++;
                                break;
                        }

                        effectCount += 1;
                        break;
                    }
                }
        }

        Events = events.ToArray();
        if (translationData.IsEmpty()) return;
        if (!translationData.IsApplicable(gameScript)) throw new Exception("Translation data is not applicable");
        for (var i = 0; i < Events.Length; i++)
            if (Events[i] is not DialogStoryEvent)
            {
                Events[i].BodyTranslated = translationData.Translations[i].Body;
            }
            else
            {
                var dialog = (DialogStoryEvent)Events[i];
                dialog.SetTranslation(((DialogTranslate)translationData.Translations[i]).Chara,
                    ((DialogTranslate)translationData.Translations[i]).Body);
            }
    }

    public static Story FromFile(string gameStoryDataPath, string translationDataPath = "")
    {
        if (!File.Exists(gameStoryDataPath)) throw new Exception("File not found");
        var jsonData = new GameScript.GameScript(gameStoryDataPath);
        var textData = File.Exists(translationDataPath)
            ? new TranslationData(translationDataPath)
            : new TranslationData(null);
        return new Story(jsonData, textData);
    }

    private int IndexInType(StoryEventType types, int index)
    {
        var i = 0;
        foreach (var e in Events)
        {
            if (types.HasFlag(StoryEventType.Dialog) && e.Type == "Dialog")
            {
                if (i == index) return i;
            }
            else if (types.HasFlag(StoryEventType.Banner) && e.Type == "Banner")
            {
                if (i == index) return i;
            }
            else if (types.HasFlag(StoryEventType.Marker) && e.Type == "Marker")
            {
                if (i == index) return i;
            }

            i += 1;
        }

        return -1;
    }

    public BaseStoryEvent[] GetTypes(StoryEventType types)
    {
        var result = new List<BaseStoryEvent>();
        foreach (var @event in Events)
            if (types.HasFlag(StoryEventType.Dialog) && @event.Type == "Dialog") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Banner) && @event.Type == "Banner") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Marker) && @event.Type == "Marker") result.Add(@event);

        return result.ToArray();
    }

    public DialogStoryEvent[] Dialogs()
    {
        var result = new List<DialogStoryEvent>();
        foreach (var v in Events)
            if (v is DialogStoryEvent @event)
                result.Add(@event);

        return result.ToArray();
    }

    public BannerStoryEvent[] Banners()
    {
        var result = new List<BannerStoryEvent>();
        foreach (var v in Events)
            if (v is BannerStoryEvent @event)
                result.Add(@event);

        return result.ToArray();
    }

    public MarkerStoryEvent[] Markers()
    {
        var result = new List<MarkerStoryEvent>();
        foreach (var v in Events)
            if (v is MarkerStoryEvent @event)
                result.Add(@event);

        return result.ToArray();
    }

    public BaseStoryEvent[] Effects()
    {
        var result = new List<BaseStoryEvent>();
        foreach (var v in Events)
            switch (v)
            {
                case BannerStoryEvent banner:
                    result.Add(banner);
                    break;
                case MarkerStoryEvent marker:
                    result.Add(marker);
                    break;
            }

        return result.ToArray();
    }
}