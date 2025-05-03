using SekaiDataFetch.Data;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.Item;

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
            SourceType.SiteHaruki =>
                $"https://storage.haruki.wacca.cn/assets/startapp/character/member/" +
                $"{episode.AssetBundleName}/{episode.ScenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}