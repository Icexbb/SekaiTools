using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class ActionSet
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public bool IsNextGrade { get; set; }
    public string ScriptId { get; set; } = "";
    public int[] CharacterIds { get; set; } = [];
    public string ActionSetType { get; set; } = "";
    public long ArchivePublishedAt { get; set; }
    public int ReleaseConditionId { get; set; }


    public static ActionSet FromJson(JObject json)
    {
        return new ActionSet
        {
            Id = json.Get("id", 0),
            AreaId = json.Get("areaId", 0),
            IsNextGrade = json.Get("isNextGrade", false),
            ScriptId = json.Get("scriptId", ""),
            CharacterIds = json.Get("characterIds", Array.Empty<int>()),
            ActionSetType = json.Get("actionSetType", "normal"),
            ArchivePublishedAt = json.Get("archivePublishedAt", 0L),
            ReleaseConditionId = json.Get("releaseConditionId", 0),
        };
    }
}