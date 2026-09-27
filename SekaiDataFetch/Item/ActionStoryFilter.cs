namespace SekaiDataFetch.Item;

public record ActionStoryFilter(int? AreaId = null, int CharacterId = 0, int? MinimumId = null, int? MaximumId = null)
{
    public bool Matches(AreaStorySet story)
    {
        return (!AreaId.HasValue || story.ActionSet.AreaId == AreaId)
               && (CharacterId == 0 || story.CharacterIds.Contains(CharacterId))
               && (!MinimumId.HasValue || story.ActionSet.Id >= MinimumId)
               && (!MaximumId.HasValue || story.ActionSet.Id <= MaximumId);
    }
}
