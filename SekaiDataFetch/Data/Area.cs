namespace SekaiDataFetch.Data;

public class Area : ICloneable
{
    public int Id { get; set; }
    public string AssetBundleName { get; set; } = "";
    public int GroupId { get; set; }
    public bool IsBaseArea { get; set; }
    public string AreaType { get; set; } = "";
    public string ViewType { get; set; } = "";
    public string DisplayTimelineType { get; set; } = "";
    public string AdditionalAreaType { get; set; } = "";
    public string Name { get; set; } = "";
    public int ReleaseConditionId { get; set; }

    public object Clone()
    {
        return new Area
        {
            Id = Id,
            AssetBundleName = AssetBundleName,
            GroupId = GroupId,
            IsBaseArea = IsBaseArea,
            AreaType = AreaType,
            ViewType = ViewType,
            DisplayTimelineType = DisplayTimelineType,
            AdditionalAreaType = AdditionalAreaType,
            Name = Name,
            ReleaseConditionId = ReleaseConditionId
        };
    }
}