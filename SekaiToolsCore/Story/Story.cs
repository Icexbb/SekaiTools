using SekaiToolsCore.Story.Event;
using SekaiToolsCore.Story.Game;
using SekaiToolsCore.Story.Translation;

namespace SekaiToolsCore.Story;

public class Story
{
    public readonly Event.Event[] Events;

    public Story(GameData gameData, TranslationData translationData)
    {
        List<Event.Event> events = [];
        if (!gameData.Empty())
        {
            int dialogCount = 0, effectCount = 0;
            int bannerCount = 0, markerCount = 0;
            foreach (var snippet in gameData.Snippets)
                switch (snippet.Action)
                {
                    case 1:
                    {
                        var talkData = gameData.TalkData[dialogCount];

                        if (dialogCount < gameData.TalkData.Length)
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
                        var seData = gameData.SpecialEffectData[effectCount];
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

        Events = events.ToArray();
        if (translationData.IsEmpty()) return;
        if (!translationData.IsApplicable(gameData)) throw new Exception("Translation data is not applicable");
        for (var i = 0; i < Events.Length; i++)
        {
            if (Events[i] is not Dialog) Events[i].BodyTranslated = translationData.Translations[i].Body;
            else
            {
                var dialog = (Dialog)Events[i];
                dialog.SetTranslation(((DialogTranslate)translationData.Translations[i]).Chara,
                    ((DialogTranslate)translationData.Translations[i]).Body);
            }
        }
    }

    public static Story FromFile(string gameStoryDataPath, string translationDataPath = "")
    {
        if (!File.Exists(gameStoryDataPath)) throw new Exception("File not found");
        var jsonData = new GameData(gameStoryDataPath);
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

    private Event.Event[] GetTypes(StoryEventType types)
    {
        var result = new List<Event.Event>();
        foreach (var @event in Events)
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
        foreach (var v in Events)
        {
            if (v is Dialog @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Banner[] Banners()
    {
        var result = new List<Banner>();
        foreach (var v in Events)
        {
            if (v is Banner @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Marker[] Markers()
    {
        var result = new List<Marker>();
        foreach (var v in Events)
        {
            if (v is Marker @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public Event.Event[] Effects()
    {
        var result = new List<Event.Event>();
        foreach (var v in Events)
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