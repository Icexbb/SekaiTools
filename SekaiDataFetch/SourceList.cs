namespace SekaiDataFetch;

public class SourceList
{
    public enum SourceType
    {
        SiteBest,
        SiteAi,
        // UniPjsk
    }

    private SourceType _source;


    public SourceType Source
    {
        get => _source;
        set
        {
            _source = value;
            string root;
            switch (_source)
            {
                case SourceType.SiteBest:
                    root = "https://sekai-world.github.io/sekai-master-db-diff/";
                    Events = root + "events.json";
                    Cards = root + "cards.json";
                    Character2ds = root + "character2ds.json";
                    UnitStories = root + "unitStories.json";
                    EventStories = root + "eventStories.json";
                    CardEpisodes = root + "cardEpisodes.json";
                    ActionSets = root + "actionSets.json";
                    SpecialStories = root + "specialStories.json";
                    break;
                case SourceType.SiteAi:
                    root = "https://api.pjsek.ai/database/master/";
                    Events = root + "events?$limit=2000";
                    Cards = root + "cards?$limit=2000";
                    Character2ds = root + "character2ds?$limit=2000";
                    UnitStories = root + "unitStories?$limit=2000";
                    EventStories = root + "eventStories?$limit=2000";
                    CardEpisodes = root + "cardEpisodes?$limit=2000";
                    ActionSets = root + "actionSets?$limit=2000";
                    SpecialStories = root + "specialStories?$limit=2000";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void SetSource(SourceType sourceType) => Source = sourceType;

    public SourceList(SourceType sourceType = 0) => SetSource(sourceType);

    public string Events { get; private set; } = "";
    public string Cards { get; private set; } = "";
    public string Character2ds { get; private set; } = "";
    public string UnitStories { get; private set; } = "";
    public string EventStories { get; private set; } = "";
    public string CardEpisodes { get; private set; } = "";
    public string ActionSets { get; private set; } = "";
    public string SpecialStories { get; private set; } = "";
}