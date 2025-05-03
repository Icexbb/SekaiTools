using SekaiDataFetch.Source;

namespace SekaiDataFetch.Item;

public class SpecialStorySet(string title, SpecialStorySet.Episode[]? episodes = null)
{
    public string Title { get; } = title;
    public Episode[] Episodes { get; } = episodes ?? [];

    public class Episode(string title, string assetBundleName, string scenarioId)
    {
        public string Title { get; } = title;
        public string ScenarioId { get; } = scenarioId;
        public string AssetBundleName { get; } = assetBundleName;

        public string Url(SourceType sourceType = 0)
        {
            return sourceType switch
            {
                SourceType.SiteBest =>
                    $"https://storage.sekai.best/sekai-jp-assets/scenario/special/" +
                    $"{AssetBundleName}_rip/{ScenarioId}.asset",
                SourceType.SiteHaruki =>
                    $"https://storage.haruki.wacca.cn/assets/startapp/scenario/special/" +
                    $"{AssetBundleName}/{ScenarioId}.json",
                _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
            };
        }
    }
}