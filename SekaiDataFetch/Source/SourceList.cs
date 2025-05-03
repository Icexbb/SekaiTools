namespace SekaiDataFetch.Source;

public class SourceList
{
    public SourceList(SourceType sourceType = 0)
    {
        SetSource(sourceType);
    }

    private Source Source { get; set; } = null!;

    private SourceType _sourceType;

    public SourceType SourceType
    {
        get => _sourceType;
        private set
        {
            _sourceType = value;
            Source = _sourceType switch
            {
                SourceType.SiteBest => new Source(new SourceData
                {
                    SourceTemplate = "https://sekai-world.github.io/sekai-master-db-diff/{type}.json",
                    ActionSetTemplate =
                        "https://storage.sekai.best/sekai-jp-assets/scenario/actionset/" +
                        "{abName}_rip/{scenarioId}.asset",
                    MemberStoryTemplate =
                        "https://storage.sekai.best/sekai-jp-assets/character/member/" +
                        "{abName}_rip/{scenarioId}.asset",
                    EventStoryTemplate =
                        "https://storage.sekai.best/sekai-jp-assets/event_story/" +
                        "{abName}/scenario_rip/{scenarioId}.asset",
                    SpecialStoryTemplate =
                        "https://storage.sekai.best/sekai-jp-assets/scenario/special/" +
                        "{abName}_rip/{scenarioId}.asset",
                    UnitStoryTemplate = "https://storage.sekai.best/sekai-jp-assets/scenario/unitstory/" +
                                        "{abName}_rip/{scenarioId}.asset"
                }),
                SourceType.SiteHaruki => new Source(new SourceData
                {
                    SourceTemplate = "https://api.pjsek.ai/database/master/{type}.json",
                    ActionSetTemplate =
                        "https://storage.haruki.wacca.cn/assets/startapp/scenario/actionset/" +
                        "{abName}/{scenarioId}.json",
                    MemberStoryTemplate =
                        "https://storage.haruki.wacca.cn/assets/startapp/character/member/" +
                        "{abName}/{scenarioId}.json",
                    EventStoryTemplate =
                        "https://storage.haruki.wacca.cn/assets/ondemand/event_story/" +
                        "{abName}/{scenarioId}.json",
                    SpecialStoryTemplate =
                        "https://storage.haruki.wacca.cn/assets/startapp/scenario/special/" +
                        "{abName}/{scenarioId}.json",
                    UnitStoryTemplate = "https://storage.haruki.wacca.cn/assets/startapp/scenario/unitstory/" +
                                        "{abName}/{scenarioId}.json",
                }),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public string ActionSets => Source.ActionSets;
    public string Events => Source.Events;
    public string EventStories => Source.EventStories;
    public string Character2ds => Source.Character2ds;
    public string Cards => Source.Cards;
    public string CardEpisodes => Source.CardEpisodes;
    public string UnitStories => Source.UnitStories;
    public string SpecialStories => Source.SpecialStories;
    public string Areas => Source.Areas;
    public string GameCharacters => Source.GameCharacters;
    public string CharacterProfiles => Source.CharacterProfiles;
    public string UnitProfiles => Source.UnitProfiles;

    public void SetSource(SourceType sourceType)
    {
        SourceType = sourceType;
    }
}

public enum SourceType
{
    SiteBest = 0,
    SiteHaruki = 1,
}