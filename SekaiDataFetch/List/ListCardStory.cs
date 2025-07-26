using Microsoft.Extensions.Logging;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListCardStory : BaseListStory
{
    [CachePath("cardEpisodes")]
    private static string CachePathCardEpisodes =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cardEpisodes.json");

    [CachePath("cards")]
    private static string CachePathCards =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "cards.json");

    [SourcePath("cardEpisodes")] private static string SourceCardEpisodes => Fetcher.SourceList.CardEpisodes;
    [SourcePath("cards")] private static string SourceCards => Fetcher.SourceList.Cards;


    public readonly List<CardStorySet> Data = [];

    private ListCardStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListCardStory Instance { get; } = new();

    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCardEpisodes)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCards)!);

        if (!File.Exists(CachePathCardEpisodes) || !File.Exists(CachePathCards)) return;

        var stringCardEpisodes = File.ReadAllText(CachePathCardEpisodes);
        var stringCards = File.ReadAllText(CachePathCards);

        try
        {
            var cardEpisodes = Utils.Deserialize<CardEpisode[]>(stringCardEpisodes);
            var cards = Utils.Deserialize<Card[]>(stringCards);

            if (cardEpisodes == null || cards == null) throw new Exception("Json parse error");
            GetData(cardEpisodes, cards);
        }
        catch (Exception e)
        {
            Log.Logger.LogError(e,
                "{TypeName} Failed to load data. Clearing cache and retrying. Error: {Message}",
                GetType().Name, e.Message);
            ClearCache();
        }
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
            Data.Add(new CardStorySet(card, firstPart, secondPart));
        }

        Data.Sort((a, b) => a.Card.Id.CompareTo(b.Card.Id));
    }
}