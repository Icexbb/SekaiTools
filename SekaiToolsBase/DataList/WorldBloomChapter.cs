namespace SekaiToolsBase.DataList;

public class WorldBloomChapter
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int? GameCharacterId { get; set; }
    public string WorldBloomChapterType { get; set; } = "";
    public int ChapterNo { get; set; }
    public long ChapterStartAt { get; set; }
    public long AggregateAt { get; set; }
    public long ChapterEndAt { get; set; }
    public bool IsSupplemental { get; set; }

    public static int[] GetBannerGameCharacterIds(IEnumerable<WorldBloomChapter> chapters, int eventId)
    {
        return chapters
            .Where(chapter => chapter.EventId == eventId &&
                              chapter.WorldBloomChapterType == "game_character" &&
                              chapter.GameCharacterId.HasValue)
            .OrderBy(chapter => chapter.ChapterNo)
            .Select(chapter => chapter.GameCharacterId!.Value)
            .Distinct()
            .ToArray();
    }
}
