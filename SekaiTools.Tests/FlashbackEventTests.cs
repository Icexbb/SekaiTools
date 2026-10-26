using System.Text.Json;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsBase.DataList;
using SekaiToolsBase.Story;

namespace SekaiTools.Tests;

public sealed class FlashbackEventTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SekaiToolsFlashbackTests", Guid.NewGuid().ToString("N"));
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] Files = ["events", "eventStories", "actionSets", "releaseConditions"];
    private static Dictionary<string, string> Data(int id = 135) => new()
    {
        ["events"] = JsonSerializer.Serialize(new[] { new GameEvent { Id = id, Name = "OVER RAD SQUAD!!" } }, JsonOptions),
        ["eventStories"] = JsonSerializer.Serialize(new[] { new EventStory { Id = id, EventId = id,
            EventStoryEpisodes = [new EventEpisode { Id = 1000, EpisodeNo = 1, ScenarioId = "event_135_01" },
                new EventEpisode { Id = 1001, EpisodeNo = 6, ScenarioId = "event_135_06" }] } }, JsonOptions),
        ["actionSets"] = JsonSerializer.Serialize(new[] {
            new ActionSet { Id = 1, ScenarioId = "areatalk_ev_street_18_001", ReleaseConditionId = 10 },
            new ActionSet { Id = 2, ScenarioId = "areatalk_ev_street_18_002", ReleaseConditionId = 11 }
        }, JsonOptions),
        ["releaseConditions"] = JsonSerializer.Serialize(new[] {
            new ReleaseCondition { Id = 10, ReleaseConditionType = "event_story", ReleaseConditionTypeId = 1000 },
            new ReleaseCondition { Id = 11, ReleaseConditionType = "action_set", ReleaseConditionTypeId = 1 }
        }, JsonOptions)
    };

    private static SourceData Source(string name) => new()
    {
        SourceName = name,
        SourceTemplate = $"https://{name}.example/{{type}}.json",
        StorageBaseUrl = "https://assets.example/",
        ActionSetTemplate = "",
        MemberStoryTemplate = "",
        EventStoryTemplate = "",
        SpecialStoryTemplate = "",
        UnitStoryTemplate = ""
    };
    private string Cache(SourceData source) => Path.Combine(_directory, source.SourceName);
    private async Task Seed(SourceData source, Dictionary<string, string> data)
    {
        Directory.CreateDirectory(Cache(source));
        foreach (var pair in data) await File.WriteAllTextAsync(Path.Combine(Cache(source), (pair.Key == "events" ? "gameEvents" : pair.Key) + ".json"), pair.Value);
    }

    [Theory]
    [InlineData("ev_street_18_06", "street_18")]
    [InlineData("sc_ev_street_18_06", "street_18")]
    [InlineData("card_ev_street_18_09_4a", "street_18")]
    [InlineData("ev_street_18_09_4a", "street_18")]
    [InlineData("ev_wl_band_01_05", "wl_band_01")]
    [InlineData("ev_night__06", "night_")]
    [InlineData("ms_night13", null)]
    [InlineData("card_18_3a", null)]
    public void 来源前缀区分剧情类型(string source, string? expected) => Assert.Equal(expected, FlashbackEventCatalog.GetEventKey(source));

    [Fact]
    public async Task 完整缓存直接显示真实期数而非团内序号且不联网()
    {
        var source = Source("cache");
        await Seed(source, Data());
        var service = new FlashbackEventService((_, _) => throw new Exception("不应联网"), Cache);
        var result = await service.ResolveAsync(["ev_street_18_06", "card_ev_street_18_09_4a"], [source],
            loadAdditionalSources: _ => throw new Exception("不应获取源列表"));
        Assert.Equal("第 135 期 · OVER RAD SQUAD!! · 第 6 话", result["ev_street_18_06"].DisplayName);
        Assert.Equal(135, result["card_ev_street_18_09_4a"].EventId);
        Assert.Null(result["card_ev_street_18_09_4a"].EpisodeNumber);
    }

    [Fact]
    public async Task 优先读取后续源的缓存而非刷新首个源()
    {
        await Seed(Source("second"), Data());
        var result = await new FlashbackEventService((_, _) => throw new Exception("不应联网"), Cache)
            .ResolveAsync(["ev_street_18_06"], [Source("first"), Source("second")]);
        Assert.Equal(135, result["ev_street_18_06"].Number);
    }

    [Fact]
    public async Task 相同主数据地址的不同源名称仍检查各自缓存()
    {
        var second = Source("second");
        var alias = JsonSerializer.Deserialize<SourceData>(JsonSerializer.Serialize(second).Replace("\"second\"", "\"alias\""))!;
        await Seed(second, Data());
        var result = await new FlashbackEventService((_, _) => throw new Exception("不应联网"), Cache)
            .ResolveAsync(["ev_street_18_06"], [alias, second]);
        Assert.Equal(135, result["ev_street_18_06"].Number);
    }

    [Fact]
    public async Task 损坏的缓存和下载数据不会阻止后续源或覆盖有效文件()
    {
        var first = Source("first");
        await Seed(first, Data());
        var eventsPath = Path.Combine(Cache(first), "gameEvents.json");
        var eventsBefore = await File.ReadAllTextAsync(eventsPath);
        await File.WriteAllTextAsync(Path.Combine(Cache(first), "actionSets.json"), "invalid json");
        var data = Data();
        var service = new FlashbackEventService((url, _) => Task.FromResult(url.Contains("first.example")
            ? "[{}]" : data[Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath)]), Cache);
        var result = await service.ResolveAsync(["ev_street_18_06"], [first, Source("second")]);
        Assert.Equal(135, result["ev_street_18_06"].Number);
        Assert.Equal(eventsBefore, await File.ReadAllTextAsync(eventsPath));
    }

    [Fact]
    public async Task 缓存不足或源失败依次尝试后续源并持久化成功数据()
    {
        var calls = new List<string>();
        var data = Data();
        var service = new FlashbackEventService((url, _) =>
        {
            calls.Add(url);
            if (url.Contains("first.example")) throw new HttpRequestException("unavailable");
            return Task.FromResult(data[Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath)]);
        }, Cache);
        var result = await service.ResolveAsync(["ev_street_18_06"], [Source("first"), Source("second"), Source("third")]);
        Assert.Equal(135, result["ev_street_18_06"].Number);
        Assert.Equal(Files.Select(file => $"https://first.example/{file}.json")
            .Concat(Files.Select(file => $"https://second.example/{file}.json")), calls);
        Assert.True(File.Exists(Path.Combine(Cache(Source("second")), "gameEvents.json")));
        calls.Clear();
        await service.ResolveAsync(["ev_street_18_06"], [Source("second")]);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task 已下载但无法解析仍继续下个源且加载额外源列表()
    {
        var data = Data();
        var calls = new List<string>();
        var service = new FlashbackEventService((url, _) =>
        {
            calls.Add(url);
            return Task.FromResult(url.Contains("first.example") ? "[]" : data[Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath)]);
        }, Cache);
        var result = await service.ResolveAsync(["ev_street_18_06"], [Source("first")],
            loadAdditionalSources: _ => Task.FromResult(new[] { Source("second") }));
        Assert.Equal(135, result["ev_street_18_06"].Number);
        Assert.Equal(8, calls.Count);
    }

    [Fact]
    public async Task 非活动来源不联网且取消不会继续下个源()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new FlashbackEventService((_, token) =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromCanceled<string>(token);
        }, Cache);
        Assert.Empty(await service.ResolveAsync(["ms_night13"], [Source("first")]));
        Assert.Equal(0, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ResolveAsync(["ev_street_18_06"],
            [Source("first"), Source("second")], cancellation.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void 解锁循环安全处理且WorldLink和早期例外可解析()
    {
        var catalog = new FlashbackEventCatalog([
            new GameEvent { Id = 1, Name = "初期活动" }, new GameEvent { Id = 53, Name = "空序号活动" },
            new GameEvent { Id = 170, Name = "World Link" }], [
            new EventStory { Id = 1, EventId = 1 }, new EventStory { Id = 53, EventId = 53 },
            new EventStory { Id = 170, EventId = 170, EventStoryEpisodes = [new EventEpisode { Id = 1000, ScenarioId = "wl_night_02_01" }] }],
            [new ActionSet { Id = 1, ReleaseConditionId = 10, ScenarioId = "areatalk_ev_loop_01_001" }],
            [new ReleaseCondition { Id = 10, ReleaseConditionType = "action_set", ReleaseConditionTypeId = 1 }]);
        Assert.Equal(1, catalog.Resolve("ev_band_01_01")!.Number);
        Assert.Equal(53, catalog.Resolve("ev_night__06")!.Number);
        Assert.Equal(170, catalog.Resolve("ev_wl_night_02_01")!.Number);
        Assert.Null(catalog.Resolve("ev_loop_01_01"));
    }

    [Fact]
    public void 话数按章节主数据校验并处理零起始和WorldLink()
    {
        var catalog = new FlashbackEventCatalog([
            new GameEvent { Id = 9, Name = "新年活动" }, new GameEvent { Id = 137, Name = "World Link" }], [
            new EventStory { Id = 9, EventId = 9, EventStoryEpisodes = [
                new EventEpisode { EpisodeNo = 1 }, new EventEpisode { EpisodeNo = 2 }] },
            new EventStory { Id = 137, EventId = 137, EventStoryEpisodes = [
                new EventEpisode { EpisodeNo = 5, ScenarioId = "wl_band_01_05" }] }], [], []);
        Assert.Equal(1, catalog.Resolve("ev_shuffle_03_00")!.EpisodeNumber);
        Assert.Equal(2, catalog.Resolve("sc_ev_shuffle_03_01")!.EpisodeNumber);
        Assert.Equal(5, catalog.Resolve("ev_wl_band_01_05")!.EpisodeNumber);
        Assert.Null(catalog.Resolve("ev_shuffle_03_99")!.EpisodeNumber);
        Assert.Null(catalog.Resolve("ev_shuffle_03_2147483647")!.EpisodeNumber);
        Assert.Null(catalog.Resolve("ev_shuffle_03_unknown")!.EpisodeNumber);
        Assert.Null(catalog.Resolve("ev_shuffle_03_09_4a")!.EpisodeNumber);
        Assert.Null(catalog.Resolve("card_ev_shuffle_03_09_1")!.EpisodeNumber);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
