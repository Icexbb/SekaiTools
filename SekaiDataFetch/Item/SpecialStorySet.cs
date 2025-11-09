using SekaiToolsBase.DataList;

namespace SekaiDataFetch.Item;

public class SpecialStorySet
{
    public SpecialStorySet(SpecialStory specialStory)
    {
        SpecialStory = specialStory;
        Episodes = specialStory.Episodes.Select(episode => new Episode(this, episode)).ToArray();
    }

    private SpecialStory SpecialStory { get; }

    public Episode[] Episodes { get; }

    public string AssetBundleName => SpecialStory.AssetBundleName;

    public class Episode(SpecialStorySet parent, SpecialStoryEpisode episode)
    {
        private SpecialStoryEpisode StoryEpisode { get; } = episode;
        public SpecialStorySet Parent { get; } = parent;
        public string Title => StoryEpisode.Title;
        public string ScenarioId => StoryEpisode.ScenarioId;
        public string AssetBundleName => StoryEpisode.AssetBundleName;
    }
}