using SekaiToolsCore.Story.Event;
using SekaiToolsCore.Story.Game;
using SekaiToolsCore.Story.Translation;

namespace SekaiToolsCore.Story;

public class Story
{
    private readonly Event.Event[] _events;

    private Story(Data data, TranslationData translationData)
    {
        List<Event.Event> events = [];
        if (!data.Empty())
        {
            int dialogCount = 0, effectCount = 0;
            int bannerCount = 0, markerCount = 0;
            foreach (var snippet in data.Snippets)
                switch (snippet.Action)
                {
                    case 1:
                    {
                        var talkData = data.TalkData[dialogCount];

                        if (dialogCount < data.TalkData.Length)
                        {
                            var storyDialogEvent = new Dialog(
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
                        var seData = data.SpecialEffectData[effectCount];
                        switch (seData.EffectType)
                        {
                            case 8:
                                events.Add(new Banner(seData.StringVal, bannerCount));
                                bannerCount++;
                                break;
                            case 18:
                                events.Add(new Marker(seData.StringVal, markerCount));
                                markerCount++;
                                break;
                        }

                        effectCount += 1;
                        break;
                    }
                }
        }

        _events = events.ToArray();
        if (translationData.IsEmpty()) return;
        if (!translationData.IsApplicable(data)) throw new Exception("Translation data is not applicable");
        for (var i = 0; i < _events.Length; i++)
        {
            if (_events[i] is not Dialog) _events[i].BodyTranslated = translationData.Translations[i].Body;
            else
            {
                var dialog = (Dialog)_events[i];
                dialog.SetTranslation(((DialogTranslate)translationData.Translations[i]).Chara,
                    ((DialogTranslate)translationData.Translations[i]).Body);
            }
        }
    }

    public static Story FromFile(string gameStoryDataPath, string translationDataPath = "")
    {
        if (!File.Exists(gameStoryDataPath)) throw new Exception("File not found");
        var jsonData = new Data(gameStoryDataPath);
        var textData = File.Exists(translationDataPath)
            ? new TranslationData(translationDataPath)
            : new TranslationData(null);
        return new Story(jsonData, textData);
    }


    [Flags]
    private enum StoryEventType
    {
        Dialog = 0b001,
        Banner = 0b010,
        Marker = 0b100,
    }

    private int IndexInType(StoryEventType types, int index)
    {
        var i = 0;
        foreach (var e in _events)
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

    private Event.Event[] GetTypes(StoryEventType types)
    {
        var result = new List<Event.Event>();
        foreach (var @event in _events)
        {
            if (types.HasFlag(StoryEventType.Dialog) && @event.Type == "Dialog") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Banner) && @event.Type == "Banner") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Marker) && @event.Type == "Marker") result.Add(@event);
        }

        return result.ToArray();
    }

    public Dialog[] Dialogs()
    {
        var result = new List<Dialog>();
        foreach (var v in _events)
        {
            if (v is Dialog @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Banner[] Banners()
    {
        var result = new List<Banner>();
        foreach (var v in _events)
        {
            if (v is Banner @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Marker[] Markers()
    {
        var result = new List<Marker>();
        foreach (var v in _events)
        {
            if (v is Marker @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Event.Event[] Effects()
    {
        var result = new List<Event.Event>();
        foreach (var v in _events)
        {
            switch (v)
            {
                case Banner banner:
                    result.Add(banner);
                    break;
                case Marker marker:
                    result.Add(marker);
                    break;
            }
        }

        return result.ToArray();
    }
}