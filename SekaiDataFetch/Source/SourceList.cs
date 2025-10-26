using SekaiDataFetch.Item;
using SekaiToolsBase.Data;

namespace SekaiDataFetch.Source;

public partial class SourceList(SourceData data)
{
    public static SourceList Instance { get; } = new(SourceData.Default[0]);
    public SourceData SourceData { private get; set; } = data;

    public string ActionSets => SourceData.SourceTemplate.Replace("{type}", "actionSets");
    public string Events => SourceData.SourceTemplate.Replace("{type}", "events");
    public string EventStories => SourceData.SourceTemplate.Replace("{type}", "eventStories");
    public string Character2ds => SourceData.SourceTemplate.Replace("{type}", "character2ds");
    public string Cards => SourceData.SourceTemplate.Replace("{type}", "cards");
    public string CardEpisodes => SourceData.SourceTemplate.Replace("{type}", "cardEpisodes");
    public string UnitStories => SourceData.SourceTemplate.Replace("{type}", "unitStories");
    public string SpecialStories => SourceData.SourceTemplate.Replace("{type}", "specialStories");
    public string Areas => SourceData.SourceTemplate.Replace("{type}", "areas");
    public string GameCharacters => SourceData.SourceTemplate.Replace("{type}", "gameCharacters");
    public string CharacterProfiles => SourceData.SourceTemplate.Replace("{type}", "characterProfiles");
    public string UnitProfiles => SourceData.SourceTemplate.Replace("{type}", "unitProfiles");
}

partial class SourceList
{
    public string ActionSet(string scenarioId, string abName)
    {
        return SourceData.StorageBaseUrl + SourceData.ActionSetTemplate
            .Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string MemberStory(string scenarioId, string abName)
    {
        return SourceData.StorageBaseUrl + SourceData.MemberStoryTemplate
            .Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }


    public string SpecialStory(string scenarioId, string abName)
    {
        return SourceData.StorageBaseUrl + SourceData.SpecialStoryTemplate
            .Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string EventStory(string scenarioId, string abName)
    {
        return SourceData.StorageBaseUrl + SourceData.EventStoryTemplate
            .Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }


    public string UnitStory(string scenarioId, string abName)
    {
        return SourceData.StorageBaseUrl + SourceData.UnitStoryTemplate
            .Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string SpecialStory(SpecialStorySet.Episode episode)
    {
        if (SourceData.StorageBaseUrl.Contains("sekai.best", StringComparison.CurrentCultureIgnoreCase))
            return SpecialStory(episode.ScenarioId, episode.Parent.AssetBundleName);

        return SpecialStory(episode.ScenarioId, episode.AssetBundleName);
    }

    public string MemberStory(CardEpisode episode)
    {
        return MemberStory(episode.ScenarioId, episode.AssetBundleName);
    }

    public string ActionSet(AreaStorySet actionSet)
    {
        return ActionSet(actionSet.ScenarioId, $"group{actionSet.Group}");
    }
}