namespace SekaiDataFetch.Source;

public class Source(SourceData data)
{
    private SourceData SourceData { get; set; } = data;

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


    public string ActionSet(string scenarioId, string abName)
    {
        return SourceData.ActionSetTemplate.Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string MemberStory(string scenarioId, string abName)
    {
        return SourceData.MemberStoryTemplate.Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string EventStory(string scenarioId, string abName)
    {
        return SourceData.MemberStoryTemplate.Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string SpecialStory(string scenarioId, string abName)
    {
        return SourceData.SpecialStoryTemplate.Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }

    public string UnitStory(string scenarioId, string abName)
    {
        return SourceData.UnitStoryTemplate.Replace("{scenarioId}", scenarioId)
            .Replace("{abName}", abName);
    }
}