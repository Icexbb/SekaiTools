using System.Text.Json;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class WorldBloomChapterTests
{
    [Fact]
    public void GetBannerGameCharacterIdsReturnsDistinctCharactersInChapterOrder()
    {
        WorldBloomChapter[] chapters =
        [
            new() { EventId = 202, GameCharacterId = 14, WorldBloomChapterType = "game_character", ChapterNo = 3 },
            new() { EventId = 202, GameCharacterId = 6, WorldBloomChapterType = "game_character", ChapterNo = 1 },
            new() { EventId = 202, GameCharacterId = 1, WorldBloomChapterType = "game_character", ChapterNo = 2 },
            new() { EventId = 202, GameCharacterId = 17, WorldBloomChapterType = "game_character", ChapterNo = 4 },
            new() { EventId = 202, GameCharacterId = 21, WorldBloomChapterType = "game_character", ChapterNo = 5 },
            new() { EventId = 202, GameCharacterId = 6, WorldBloomChapterType = "game_character", ChapterNo = 6 },
            new() { EventId = 202, GameCharacterId = null, WorldBloomChapterType = "finale", ChapterNo = 7 },
            new() { EventId = 201, GameCharacterId = 17, WorldBloomChapterType = "game_character", ChapterNo = 1 }
        ];

        var result = WorldBloomChapter.GetBannerGameCharacterIds(chapters, 202);

        Assert.Equal([6, 1, 14, 17, 21], result);
    }

    [Fact]
    public void EventStoryAllowsNullBannerGameCharacterUnitId()
    {
        const string json = """
                            {
                              "id": 202,
                              "eventId": 202,
                              "bannerGameCharacterUnitId": null,
                              "assetbundleName": "event_wl_3rd_part1_2026"
                            }
                            """;

        var story = JsonSerializer.Deserialize<EventStory>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(story);
        Assert.Null(story.BannerGameCharacterUnitId);
    }
}
