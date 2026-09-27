using SekaiToolsBase.DataList;

namespace SekaiDataFetch.Item;

public class AreaStorySet(ActionSet actionSet) : ICloneable
{
    public ActionSet ActionSet { get; } = actionSet;
    public string ScenarioId { get; } = actionSet.ScenarioId;
    public int Group { get; } = actionSet.Id / 100;

    public int[] CharacterIds { get; set; } = [];
    public string AreaName { get; set; } = "";
    public ActionStoryActivity? ReleaseActivity { get; set; }
    public ActionStoryActivity? AdditionActivity { get; set; }
    public string BatchKey { get; set; } = "other";
    public string BatchName { get; set; } = "其他更新／未归类";
    public string TypeName => ActionSet.ActionSetType switch
    {
        "normal" => "普通",
        "limited" => "限定",
        "" => "未分类",
        _ => $"其他（{ActionSet.ActionSetType}）"
    };
    public string? ArchiveDate => ActionSet.ArchivePublishedAt is > 0 and <= 253402268399999
        ? DateTimeOffset.FromUnixTimeMilliseconds(ActionSet.ArchivePublishedAt).ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd")
        : null;


    public object Clone()
    {
        return new AreaStorySet(ActionSet)
        {
            CharacterIds = CharacterIds,
            AreaName = AreaName,
            ReleaseActivity = ReleaseActivity,
            AdditionActivity = AdditionActivity,
            BatchKey = BatchKey,
            BatchName = BatchName
        };
    }
}
