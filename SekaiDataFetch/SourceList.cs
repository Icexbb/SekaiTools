namespace SekaiDataFetch;

public class SourceList
{
    private SourceType _source;

    public SourceList(SourceType sourceType = 0)
    {
        SetSource(sourceType);
    }

    public SourceType Source
    {
        get => _source;
        private set
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
                    Areas = root + "areas.json";
                    GameCharacters = root + "gameCharacters.json";
                    CharacterProfiles = root + "characterProfiles.json";
                    UnitProfiles = root + "unitProfiles.json";
                    break;
                case SourceType.SiteHaruki:
                    root = "https://storage.haruki.wacca.cn/master-jp/";
                    Events = root + "events.json";
                    Cards = root + "cards.json";
                    Character2ds = root + "character2ds.json";
                    UnitStories = root + "unitStories.json";
                    EventStories = root + "eventStories.json";
                    CardEpisodes = root + "cardEpisodes.json";
                    ActionSets = root + "actionSets.json";
                    SpecialStories = root + "specialStories.json";
                    Areas = root + "areas.json";
                    GameCharacters = root + "gameCharacters.json";
                    CharacterProfiles = root + "characterProfiles.json";
                    UnitProfiles = root + "unitProfiles.json";
                    break;
                case SourceType.SiteAi:
                    root = "https://api.pjsek.ai/database/master/";
                    const string limit = "?$limit=2000";
                    Events = root + "events" + limit;
                    Cards = root + "cards" + limit;
                    Character2ds = root + "character2ds" + limit;
                    UnitStories = root + "unitStories" + limit;
                    EventStories = root + "eventStories" + limit;
                    CardEpisodes = root + "cardEpisodes" + limit;
                    ActionSets = root + "actionSets" + limit;
                    SpecialStories = root + "specialStories" + limit;
                    Areas = root + "areas" + limit;
                    GameCharacters = root + "gameCharacters" + limit;
                    CharacterProfiles = root + "characterProfiles" + limit;
                    UnitProfiles = root + "unitProfiles" + limit;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }


    public string ActionSets { get; private set; } = "";
    public string Events { get; private set; } = "";
    public string EventStories { get; private set; } = "";
    public string Character2ds { get; private set; } = "";
    public string Cards { get; private set; } = "";
    public string CardEpisodes { get; private set; } = "";
    public string UnitStories { get; private set; } = "";
    public string SpecialStories { get; private set; } = "";

    public string Areas { get; private set; } = "";
    public string GameCharacters { get; private set; } = "";
    public string CharacterProfiles { get; private set; } = "";
    public string UnitProfiles { get; private set; } = "";

    public void SetSource(SourceType sourceType)
    {
        Source = sourceType;
    }
}

public enum SourceType
{
    SiteBest = 0,
    SiteHaruki = 1,
    SiteAi = 2,
}