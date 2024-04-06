using SekaiToolsCore.Story.Game;

namespace SekaiToolsCore.Story.Fetch.List;

public class CardEpisode()
{
    public Dictionary<string, Dictionary<string, string>> Data = new();

    public CardEpisode(IEnumerable<Data.CardEpisode> cardEpisodes, IReadOnlyCollection<Data.Card> cards,
        Fetcher.SourceList.SourceType source = Fetcher.SourceList.SourceType.SekaiBest) : this()
    {
        Data = new Dictionary<string, Dictionary<string, string>>();
        foreach (var cardEpisode in cardEpisodes)
        {
            if (cardEpisode.ScenarioId == string.Empty) continue;
            var card = cards.FirstOrDefault(x => x.Id == cardEpisode.CardId);
            if (card == null) continue;
            Constants.CharacterIdToName.TryGetValue(card.CharacterId, out var charaName);
            if (charaName == null) continue;

            var rarity = $"★{card.CardRarityType[7..].ToUpper()}";
            var prefix = card.Prefix;
            var section = cardEpisode.CardEpisodePartType == "first_part" ? "前篇" : "后篇";

            var url = source == Fetcher.SourceList.SourceType.SekaiBest
                ? $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/character/member/{cardEpisode.AssetBundleName}/{cardEpisode.ScenarioId}.json"
                : $"https://storage.sekai.best/sekai-assets/character/member/{cardEpisode.AssetBundleName}_rip/{cardEpisode.ScenarioId}.asset";
            var key = $"{cardEpisode.CardId} - {rarity} {prefix} {section}";

            Data.Set(charaName, key, url);
        }
    }
}