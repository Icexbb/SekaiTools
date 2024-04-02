namespace SekaiToolsCore.Story.Fetch.List;

public class CardEpisode
{
    public Dictionary<string, Dictionary<string, string>> Data;

    public CardEpisode()
    {
        Data = new Dictionary<string, Dictionary<string, string>>();
    }

    public CardEpisode(IEnumerable<Data.CardEpisode> cardEpisodes, IEnumerable<Data.Card> cards)
    {
        Data = new Dictionary<string, Dictionary<string, string>>();
        foreach (var cardEpisode in cardEpisodes)
        {
        }
    }

    public void Add(string key, Dictionary<string, string> value)
    {
        Data.Add(key, value);
    }

    public Dictionary<string, string> Get(string key)
    {
        return Data[key];
    }
}