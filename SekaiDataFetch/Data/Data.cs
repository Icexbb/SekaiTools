namespace SekaiDataFetch.Data;

public class Data(
    List<ActionSet> actions,
    List<Card> cards,
    List<CardEpisode> cardEpisodes,
    List<Character2d> character2ds,
    List<GameEvent> events,
    List<EventStory> eventStories,
    List<SpecialStory> specialStories,
    List<UnitStory> unitStories)
{
    public List<ActionSet> Actions { get; set; } = actions;
    public List<Card> Cards { get; set; } = cards;
    public List<CardEpisode> CardEpisodes { get; set; } = cardEpisodes;
    public List<Character2d> Character2ds { get; set; } = character2ds;
    public List<GameEvent> Events { get; set; } = events;
    public List<EventStory> EventStories { get; set; } = eventStories;
    public List<SpecialStory> SpecialStories { get; set; } = specialStories;
    public List<UnitStory> UnitStories { get; set; } = unitStories;


    public Data() : this([], [], [], [], [], [], [], [])
    {
    }

    public bool NotComplete => Actions.Count == 0 || Cards.Count == 0 || CardEpisodes.Count == 0 ||
                               Character2ds.Count == 0 || Events.Count == 0 || EventStories.Count == 0 ||
                               SpecialStories.Count == 0 || UnitStories.Count == 0;
}