namespace SekaiDataFetch.Data;

public class UnitEpisode : ICloneable
{
    public int Id { get; set; }
    public int UnitStoryEpisodeGroupId { get; set; }
    public int ChapterNo { get; set; }
    public int EpisodeNo { get; set; }
    public string EpisodeNoLabel { get; set; } = "";
    public string Title { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public int[] RewardResourceBoxIds { get; set; } = [];

    public object Clone()
    {
        return new UnitEpisode
        {
            Id = Id,
            UnitStoryEpisodeGroupId = UnitStoryEpisodeGroupId,
            ChapterNo = ChapterNo,
            EpisodeNo = EpisodeNo,
            EpisodeNoLabel = EpisodeNoLabel,
            Title = Title,
            AssetbundleName = AssetbundleName,
            ScenarioId = ScenarioId,
            ReleaseConditionId = ReleaseConditionId,
            RewardResourceBoxIds = RewardResourceBoxIds
        };
    }
}

public struct UnitChapter : ICloneable
{
    public int Id { set; get; }
    public string Unit { set; get; }
    public int ChapterNo { set; get; }
    public string Title { set; get; }
    public string AssetBundleName { set; get; }
    public UnitEpisode[] Episodes { set; get; }

    public object Clone()
    {
        return new UnitChapter
        {
            Id = Id,
            Unit = Unit,
            ChapterNo = ChapterNo,
            Title = Title,
            AssetBundleName = AssetBundleName,
            Episodes = Episodes.Select(x => (UnitEpisode)x.Clone()).ToArray()
        };
    }
}

public struct UnitStory : ICloneable
{
    public string Unit { get; set; }
    public int Seq { get; set; }
    public UnitChapter[] Chapters { get; set; }


    public object Clone()
    {
        return new UnitStory
        {
            Unit = Unit,
            Seq = Seq,
            Chapters = Chapters.Select(x => (UnitChapter)x.Clone()).ToArray()
        };
    }
}