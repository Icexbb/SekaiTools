using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SekaiToolsCore;

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
    /*
     * 1 TalkData
     * 2 LayoutData Type=*
     * 3
     * 4 LayoutData Type=0
     * 5
     * 6 SpecialEffectData
     * 7 SoundData
     * 8 ScenarioSnippetCharacterLayoutModes
     */
}

internal struct TalkDataItem
{
    public string WindowDisplayName;
    public string Body;
    public int WhenFinishCloseWindow;
    public VoiceData[] Voices;
    public bool Shake;

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

    /* EffectType
     * 1 渐入
     * 2 渐出-黑
     * 3 闪烁
     * 4 渐出-白
     * 5 画面震动-背景
     * 6 对话震动 *
     * 7 背景
     * 8 地点横幅
     * 18 地点角标
     * 23 选项
     */
    public string StringVal;
}

internal class GameStoryData
{
    internal SnippetItem[] Snippets;
    internal SpecialEffectDataItem[] SpecialEffectData;
    internal readonly TalkDataItem[] TalkData;

    public GameStoryData(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath)) throw new Exception("File not found");

        var jsonString = File.ReadAllText(jsonFilePath);
        var data = JsonConvert.DeserializeObject<JObject>(jsonString) ?? throw new Exception("Json parse error");

        TalkData = data["TalkData"]!
            .ToObject<JObject[]>()!
            .Select(v => new TalkDataItem
            {
                WindowDisplayName = v["WindowDisplayName"]!.ToObject<string>()!, Body = v["Body"]!.ToObject<string>()!,
                WhenFinishCloseWindow = v["WhenFinishCloseWindow"]!.ToObject<int>(),
                Voices = v["Voices"]!.ToObject<JObject[]>()!.Select(voice => new VoiceData
                {
                    Character2DId = voice["Character2dId"]!.ToObject<int>(),
                    VoiceId = voice["VoiceId"]!.ToObject<string>()!
                }).ToArray()
            }).ToArray();

        Snippets = data["Snippets"]!
            .ToObject<JObject[]>()!
            .Select(v => new SnippetItem { Action = v["Action"]!.ToObject<int>() })
            .ToArray();

        SpecialEffectData = data["SpecialEffectData"]!
            .ToObject<JObject[]>()!
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
        List<int> shakeIndex = [];
        var tdc = 0;
        var sec = 0;
        foreach (var item in Snippets)
        {
            switch (item.Action)
            {
                case 1:
                    tdc += 1;
                    break;
                case 6:
                {
                    if (SpecialEffectData[sec].EffectType is 6) shakeIndex.Add(tdc - 1);
                    sec += 1;
                    break;
                }
            }
        }

        foreach (var i in shakeIndex) TalkData[i].Shake = true;

        List<SnippetItem> sn = [];
        var seCount = 0;
        foreach (var snippet in Snippets)
            switch (snippet.Action)
            {
                case 1:
                    sn.Add(snippet);
                    break;
                case 6:
                {
                    var seData = SpecialEffectData[seCount];
                    if (seData.EffectType is 8 or 18) sn.Add(snippet);
                    seCount += 1;
                    break;
                }
            }

        Snippets = sn.ToArray();
        SpecialEffectData = SpecialEffectData
            .Where(seData => seData.EffectType is 8 or 18).ToArray();
    }

    public bool Empty()
    {
        return TalkData.Length + Snippets.Length + SpecialEffectData.Length == 0;
    }
}

internal abstract class Translation(string body)
{
    public readonly string Body = body;
}

internal class DialogTranslate(string chara, string body) : Translation(body)
{
    public readonly string Chara = chara;
}

internal class EffectTranslate(string body) : Translation(body)
{
}

internal partial class TranslationData
{
    // public readonly DialogTranslate[] Dialogs;
    // public readonly EffectTranslate[] Effects;
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

    public bool IsApplicable(GameStoryData gameStoryData)
    {
        if (gameStoryData.Empty()) return true;
        if (IsEmpty()) return true;

        if (DialogCount() != gameStoryData.TalkData.Length) return false;
        if (EffectCount() != gameStoryData.SpecialEffectData.Length) return false;

        for (var i = 0; i < gameStoryData.Snippets.Length; i++)
        {
            switch (gameStoryData.Snippets[i].Action)
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

public abstract class StoryEvent(string type, string bodyOriginal) : ICloneable
{
    public readonly string Type = type;
    public readonly string BodyOriginal = bodyOriginal;
    public string BodyTranslated = "";

    public string FinalContent => BodyTranslated.Length > 0 ? BodyTranslated : BodyOriginal;

    public abstract object Clone();
}

public class StoryDialogEvent(
    int index,
    string bodyOriginal,
    int characterId,
    string characterOriginal,
    bool closeWindow,
    bool shake)
    : StoryEvent("Dialog", bodyOriginal)
{
    public readonly int Index = index;
    public readonly int CharacterId = characterId;
    public readonly string CharacterOriginal = characterOriginal;
    public string CharacterTranslated = "";
    public readonly bool CloseWindow = closeWindow;
    public readonly bool Shake = shake;

    public void SetTranslation(string character, string body)
    {
        CharacterTranslated = character;
        BodyTranslated = body;
    }

    public string FinalCharacter => CharacterTranslated.Length > 0 && CharacterTranslated != CharacterOriginal
        ? CharacterTranslated
        : "";

    public override object Clone()
    {
        var cloned = new StoryDialogEvent(Index, BodyOriginal, CharacterId, CharacterOriginal, CloseWindow, Shake)
        {
            BodyTranslated = BodyTranslated
        };
        return cloned;
    }
}

internal class StoryBannerEvent(string bodyOriginal) : StoryEvent("Banner", bodyOriginal)
{
    public override object Clone()
    {
        var cloned = new StoryBannerEvent(BodyOriginal) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}

internal class StoryMarkerEvent(string bodyOriginal) : StoryEvent("Marker", bodyOriginal)
{
    public override object Clone()
    {
        var cloned = new StoryMarkerEvent(BodyOriginal) { BodyTranslated = BodyTranslated };
        return cloned;
    }
}

internal class StoryData
{
    private readonly StoryEvent[] _events;

    private StoryData(GameStoryData gameStoryData, TranslationData translationData)
    {
        List<StoryEvent> events = [];
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
        if (translationData.IsEmpty()) return;
        if (!translationData.IsApplicable(gameStoryData)) throw new Exception("Translation data is not applicable");
        for (var i = 0; i < _events.Length; i++)
        {
            if (_events[i] is not StoryDialogEvent) _events[i].BodyTranslated = translationData.Translations[i].Body;
            else
            {
                var dialog = (StoryDialogEvent)_events[i];
                dialog.SetTranslation(((DialogTranslate)translationData.Translations[i]).Chara,
                    ((DialogTranslate)translationData.Translations[i]).Body);
            }
        }
    }

    public static StoryData FromFile(string gameStoryDataPath, string translationDataPath = "")
    {
        if (!File.Exists(gameStoryDataPath)) throw new Exception("File not found");
        var jsonData = new GameStoryData(gameStoryDataPath);
        var textData = File.Exists(translationDataPath)
            ? new TranslationData(translationDataPath)
            : new TranslationData(null);
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
            if (types.HasFlag(StoryEventType.Dialog) && @event.Type == "Dialog") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Banner) && @event.Type == "Banner") result.Add(@event);
            else if (types.HasFlag(StoryEventType.Marker) && @event.Type == "Marker") result.Add(@event);
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