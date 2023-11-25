using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core;

internal struct VoiceData
{
    public int Character2DId;
    public string VoiceId;

    public readonly int GetCharacterId()
    {
        var charaIdFromCharaL2dId =
            Constants.CharacterLive2dIdToCharacterId.GetValueOrDefault(Character2DId, 0);
        if (charaIdFromCharaL2dId is >= 1 and <= 26) return charaIdFromCharaL2dId;
        var idSplit = VoiceId.Split('_');
        List<int> idList = new();
        foreach (var id in idSplit)
            if (int.TryParse(id, out var result))
                idList.Add(result);
        return idList.Count == 0 ? 0 : idList[^1];
    }
}

internal struct SnippetItem
{
    public int Action;
}

internal struct TalkDataItem
{
    public string WindowDisplayName;
    public string Body;
    public int WhenFinishCloseWindow;
    public VoiceData[] Voices;

    public readonly int GetCharacterId()
    {
        if (Voices.Length != 1) return 0;
        var cid = Voices[0].GetCharacterId();
        return cid is >= 0 and <= 26 ? cid : 0;
    }
}

internal struct SpecialEffectDataItem
{
    public int EffectType;
    public string StringVal;
}

internal class GameStoryData
{
    internal SnippetItem[] Snippets;
    internal SpecialEffectDataItem[] SpecialEffectData;
    internal readonly TalkDataItem[] TalkData;

    public GameStoryData(TalkDataItem[] talkData, SnippetItem[] snippets, SpecialEffectDataItem[] specialEffectData)
    {
        TalkData = talkData;
        Snippets = snippets;
        SpecialEffectData = specialEffectData;
        Clean();
    }

    public GameStoryData(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath)) throw new Exception("File not found");

        var jsonString = File.ReadAllText(jsonFilePath);
        var data = JsonConvert.DeserializeObject<JObject>(jsonString) ?? throw new Exception("Json parse error");

        TalkData = data["TalkData"]!.ToObject<JObject[]>()!.Select(v => new TalkDataItem
        {
            WindowDisplayName = v["WindowDisplayName"]!.ToObject<string>()!, Body = v["Body"]!.ToObject<string>()!,
            WhenFinishCloseWindow = v["WhenFinishCloseWindow"]!.ToObject<int>(),
            Voices = v["Voices"]!.ToObject<JObject[]>()!.Select(voice => new VoiceData
            {
                Character2DId = voice["Character2dId"]!.ToObject<int>(), VoiceId = voice["VoiceId"]!.ToObject<string>()!
            }).ToArray()
        }).ToArray();
        Snippets = data["Snippets"]!.ToObject<JObject[]>()!
            .Select(v => new SnippetItem { Action = v["Action"]!.ToObject<int>() }).ToArray();
        SpecialEffectData = data["SpecialEffectData"]!.ToObject<JObject[]>()!
            .Select(v => new SpecialEffectDataItem
            {
                EffectType = v["EffectType"]!.ToObject<int>(),
                StringVal = v["StringVal"]!.ToObject<string>() ?? string.Empty
            })
            .ToArray();
        Clean();
    }

    private void Clean()
    {
        List<SnippetItem> sn = new();
        var seCount = 0;
        foreach (var snippet in Snippets)
            if (snippet.Action == 1)
            {
                sn.Add(snippet);
            }
            else if (snippet.Action == 6)
            {
                var seData = SpecialEffectData[seCount];
                if (seData.EffectType == 8 || seData.EffectType == 18) sn.Add(snippet);
                seCount += 1;
            }

        Snippets = sn.ToArray();
        List<SpecialEffectDataItem> se = new();
        foreach (var seData in SpecialEffectData)
            if (seData.EffectType == 8 || seData.EffectType == 18)
                se.Add(seData);
        SpecialEffectData = se.ToArray();
    }

    public bool Empty()
    {
        return TalkData.Length + Snippets.Length + SpecialEffectData.Length == 0;
    }
}

internal struct DialogTranslate
{
    public string Chara;
    public string Body;
}

internal struct EffectTranslate
{
    public string Body;
}

internal partial class TranslationData
{
    public readonly DialogTranslate[] Dialogs;
    public readonly EffectTranslate[] Effects;

    public TranslationData(DialogTranslate[] dialogs, EffectTranslate[] effects)
    {
        Dialogs = dialogs;
        Effects = effects;
    }

    public TranslationData(string filePath)
    {
        if (!File.Exists(filePath)) throw new Exception("File not found");
        // const string dialogPattern = @"^([^：]+)：(.*)$";

        var fileStrings = File.ReadAllLines(filePath).ToList();
        fileStrings.Select(line => line.Trim()).ToList().RemoveAll(line => line == "");
        List<DialogTranslate> dialogs = new();
        List<EffectTranslate> effects = new();
        foreach (var line in fileStrings)
        {
            var matches = DialogPattern().Match(line);

            if (matches.Success)
                dialogs.Add(new DialogTranslate { Chara = matches.Groups[1].Value, Body = matches.Groups[2].Value });
            else
                effects.Add(new EffectTranslate { Body = line });
        }

        Dialogs = dialogs.ToArray();
        Effects = effects.ToArray();
    }

    public bool Empty()
    {
        return Dialogs.Length + Effects.Length == 0;
    }

    [GeneratedRegex("^([^：]+)：(.*)$")]
    private static partial Regex DialogPattern();
}

