using SekaiDataFetch.Item;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class ActionStoryMetadataTests
{
    private static ActionSet Action(int id, int condition = 1, string scenario = "areatalk_test_001") =>
        new() { Id = id, ReleaseConditionId = condition, ScenarioId = scenario, ActionSetType = "normal" };

    private static readonly EventStory[] EventStories =
    [
        new() { Id = 10, EventId = 99, EventStoryEpisodes = [new() { Id = 1000 }] },
        new() { Id = 20, EventId = 5, EventStoryEpisodes = [new() { Id = 2000 }] }
    ];
    private static readonly GameEvent[] Events = [new() { Id = 99, Name = "第一期" }, new() { Id = 5, Name = "第二期" }];
    private static readonly ReleaseCondition[] Conditions =
    [
        new() { Id = 500, ReleaseConditionType = "event_story", ReleaseConditionTypeId = 1000 },
        new() { Id = 600, ReleaseConditionType = "event_story", ReleaseConditionTypeId = 2000 }
    ];

    [Fact]
    public void ResolvesEpisodeToRealEventIdWithoutUsingConditionNumberFormula()
    {
        var actions = new[] { Action(10, 500), Action(20, 600) };
        var stories = actions.Select(action => new AreaStorySet(action)).ToArray();
        ActionStoryMetadata.Apply(stories, actions, Conditions, EventStories, Events);
        Assert.Equal(99, stories[0].ReleaseActivity!.Id);
        Assert.Equal(1, stories[0].ReleaseActivity!.Number);
        Assert.Equal(5, stories[1].ReleaseActivity!.Id);
        Assert.Equal(2, stories[1].ReleaseActivity!.Number);
        Assert.Contains("ID 99", stories[0].ReleaseActivity!.DisplayName);
    }

    [Fact]
    public void InfersAdditionsInIdOrderAndDoesNotRegressToAnOlderActivity()
    {
        var actions = new[] { Action(40, 500), Action(20, 500), Action(10), Action(30, 600), Action(35) };
        var stories = actions.Select(action => new AreaStorySet(action)).ToArray();
        ActionStoryMetadata.Apply(stories, actions, Conditions, EventStories, Events);
        Assert.Equal("initial", stories.Single(story => story.ActionSet.Id == 10).BatchKey);
        Assert.Equal(99, stories.Single(story => story.ActionSet.Id == 20).AdditionActivity!.Id);
        Assert.Equal(5, stories.Single(story => story.ActionSet.Id == 35).AdditionActivity!.Id);
        var oldUnlock = stories.Single(story => story.ActionSet.Id == 40);
        Assert.Equal(99, oldUnlock.ReleaseActivity!.Id);
        Assert.Equal(5, oldUnlock.AdditionActivity!.Id);
    }

    [Fact]
    public void SpecialBatchesDoNotChangeNormalAdditionInference()
    {
        var actions = new[] { Action(10, 500), Action(20, 600, "areatalk_monthly2609_001"), Action(30) };
        var stories = actions.Select(action => new AreaStorySet(action)).ToArray();
        ActionStoryMetadata.Apply(stories, actions, Conditions, EventStories, Events);
        Assert.Equal("monthly:2609", stories[1].BatchKey);
        Assert.Equal(5, stories[1].ReleaseActivity!.Id);
        Assert.Null(stories[1].AdditionActivity);
        Assert.Equal(99, stories[2].AdditionActivity!.Id);
    }

    [Fact]
    public void FollowsActionDependenciesAndHandlesCyclesOrMissingReferences()
    {
        var conditions = Conditions.Concat(new[]
        {
            new ReleaseCondition { Id = 700, ReleaseConditionType = "action_set", ReleaseConditionTypeId = 10 },
            new ReleaseCondition { Id = 800, ReleaseConditionType = "action_set", ReleaseConditionTypeId = 40 },
            new ReleaseCondition { Id = 900, ReleaseConditionType = "action_set", ReleaseConditionTypeId = 30 },
            new ReleaseCondition { Id = 901, ReleaseConditionType = "event_story", ReleaseConditionTypeId = 9999 }
        });
        var actions = new[] { Action(10, 500), Action(20, 700), Action(30, 800), Action(40, 900), Action(50, 901) };
        var stories = actions.Select(action => new AreaStorySet(action)).ToArray();
        ActionStoryMetadata.Apply(stories, actions, conditions, EventStories, Events);
        Assert.Equal(99, stories[1].ReleaseActivity!.Id);
        Assert.All(stories.Skip(2), story => Assert.Null(story.ReleaseActivity));
    }

    [Theory]
    [InlineData("areatalk_monthly2106_001", "normal", "monthly:2106")]
    [InlineData("areatalk_monthly2113_001", "normal", "other")]
    [InlineData("areatalk_aprilfool2022_001", "limited", "aprilfool:2022")]
    [InlineData("areatalk_3rdaniv_001", "normal", "anniversary:3")]
    [InlineData("unknown_future_story", "normal", "other")]
    [InlineData("unknown_limited", "limited", "limited")]
    public void ClassifiesKnownNamesAndRetainsUnknownNames(string scenario, string type, string key)
    {
        var action = Action(1, scenario: scenario);
        action.ActionSetType = type;
        Assert.Equal(key, ActionStoryMetadata.ClassifyBatch(action).Key);
    }

    [Fact]
    public void MissingMetadataClearsOldAssociationsButKeepsSpecialClassification()
    {
        var actions = new[] { Action(10, 500), Action(20, 600, "areatalk_monthly2609_001") };
        var stories = actions.Select(action => new AreaStorySet(action)).ToArray();
        ActionStoryMetadata.Apply(stories, actions, Conditions, EventStories, Events);
        ActionStoryMetadata.Apply(stories, actions, [], [], []);
        Assert.All(stories, story =>
        {
            Assert.Null(story.ReleaseActivity);
            Assert.Null(story.AdditionActivity);
            Assert.True(new ActionStoryFilter().Matches(story));
        });
        Assert.Equal("monthly:2609", stories[1].BatchKey);
        Assert.Equal("other", stories[0].BatchKey);
    }

    [Fact]
    public void MalformedMetadataIsRejectedBeforeChangingStoryAssociations()
    {
        var action = Action(10, 500);
        var story = new AreaStorySet(action) { ReleaseActivity = new(99, 1, "活动") };
        Assert.Throws<ArgumentException>(() => ActionStoryMetadata.Apply([story], [action], Conditions,
            [new EventStory { EventId = 99, EventStoryEpisodes = null! }], Events));
        Assert.Equal(99, story.ReleaseActivity!.Id);
    }

    [Fact]
    public void MetadataAndBaseFiltersIntersectAndUnknownEntriesRemainSelectable()
    {
        var story = new AreaStorySet(new ActionSet { Id = 10, AreaId = 2, ActionSetType = "normal",
            ArchivePublishedAt = new DateTimeOffset(2026, 9, 26, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() })
        {
            CharacterIds = [3], ReleaseActivity = new(99, 1, "活动"), BatchKey = "monthly:2609"
        };
        Assert.True(new ActionStoryFilter(2, 3, 10, 10, ActionStoryFilterMode.ReleaseActivity, "99").Matches(story));
        Assert.False(new ActionStoryFilter(1, Mode: ActionStoryFilterMode.ReleaseActivity, Value: "99").Matches(story));
        Assert.True(new ActionStoryFilter(Mode: ActionStoryFilterMode.AdditionActivity, Value: "unknown").Matches(story));
        Assert.True(new ActionStoryFilter(Mode: ActionStoryFilterMode.Batch, Value: "monthly:2609").Matches(story));
        Assert.True(new ActionStoryFilter(Mode: ActionStoryFilterMode.Type, Value: "normal").Matches(story));
        Assert.True(new ActionStoryFilter(Mode: ActionStoryFilterMode.ArchiveDate, Value: "2026-09-27").Matches(story));
        story.ActionSet.ArchivePublishedAt = long.MaxValue;
        Assert.Null(story.ArchiveDate);
    }
}
