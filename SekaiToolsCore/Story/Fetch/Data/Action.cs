using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Fetch.Data;

public struct Action
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public bool IsNextGrade { get; set; }
    public string ScriptId { get; set; }
    public List<int> CharacterIds { get; set; }
    public string ArchiveDisplayType { get; set; }
    public long ArchivePublishedAt { get; set; }
    public int ReleaseConditionId { get; set; }


    public static Action FromJson(JObject json)
    {
        return new Action
        {
            Id = json["id"]!.ToObject<int>(),
            AreaId = json["areaId"]!.ToObject<int>(),
            IsNextGrade = json["isNextGrade"]!.ToObject<bool>(),
            ScriptId = json["scriptId"]!.ToObject<string>()!,
            CharacterIds = json["characterIds"]!.ToObject<int[]>()?.ToList() ?? new List<int>(),
            ArchiveDisplayType = json["archiveDisplayType"]!.ToObject<string>()!,
            ArchivePublishedAt = json["archivePublishedAt"]!.ToObject<long>(),
            ReleaseConditionId = json["releaseConditionId"]!.ToObject<int>()
        };
    }
}