namespace SekaiDataFetch.List;

public class CardEpisode()
{
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> Data = new();

    public CardEpisode(IReadOnlyCollection<Data.CardEpisode> cardEpisodes, IReadOnlyCollection<Data.Card> cards,
        SourceList.SourceType source = SourceList.SourceType.SiteBest) : this()
    {
        foreach (var charaId in Constants.CharacterIdToName.Keys)
        {
            var charaName = Constants.CharacterIdToName[charaId];
            var charaDict = new Dictionary<string, Dictionary<string, string>>();
            foreach (var card in cards.Where(card => card.CharacterId == charaId))
            {
                var cardDict = new Dictionary<string, string>();
                var rarity = $"★{card.CardRarityType[7..].ToUpper()}";
                var key = $"{card.Id} - {rarity} {card.Prefix}";

                foreach (var episode in cardEpisodes.Where(episode => episode.CardId == card.Id))
                {
                    var section = episode.CardEpisodePartType == "first_part" ? "前篇" : "后篇";

                    var url = source switch
                    {
                        SourceList.SourceType.SiteBest =>
                            $"https://storage.sekai.best/sekai-assets/character" +
                            $"/member/{episode.AssetBundleName}_rip/{episode.ScenarioId}.asset",
                        SourceList.SourceType.SiteAi =>
                            $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/character" +
                            $"/member/{episode.AssetBundleName}/{episode.ScenarioId}.json",
                        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
                    };
                    cardDict.Add(section, url);
                }

                charaDict.Add(key, cardDict);
            }

            Data.Add(charaName, charaDict);
        }
    }
}