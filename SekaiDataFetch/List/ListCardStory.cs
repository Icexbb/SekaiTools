using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListCardStory
{
    private static readonly string CachePathCardEpisodes =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cardEpisodes.json");

    private static readonly string CachePathCards =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cards.json");

    private Fetcher Fetcher { get; }
    public List<CardStoryImpl> Data = [];

    public ListCardStory(SourceList.SourceType sourceType = SourceList.SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCardEpisodes)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCards)!);

        if (!File.Exists(CachePathCardEpisodes) || !File.Exists(CachePathCards)) return;
        var dataCardEpisodes = File.ReadAllText(CachePathCardEpisodes);
        var dataCards = File.ReadAllText(CachePathCards);

        var jObjCardEpisodes = JsonConvert.DeserializeObject<JObject[]>(dataCardEpisodes);
        var jObjCards = JsonConvert.DeserializeObject<JObject[]>(dataCards);

        if (jObjCardEpisodes != null && jObjCards != null)
            GetData(jObjCardEpisodes.Select(CardEpisode.FromJson).ToList(), jObjCards.Select(Card.FromJson).ToList());
    }

    public async Task Refresh()
    {
        var jsonCardEpisodes = await Fetcher.FetchSource(Fetcher.Source.CardEpisodes);
        var jsonCards = await Fetcher.FetchSource(Fetcher.Source.Cards);
        if (jsonCardEpisodes == null || jsonCards == null)
            throw new Exception("Failed to fetch event stories or game events");
        await File.WriteAllTextAsync(CachePathCardEpisodes, JsonConvert.SerializeObject(jsonCardEpisodes));
        await File.WriteAllTextAsync(CachePathCards, JsonConvert.SerializeObject(jsonCards));
        GetData(jsonCardEpisodes.Select(CardEpisode.FromJson).ToList(), jsonCards.Select(Card.FromJson).ToList());
    }

    private void GetData(List<CardEpisode> cardEpisodes, List<Card> cards)
    {
        foreach (var card in cards)
        {
            var firstPart = cardEpisodes.FirstOrDefault(episode =>
                episode.CardId == card.Id && episode.CardEpisodePartType == "first_part");
            var secondPart = cardEpisodes.FirstOrDefault(episode =>
                episode.CardId == card.Id && episode.CardEpisodePartType == "second_part");

            if (firstPart == null || secondPart == null) continue;
            Data.Add(new CardStoryImpl(card, firstPart, secondPart));
        }

        Data.Sort((a, b) => a.Card.Id.CompareTo(b.Card.Id));
    }
}

public enum CardEpisodeType
{
    FirstPart,
    SecondPart
}

public class CardStoryImpl(Card card, CardEpisode firstPart, CardEpisode secondPart) : ICloneable
{
    public Card Card { get; } = card;
    public CardEpisode FirstPart { get; } = firstPart;
    public CardEpisode SecondPart { get; } = secondPart;

    public object Clone()
    {
        return new CardStoryImpl((Card)Card.Clone(), (CardEpisode)FirstPart.Clone(), (CardEpisode)SecondPart.Clone());
    }

    public string Url(CardEpisodeType type, SourceList.SourceType sourceType)
    {
        var episode = type switch
        {
            CardEpisodeType.FirstPart => FirstPart,
            CardEpisodeType.SecondPart => SecondPart,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        return sourceType switch
        {
            SourceList.SourceType.SiteBest => $"https://storage.sekai.best/sekai-jp-assets/character" +
                                              $"/member/{episode.AssetBundleName}_rip" +
                                              $"/{episode.ScenarioId}.asset",
            SourceList.SourceType.SiteAi => $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/character" +
                                            $"/member/{episode.AssetBundleName}/" +
                                            $"{episode.ScenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}