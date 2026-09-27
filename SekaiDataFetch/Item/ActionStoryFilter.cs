namespace SekaiDataFetch.Item;

public enum ActionStoryFilterMode
{
    All,
    ReleaseActivity,
    AdditionActivity,
    Type,
    Batch,
    ArchiveDate
}

public record ActionStoryFilter(int? AreaId = null, int CharacterId = 0, int? MinimumId = null, int? MaximumId = null,
    ActionStoryFilterMode Mode = ActionStoryFilterMode.All, string? Value = null)
{
    public bool Matches(AreaStorySet story)
    {
        return (!AreaId.HasValue || story.ActionSet.AreaId == AreaId)
               && (CharacterId == 0 || story.CharacterIds.Contains(CharacterId))
               && (!MinimumId.HasValue || story.ActionSet.Id >= MinimumId)
               && (!MaximumId.HasValue || story.ActionSet.Id <= MaximumId)
               && (Value == null || Mode switch
               {
                   ActionStoryFilterMode.ReleaseActivity => (story.ReleaseActivity?.Id.ToString() ?? "unknown") == Value,
                   ActionStoryFilterMode.AdditionActivity => (story.AdditionActivity?.Id.ToString() ?? "unknown") == Value,
                   ActionStoryFilterMode.Type => story.ActionSet.ActionSetType == Value,
                   ActionStoryFilterMode.Batch => story.BatchKey == Value,
                   ActionStoryFilterMode.ArchiveDate => (story.ArchiveDate ?? "unknown") == Value,
                   _ => true
               });
    }
}
