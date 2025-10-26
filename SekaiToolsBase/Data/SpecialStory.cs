namespace SekaiToolsBase.Data;

public class SpecialStoryEpisode : ICloneable
{
    public int Id { get; set; }
    public int SpecialStoryId { get; set; }
    public int EpisodeNo { get; set; }
    public string Title { get; set; } = "";
    public string SpecialStoryEpisodeType { get; set; } = "";
    public string AssetBundleName { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public bool IsAbleSkip { get; set; }
    public bool IsActionSetRefresh { get; set; }
    public int[] RewardResourceBoxIds { get; set; } = [];

    public object Clone()
    {
        return new SpecialStoryEpisode
        {
            Id = Id,
            SpecialStoryId = SpecialStoryId,
            EpisodeNo = EpisodeNo,
            Title = Title,
            SpecialStoryEpisodeType = SpecialStoryEpisodeType,
            AssetBundleName = AssetBundleName,
            ScenarioId = ScenarioId,
            ReleaseConditionId = ReleaseConditionId,
            IsAbleSkip = IsAbleSkip,
            IsActionSetRefresh = IsActionSetRefresh,
            RewardResourceBoxIds = RewardResourceBoxIds
        };
    }
}

public struct SpecialStory : ICloneable
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Title { get; set; }
    public string AssetBundleName { get; set; }
    public long StartAt { get; set; }
    public long EndAt { get; set; }
    public SpecialStoryEpisode[] Episodes { get; set; }


    public object Clone()
    {
        return new SpecialStory
        {
            Id = Id,
            Seq = Seq,
            Title = Title,
            AssetBundleName = AssetBundleName,
            StartAt = StartAt,
            EndAt = EndAt,
            Episodes = Episodes.Select(x => (SpecialStoryEpisode)x.Clone()).ToArray()
        };
    }
}