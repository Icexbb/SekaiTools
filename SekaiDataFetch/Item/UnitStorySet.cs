using SekaiToolsBase.Data;

namespace SekaiDataFetch.Item;

public class UnitStorySet
{
    public UnitStorySet(UnitStory unitStory)
    {
        UnitStory = unitStory;
        Chapters = unitStory.Chapters.Select(chapter => new Chapter(chapter)).ToArray();
    }

    public UnitStory UnitStory { get; set; }
    public string Name => Constants.UnitName[UnitStory.Unit];

    public Chapter[] Chapters { get; init; }

    public class Chapter
    {
        public Chapter(UnitChapter chapter)
        {
            Name = chapter.Title;
            AssetBundleName = chapter.AssetBundleName;
            Episodes = chapter.Episodes.Select(episode => new Episode(episode)).ToArray();
        }

        public string Name { get; }

        public string AssetBundleName { get; }

        public Episode[] Episodes { get; }

        public class Episode(UnitEpisode episode)
        {
            private string EpisodeNoLabel { get; } = episode.EpisodeNoLabel;
            private string Title { get; } = episode.Title;
            public string ScenarioId { get; } = episode.ScenarioId;

            public string Key => $"{EpisodeNoLabel} - {Title}";
        }
    }
}