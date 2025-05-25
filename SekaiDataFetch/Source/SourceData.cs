using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SekaiDataFetch.Source;

public class SourceData
{
    public required string SourceName { get; init; }
    public required string SourceTemplate { get; init; }

    public required string StorageBaseUrl { get; init; }
    public required string ActionSetTemplate { get; init; }
    public required string MemberStoryTemplate { get; init; }
    public required string EventStoryTemplate { get; init; }
    public required string SpecialStoryTemplate { get; init; }
    public required string UnitStoryTemplate { get; init; }

    public required bool Deletable { get; init; } = true;

    public static SourceData[] Default =>
    [
        new()
        {
            SourceName = "Sekai Best",

            SourceTemplate = "https://sekai-world.github.io/sekai-master-db-diff/{type}.json",
            StorageBaseUrl = "https://storage.sekai.best/sekai-jp-assets/",
            ActionSetTemplate =
                "scenario/actionset/{abName}/{scenarioId}.asset",
            MemberStoryTemplate =
                "character/member/{abName}/{scenarioId}.asset",
            EventStoryTemplate =
                "event_story/{abName}/scenario/{scenarioId}.asset",
            SpecialStoryTemplate =
                "scenario/special/{abName}/{scenarioId}.asset",
            UnitStoryTemplate =
                "scenario/unitstory/{abName}/{scenarioId}.asset",
            Deletable = false
        },
        new()
        {
            SourceName = "Haruki JP",
            SourceTemplate = "https://storage.haruki.wacca.cn/master-jp/{type}.json",
            StorageBaseUrl = "https://sekai-assets-bdf29c81.seiunx.net/jp-assets/",
            ActionSetTemplate =
                "startapp/scenario/actionset/{abName}/{scenarioId}.json",
            MemberStoryTemplate =
                "startapp/character/member/{abName}/{scenarioId}.json",
            EventStoryTemplate =
                "ondemand/event_story/{abName}/scenario/{scenarioId}.json",
            SpecialStoryTemplate =
                "startapp/scenario/special/{abName}/{scenarioId}.json",
            UnitStoryTemplate =
                "startapp/scenario/unitstory/{abName}/{scenarioId}.json",
            Deletable = false
        },
        new()
        {
            SourceName = "Haruki CN",
            SourceTemplate = "https://storage.haruki.wacca.cn/master-jp/{type}.json",
            StorageBaseUrl = "https://storage.haruki.wacca.cn/assets/",
            ActionSetTemplate =
                "startapp/scenario/actionset/{abName}/{scenarioId}.json",
            MemberStoryTemplate =
                "startapp/character/member/{abName}/{scenarioId}.json",
            EventStoryTemplate =
                "ondemand/event_story/{abName}/scenario/{scenarioId}.json",
            SpecialStoryTemplate =
                "startapp/scenario/special/{abName}/{scenarioId}.json",
            UnitStoryTemplate =
                "startapp/scenario/unitstory/{abName}/{scenarioId}.json",
            Deletable = false
        },
    ];

    public static SourceData[] Load(string filepath)
    {
        if (!File.Exists(filepath)) return Default;
        var readItem = JsonSerializer.Deserialize<SourceData[]>(File.ReadAllText(filepath));
        return readItem == null || readItem.Length == 0 ? Default : readItem;
    }

    public static string Dump(SourceData[] data)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        });
    }
}