internal abstract class StoryEvent
{
    public readonly string Type;
    public readonly string BodyOriginal;
    public string BodyTranslated = "";

    protected StoryEvent(string type, string bodyOriginal)
    {
        Type = type;
        BodyOriginal = bodyOriginal;
    }
}

internal class StoryDialogEvent : StoryEvent
{
    public int CharacterId;
    public readonly string CharacterOriginal;
    public string CharacterTranslated = "";
    public bool CloseWindow;

    public StoryDialogEvent(string bodyOriginal, int characterId, string characterOriginal, bool closeWindow) :
        base("Dialog", bodyOriginal)
    {
        CharacterId = characterId;
        CharacterOriginal = characterOriginal;
        CloseWindow = closeWindow;
    }

    public void SetTranslation(string character, string body)
    {
        CharacterTranslated = character;
        BodyTranslated = body;
    }
}

internal class StoryBannerEvent : StoryEvent
{
    public StoryBannerEvent(string bodyOriginal) : base("Banner", bodyOriginal)
    {
    }

    public void SetTranslation(string body)
    {
        BodyTranslated = body;
    }
}

internal class StoryMarkerEvent : StoryEvent
{
    public StoryMarkerEvent(string bodyOriginal) : base("Marker", bodyOriginal)
    {
    }

    public void SetTranslation(string body)
    {
        BodyTranslated = body;
    }
}

internal class StoryData
{
    private readonly StoryEvent[] _events;

    private StoryData(GameStoryData gameStoryData, TranslationData translationData)
    {
        List<StoryEvent> events = new();
        if (!gameStoryData.Empty())
        {
            int dialogCount = 0, effectCount = 0;
            foreach (var snippet in gameStoryData.Snippets)
                switch (snippet.Action)
                {
                    case 1:
                    {
                        var talkData = gameStoryData.TalkData[dialogCount];

                        if (dialogCount < gameStoryData.TalkData.Length)
                        {
                            var storyDialogEvent = new StoryDialogEvent(
                                talkData.Body,
                                talkData.GetCharacterId(),
                                talkData.WindowDisplayName,
                                talkData.WhenFinishCloseWindow == 1);
                            events.Add(storyDialogEvent);
                        }

                        dialogCount += 1;
                        break;
                    }
                    case 6:
                    {
                        var seData = gameStoryData.SpecialEffectData[effectCount];
                        switch (seData.EffectType)
                        {
                            case 8:
                                events.Add(new StoryBannerEvent(seData.StringVal));
                                break;
                            case 18:
                                events.Add(new StoryMarkerEvent(seData.StringVal));
                                break;
                        }

                        effectCount += 1;
                        break;
                    }
                }
        }

        _events = events.ToArray();
        if (translationData.Empty()) return;
        var i = 0;
        foreach (var dialog in translationData.Dialogs)
        {
            if (i < Dialogs().Length)
            {
                var index = IndexInType(StoryEventType.Dialog, i);
                var dialogEvent = (StoryDialogEvent)_events[index];
                if (index >= 0)
                {
                    dialogEvent.SetTranslation(dialog.Chara, dialog.Body);
                    _events[index] = dialogEvent;
                }
            }

            i += 1;
        }

        i = 0;
        foreach (var effect in translationData.Effects)
        {
            if (i < Effects().Length)
            {
                var index = IndexInType(StoryEventType.Banner | StoryEventType.Marker, i);
                if (index >= 0) _events[index].BodyTranslated = effect.Body;
            }

            i += 1;
        }
    }

    public static StoryData FromFile(string gameStoryDataPath, string translationDataPath = "")
    {
        if (!File.Exists(gameStoryDataPath)) throw new Exception("File not found");
        var jsonData = new GameStoryData(gameStoryDataPath);
        var textData = File.Exists(translationDataPath)
            ? new TranslationData(translationDataPath)
            : new TranslationData(Array.Empty<DialogTranslate>(), Array.Empty<EffectTranslate>());
        return new StoryData(jsonData, textData);
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

    private StoryEvent[] GetTypes(StoryEventType types)
    {
        var result = new List<StoryEvent>();
        foreach (var @event in _events)
        {
            if (types.HasFlag(StoryEventType.Dialog) && @event.Type == "Dialog")
            {
                result.Add(@event);
            }
            else if (types.HasFlag(StoryEventType.Banner) && @event.Type == "Banner")
            {
                result.Add(@event);
            }
            else if (types.HasFlag(StoryEventType.Marker) && @event.Type == "Marker")
            {
                result.Add(@event);
            }
        }

        return result.ToArray();
    }

    public StoryDialogEvent[] Dialogs()
    {
        var result = new List<StoryDialogEvent>();
        foreach (var v in _events)
        {
            if (v is StoryDialogEvent @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public StoryBannerEvent[] Banners()
    {
        var result = new List<StoryBannerEvent>();
        foreach (var v in _events)
        {
            if (v is StoryBannerEvent @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public StoryMarkerEvent[] Markers()
    {
        var result = new List<StoryMarkerEvent>();
        foreach (var v in _events)
        {
            if (v is StoryMarkerEvent @event) result.Add(@event);
        }

        return result.ToArray();
    }

    public StoryEvent[] Effects()
    {
        var result = new List<StoryEvent>();
        foreach (var v in _events)
        {
            switch (v)
            {
                case StoryBannerEvent banner:
                    result.Add(banner);
                    break;
                case StoryMarkerEvent marker:
                    result.Add(marker);
                    break;
            }
        }

        return result.ToArray();
    }
}