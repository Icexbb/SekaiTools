namespace SekaiToolsBase.DataList;

public class ActionSet : ICloneable
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public bool IsNextGrade { get; set; }
    public string ScenarioId { get; set; } = "";
    public string ScriptId { get; set; } = "";
    public int[] CharacterIds { get; set; } = [];
    public string ActionSetType { get; set; } = "";
    public long ArchivePublishedAt { get; set; }
    public int ReleaseConditionId { get; set; }


    public object Clone()
    {
        return new ActionSet
        {
            Id = Id,
            AreaId = AreaId,
            IsNextGrade = IsNextGrade,
            ScenarioId = ScenarioId,
            ScriptId = ScriptId,
            CharacterIds = CharacterIds,
            ActionSetType = ActionSetType,
            ArchivePublishedAt = ArchivePublishedAt,
            ReleaseConditionId = ReleaseConditionId
        };
    }
}