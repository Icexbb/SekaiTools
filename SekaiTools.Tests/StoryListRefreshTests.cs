using System.Reflection;
using System.Runtime.CompilerServices;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class StoryListRefreshTests
{
    [Fact]
    public void CardStoriesReloadReplacesPreviousEntries()
    {
        // Bypass the singleton constructor so tests do not load the user's cache.
        var list = (ListCardStory)RuntimeHelpers.GetUninitializedObject(typeof(ListCardStory));
        typeof(ListCardStory).GetField(nameof(ListCardStory.Data))!.SetValue(list, new List<CardStorySet>());

        void Reload(params int[] ids)
        {
            var cards = ids.Select(id => new Card { Id = id }).ToArray();
            var episodes = ids.SelectMany(id => new[]
            {
                new CardEpisode { CardId = id, CardEpisodePartType = "first_part" },
                new CardEpisode { CardId = id, CardEpisodePartType = "second_part" }
            }).ToArray();
            InvokeGetData(list, episodes, cards);
        }

        Reload(2, 1);
        Reload(2, 1);
        Assert.Equal(new[] { 1, 2 }, list.Data.Select(story => story.Card.Id));
        Reload(3);
        Assert.Equal(3, Assert.Single(list.Data).Card.Id);
        Reload();
        Assert.Empty(list.Data);
    }

    [Fact]
    public void ActionStoriesReloadReplacesPreviousEntriesAndFilters()
    {
        var list = (ListActionStory)RuntimeHelpers.GetUninitializedObject(typeof(ListActionStory));
        list.Data = [];

        void Reload(int id, int characterId)
        {
            InvokeGetData(list, new[]
            {
                new ActionSet { Id = id, AreaId = id, ScenarioId = "story", CharacterIds = [10] },
                new ActionSet { Id = 99, AreaId = id },
                new ActionSet { Id = 100, AreaId = -1, ScenarioId = "missing-area" }
            }, new[] { new Area { Id = id } },
                new[] { new Character2d { Id = 10, CharacterId = characterId } });
        }

        Reload(1, 20);
        Reload(1, 20);
        Assert.Equal(1, Assert.Single(list.Data).ActionSet.Id);
        Reload(2, 30);
        Assert.Equal(2, Assert.Single(list.Data).ActionSet.Id);
        Assert.Equal(new[] { 30 }, list.Data[0].CharacterIds);
        Assert.Equal(2, Assert.Single(list.Areas).Id);
        Assert.Equal(30, Assert.Single(list.Character2ds).CharacterId);
        InvokeGetData(list, Array.Empty<ActionSet>(), Array.Empty<Area>(), Array.Empty<Character2d>());
        Assert.Empty(list.Data);
        Assert.Empty(list.Areas);
        Assert.Empty(list.Character2ds);
    }

    [Fact]
    public void ActionTalkIdsMatchGlobalNormalAndSpecialSequencesBeforeAreaFiltering()
    {
        var list = (ListActionStory)RuntimeHelpers.GetUninitializedObject(typeof(ListActionStory));
        list.Data = [];
        var actions = new[]
        {
            new ActionSet { Id = 2, AreaId = 1, ScenarioId = "op_02area" },
            new ActionSet { Id = 11, AreaId = -1, ScenarioId = "normal", ActionSetType = "normal" },
            new ActionSet { Id = 20, AreaId = 1, ScenarioId = "areatalk_monthly2106_001", ActionSetType = "normal" },
            new ActionSet { Id = 30, AreaId = -1, ScenarioId = "limited", ActionSetType = "limited" },
            new ActionSet { Id = 40, AreaId = 1, ScenarioId = "piapro", ActionSetType = "piapro" },
            new ActionSet { Id = 50, AreaId = 1, ScenarioId = "areatalk_3rdaniv_001", ActionSetType = "normal" },
            new ActionSet { Id = 60, AreaId = 1, ScenarioId = "none", ActionSetType = "normal" },
            new ActionSet { Id = 70, AreaId = 1, ScenarioId = "last", ActionSetType = "normal" }
        };

        void Reload() => InvokeGetData(list, actions, new[] { new Area { Id = 1 } }, Array.Empty<Character2d>());
        Reload();
        Reload();

        Assert.Equal(new string?[] { null, "0002", "S0002", "0003", null, "0004" },
            list.Data.Select(story => story.TalkId));
        Assert.Equal(new[] { 2, 20, 40, 50, 60, 70 }, list.Data.Select(story => story.ActionSet.Id));
        Assert.Equal("S0002", ((AreaStorySet)list.Data[2].Clone()).TalkId);
    }

    private static void InvokeGetData(object list, params object[] arguments)
    {
        list.GetType().GetMethod("GetData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(list, arguments);
    }
}
