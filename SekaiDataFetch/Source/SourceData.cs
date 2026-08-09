using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using SekaiToolsConfiguration;

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


    public static SourceData[] Default => NetworkEndpoints.Current.DefaultDataSources
        .Select(source => new SourceData
        {
            SourceName = source.SourceName,
            SourceTemplate = source.SourceTemplate,
            StorageBaseUrl = source.StorageBaseUrl,
            ActionSetTemplate = source.ActionSetTemplate,
            MemberStoryTemplate = source.MemberStoryTemplate,
            EventStoryTemplate = source.EventStoryTemplate,
            SpecialStoryTemplate = source.SpecialStoryTemplate,
            UnitStoryTemplate = source.UnitStoryTemplate
        })
        .ToArray();

    public static SourceData[] Load(string filepath)
    {
        if (!File.Exists(filepath)) return Default;
        var readItem = JsonSerializer.Deserialize<SourceData[]>(File.ReadAllText(filepath));
        return readItem == null || readItem.Length == 0 ? Default : readItem;
    }

    public static SourceData FromJson(string json)
    {
        var readItem = JsonSerializer.Deserialize<SourceData>(json);
        return readItem ?? throw new Exception("Failed to parse source data from json");
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