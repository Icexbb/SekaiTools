using SekaiToolsBase.Data;

namespace SekaiDataFetch.Item;

public class CardStorySet(Card card, CardEpisode firstPart, CardEpisode secondPart) : ICloneable
{
    public Card Card { get; } = card;
    public CardEpisode FirstPart { get; } = firstPart;
    public CardEpisode SecondPart { get; } = secondPart;

    public object Clone()
    {
        return new CardStorySet((Card)Card.Clone(), (CardEpisode)FirstPart.Clone(), (CardEpisode)SecondPart.Clone());
    }
}