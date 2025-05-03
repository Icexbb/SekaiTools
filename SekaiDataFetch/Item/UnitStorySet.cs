using SekaiDataFetch.Source;

namespace SekaiDataFetch.Item;

public class UnitStorySet(string name, UnitStorySet.Chapter[]? chapters = null)
{
    public string Name { get; init; } = name;

    public Chapter[] Chapters { get; init; } = chapters ?? [];

    public class Chapter(string name = "", string assetBundleName = "", Chapter.Episode[]? episodes = null)
    {
        public string Name { get; init; } = name;

        public string AssetBundleName { get; init; } = assetBundleName;

        public Episode[] Episodes { get; init; } = episodes ?? [];

        public class Episode(string episodeNoLabel, string title, string scenarioId)
        {
            private string EpisodeNoLabel { get; } = episodeNoLabel;
            private string Title { get; } = title;
            private string ScenarioId { get; } = scenarioId;

            public string Key => $"{EpisodeNoLabel} - {Title}";

            public string Url(string assetBundleName, SourceType sourceType = 0)
            {
                return sourceType switch
                {
                    SourceType.SiteBest =>
                        $"https://storage.sekai.best/sekai-jp-assets/scenario/unitstory/" +
                        $"{assetBundleName}_rip/{ScenarioId}.asset",
                    SourceType.SiteHaruki =>
                        $"https://storage.haruki.wacca.cn/assets/startapp/scenario/unitstory/" +
                        $"{assetBundleName}/{ScenarioId}.json",
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                };
            }
        }
    }
}