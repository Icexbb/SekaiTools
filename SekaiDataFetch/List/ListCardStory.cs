using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListCardStory : BaseListStory
{
    private static readonly string CachePathCardEpisodes =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cardEpisodes.json");

    private static readonly string CachePathCards =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cards.json");

    public readonly List<CardStoryImpl> Data = [];

    public ListCardStory(SourceType sourceType = SourceType.SiteBest, Proxy? proxy = null)
    {
        SetSource(sourceType);
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public async Task Refresh()
    {
        var stringCardEpisodes = await Fetcher.Fetch(Fetcher.Source.CardEpisodes);
        var stringCards = await Fetcher.Fetch(Fetcher.Source.Cards);
        await File.WriteAllTextAsync(CachePathCardEpisodes, stringCardEpisodes);
        await File.WriteAllTextAsync(CachePathCards, stringCards);
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCardEpisodes)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCards)!);

        if (!File.Exists(CachePathCardEpisodes) || !File.Exists(CachePathCards)) return;
        var stringCardEpisodes = File.ReadAllText(CachePathCardEpisodes);
        var stringCards = File.ReadAllText(CachePathCards);

        var cardEpisodes = Utils.Deserialize<CardEpisode[]>(stringCardEpisodes);
        var cards = Utils.Deserialize<Card[]>(stringCards);

        if (cardEpisodes == null || cards == null) throw new Exception("Json parse error");
        GetData(cardEpisodes, cards);
    }

    private void GetData(ICollection<CardEpisode> cardEpisodes, ICollection<Card> cards)
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

    public string Url(CardEpisodeType type, SourceType sourceType)
    {
        var episode = type switch
        {
            CardEpisodeType.FirstPart => FirstPart,
            CardEpisodeType.SecondPart => SecondPart,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        return sourceType switch
        {
            SourceType.SiteBest =>
                $"https://storage.sekai.best/sekai-jp-assets/character/member/" +
                $"{episode.AssetBundleName}_rip/{episode.ScenarioId}.asset",
            SourceType.SiteAi =>
                $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/character/member/" +
                $"{episode.AssetBundleName}/{episode.ScenarioId}.json",
            SourceType.SiteHaruki =>
                $"https://storage.haruki.wacca.cn/assets/startapp/character/member/" +
                $"{episode.AssetBundleName}/{episode.ScenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}