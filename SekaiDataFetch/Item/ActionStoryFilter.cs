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

public enum ActionStoryIdIndex
{
    Id,
    TalkId
}

public record ActionStoryFilter(int? AreaId = null, int CharacterId = 0, int? MinimumId = null, int? MaximumId = null,
    ActionStoryFilterMode Mode = ActionStoryFilterMode.All, string? Value = null,
    ActionStoryIdIndex IdIndex = ActionStoryIdIndex.Id, bool SpecialTalkId = false)
{
    public bool Matches(AreaStorySet story)
    {
        return (!AreaId.HasValue || story.ActionSet.AreaId == AreaId)
               && (CharacterId == 0 || story.CharacterIds.Contains(CharacterId))
               && MatchesIdRange(story)
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
    private bool MatchesIdRange(AreaStorySet story)
    {
        if (!MinimumId.HasValue && !MaximumId.HasValue) return true;
        var id = story.ActionSet.Id;
        if (IdIndex == ActionStoryIdIndex.TalkId &&
            (!TryParseTalkId(story.TalkId, out id, out var special) || special != SpecialTalkId))
            return false;
        return (!MinimumId.HasValue || id >= MinimumId) && (!MaximumId.HasValue || id <= MaximumId);
    }

    public (int Kind, int Number) GetSortKey(AreaStorySet story)
    {
        if (IdIndex == ActionStoryIdIndex.Id) return (0, story.ActionSet.Id);
        return TryParseTalkId(story.TalkId, out var number, out var special)
            ? (special ? 1 : 0, number) : (2, story.ActionSet.Id);
    }

    public static bool TryParseTalkId(string? text, out int number, out bool special)
    {
        var value = text?.Trim() ?? "";
        special = value.StartsWith("S", StringComparison.OrdinalIgnoreCase);
        if (special) value = value[1..];
        number = 0;
        return value.Length > 0 && value.All(char.IsAsciiDigit) &&
               int.TryParse(value, System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture, out number) && number > 0;
    }
}
