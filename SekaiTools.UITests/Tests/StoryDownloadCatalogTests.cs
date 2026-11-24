using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using SekaiToolsBase.DataList;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class StoryDownloadCatalogTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DOWNLOAD_002_按分类浏览离线目录并验证地图活动关联()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-DOWNLOAD-002", "Functional");
        var config = Path.Combine(app.Data.Root, "network-endpoints.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
        using var endpoints = JsonDocument.Parse(File.ReadAllText(config));
        var sources = JsonNode.Parse(endpoints.RootElement.GetProperty("defaultDataSources").GetRawText())!.AsArray();
        var alternate = sources[0]!.DeepClone();
        alternate["SourceName"] = "UI Alternate Fixture";
        alternate["SourceTemplate"] = $"http://127.0.0.1:{server.Port}/alternate/{{type}}.json";
        sources.Add(alternate);
        var sourceResponse = sources.ToJsonString();
        var missingActivityMetadata = 0;
        var failAlternate = 1;
        var catalog = new Dictionary<string, string>
        {
            ["eventStories"] = JsonSerializer.Serialize(new[] { new EventStory
            {
                Id = 1, EventId = 1, AssetBundleName = "event-bundle",
                EventStoryEpisodes =
                [
                    new() { Id = 10, EventStoryId = 1, EpisodeNo = 1, Title = "Fixture Event Chapter", ScenarioId = "event-fixture-main" },
                    .. Enumerable.Range(10, 4).Select(episode => new EventEpisode
                    {
                        Id = 100 + episode, EventStoryId = 1, EpisodeNo = episode, ScenarioId = $"event-fixture-{episode}",
                        Title = $"Fixture Event Chapter {episode} ワンダーランズ×ショウタイム"
                    })
                ]
            } }),
            ["events"] = JsonSerializer.Serialize(new[] { new GameEvent
            {
                Id = 1, Name = "Fixture Activity", AssetBundleName = "event-bundle", EventType = "marathon"
            } }),
            ["worldBlooms"] = "[]",
            ["specialStories"] = JsonSerializer.Serialize(new[] { new SpecialStory
            {
                Id = 2, Seq = 1, Title = "Fixture Special", AssetBundleName = "special-bundle",
                Episodes = [new() { Id = 20, SpecialStoryId = 2, EpisodeNo = 1, Title = "Fixture Special Chapter", ScenarioId = "special-fixture" }]
            } }),
            ["cards"] = JsonSerializer.Serialize(new[] { new Card { Id = 3, CharacterId = 1, Prefix = "Fixture Card", AssetbundleName = "card-bundle" } }),
            ["cardEpisodes"] = JsonSerializer.Serialize(new[]
            {
                new CardEpisode { Id = 30, CardId = 3, Title = "Fixture First Part", ScenarioId = "card-fixture-1", CardEpisodePartType = "first_part" },
                new CardEpisode { Id = 31, CardId = 3, Title = "Fixture Second Part", ScenarioId = "card-fixture-2", CardEpisodePartType = "second_part" }
            }),
            ["actionSets"] = JsonSerializer.Serialize(new[] { new ActionSet
            {
                Id = 4, AreaId = 1, ScenarioId = "action-fixture", ScriptId = "action-fixture",
                ActionSetType = "normal", ReleaseConditionId = 40
            } }),
            ["areas"] = JsonSerializer.Serialize(new[] { new Area { Id = 1, AreaName = "Fixture Area" } }),
            ["character2ds"] = "[]",
            ["releaseConditions"] = JsonSerializer.Serialize(new[] { new ReleaseCondition
            {
                Id = 40, ReleaseConditionType = "event_story", ReleaseConditionTypeId = 10
            } })
        };
        server.Response = request =>
        {
            if (request.Contains("/sources.json ")) return sourceResponse;
            var path = request.Split(' ')[1];
            var resource = Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith("/alternate/", StringComparison.Ordinal))
            {
                if (Volatile.Read(ref failAlternate) != 0) return null;
                return resource == "actionSets"
                    ? catalog[resource].Replace("action-fixture", "alternate-fixture", StringComparison.Ordinal)
                    : catalog.GetValueOrDefault(resource);
            }
            if (!path.StartsWith("/master/", StringComparison.Ordinal)) return null;
            if (resource == "releaseConditions" && Volatile.Read(ref missingActivityMetadata) != 0) return null;
            return catalog.GetValueOrDefault(resource);
        };
        app.Step("打开下载页", "只访问本机剧情样本服务", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.StoryDownload", "StoryDownload.Root");
            WaitHelper.Until(() => server.Requests.Any(request => request.Contains("/sources.json ")), "收到数据源请求");
        });
        foreach (var (category, scenario, displayedName, choiceLabel) in new[]
                 {
                     ("Event", "event-fixture-main", "Fixture Activity", "Fixture Event Chapter"),
                     ("Special", "special-fixture", "Fixture Special Chapter", "Fixture Special Chapter"),
                     ("Card", "card-fixture-2", "# 3", "Fixture Card|后篇"),
                     ("Action", "action-fixture", "action-fixture", "action-fixture")
                 })
        {
            app.Step(category + " 剧情", "按剧本名搜索并可选择加入下载列表", () =>
            {
                var search = app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox();
                search.Text = "";
                app.Finder.ById(app.Window!, "StoryDownload.Category." + category).AsListBoxItem().Select();
                app.Finder.ById(app.Window!, "StoryDownload." + category + ".Root");
                Refresh();
                WaitForText(displayedName);
                if (category == "Event")
                {
                    search.Text = "event-fixture";
                    var eventRoot = app.Finder.ById(app.Window!, "StoryDownload.Event.Root");
                    WaitHelper.Until(() =>
                    {
                        var titles = eventRoot.FindAllDescendants(cf => cf.ByAutomationId("TitleTextBlock"))
                            .Where(element => element.Name.StartsWith("Fixture Event Chapter", StringComparison.Ordinal))
                            .Select(element => element.BoundingRectangle.Width).ToArray();
                        return titles.Length == 5 && titles.Min() > 0 && titles.Max() - titles.Min() <= 2;
                    }, "活动章节标题等宽，容纳不同位数编号");
                    ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.EventAligned");
                }
                search.Text = scenario;
                var choiceId = FindChoice(category, choiceLabel).AutomationId;
                var select = app.Finder.ById(app.Window!, "StoryDownload.SelectResults").AsCheckBox();
                WaitHelper.Until(() => select.IsEnabled, category + " 样本可选");
                select.IsChecked = true;
                var add = app.Finder.ById(app.Window!, "StoryDownload.AddSelectedButton");
                WaitHelper.Until(() => add.IsEnabled, category + " 所选剧本可下载");
                add.AsButton().Invoke();
                WaitHelper.Until(() => app.Finder.ById(app.Window!, "StoryDownload.StartDownloadButton").IsEnabled, category + " 已入队");
                Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByName("批量加入完成")));
                Assert.Equal(choiceId, FindChoice(category, choiceLabel).AutomationId);
                if (category == "Action")
                {
                    WaitHelper.Until(() => app.Window!.FindAllDescendants().Any(element =>
                        element.Name?.Contains("解锁：第 1 期 · Fixture Activity（ID 1）", StringComparison.Ordinal) == true), "地图对话展示关联活动");
                    OpenActionFilters();
                    var mode = app.Finder.ById(app.Window!, "ModeBox").AsComboBox();
                    mode.Select(1);
                    mode.Collapse();
                    var activities = app.Finder.ById(app.Window!, "ValueBox").AsComboBox();
                    WaitHelper.Until(() => activities.Items.Length > 1, "活动筛选包括关联活动");
                    activities.Select(1);
                    activities.Collapse();
                    WaitForText("action-fixture");
                }
                ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download." + category);
                app.Finder.ById(app.Window!, "StoryDownload.ClearQueueButton").AsButton().Invoke();
            });
        }
        app.Step("缺少活动元数据", "清缓存后请求失败，地图对话仍可选且显示降级提示", () =>
        {
            Volatile.Write(ref missingActivityMetadata, 1);
            app.Finder.ById(app.Window!, "StoryDownload.ClearCache").AsButton().Invoke();
            Refresh();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("action-fixture")), "地图对话仍显示");
            OpenActionFilters();
            WaitHelper.Until(() => app.Window!.FindAllDescendants().Any(element =>
                element.Name?.Contains("活动数据未就绪或不可用", StringComparison.Ordinal) == true), "显示活动元数据降级说明");
            Assert.True(app.Finder.ById(app.Window!, "StoryDownload.SelectResults").IsEnabled);
        });
        app.Step("切换数据源并重试刷新", "缓存按来源隔离，刷新失败后可重试并恢复浏览", () =>
        {
            var source = app.Finder.ById(app.Window!, "StoryDownload.Source").AsComboBox();
            app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox().Text = "";
            var firstChoiceId = FindChoice("Action", "action-fixture").AutomationId;
            source.Select(1);
            source.Collapse();
            WaitHelper.Until(() => !app.Finder.ById(app.Window!, "StoryDownload.EmptyMessage").IsOffscreen, "备用源无缓存");
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByName("action-fixture")));
            Refresh();
            WaitForText("刷新失败");
            Assert.Contains(server.Requests, request => request.Contains("/alternate/actionSets.json "));
            Volatile.Write(ref failAlternate, 0);
            Refresh();
            WaitForText("alternate-fixture");
            var alternateChoiceId = FindChoice("Action", "alternate-fixture").AutomationId;
            Assert.NotEqual(firstChoiceId, alternateChoiceId);
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByName("action-fixture")));
            source.Select(0);
            source.Collapse();
            WaitForText("action-fixture");
            Assert.Equal(firstChoiceId, FindChoice("Action", "action-fixture").AutomationId);
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByName("alternate-fixture")));
            source.Select(1);
            source.Collapse();
            WaitForText("alternate-fixture");
            Assert.Equal(alternateChoiceId, FindChoice("Action", "alternate-fixture").AutomationId);
            var cache = Path.Combine(app.Data.Paths.DataRoot, "Data", "cache");
            Assert.True(File.Exists(Path.Combine(cache, "UI Test Fixture", "actionSets.json")));
            Assert.True(File.Exists(Path.Combine(cache, "UI Alternate Fixture", "actionSets.json")));
            app.Finder.ById(app.Window!, "StoryDownload.ClearCache").AsButton().Invoke();
            WaitHelper.Until(() => !app.Finder.ById(app.Window!, "StoryDownload.EmptyMessage").IsOffscreen, "清除当前源缓存");
            WaitHelper.Until(() => source.IsEnabled, "清除缓存后可切换来源");
            Assert.Empty(Directory.EnumerateFiles(cache, "*.json", SearchOption.AllDirectories));
            source.Select(0);
            source.Collapse();
            WaitHelper.Until(() => !app.Finder.ById(app.Window!, "StoryDownload.EmptyMessage").IsOffscreen, "第一来源缓存也已清除");
        });
        app.Step("退出", "正常退出", () =>
        {
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
            app.Close();
        });

        void Refresh()
        {
            var refresh = app.Finder.ById(app.Window!, "StoryDownload.RefreshButton");
            WaitHelper.Until(() => refresh.IsEnabled, "刷新入口可用");
            var previousRequests = server.Requests.Count;
            refresh.AsButton().Invoke();
            WaitHelper.Until(() => server.Requests.Count > previousRequests, "刷新已向数据源发出请求");
            WaitHelper.Until(() => refresh.IsEnabled, "刷新结束");
        }

        void WaitForText(string text) =>
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName(text)), text);

        AutomationElement FindChoice(string category, string label)
        {
            var categoryRoot = app.Finder.ById(app.Window!, "StoryDownload." + category + ".Root");
            return WaitHelper.Until(() => categoryRoot.FindAllDescendants().FirstOrDefault(element =>
                element.AutomationId?.StartsWith($"StoryDownload.Choice.{category}.", StringComparison.Ordinal) == true
                && element.Name?.Contains(label, StringComparison.Ordinal) == true), $"{category} 剧本选择框");
        }

        void OpenActionFilters()
        {
            var action = app.Finder.ById(app.Window!, "StoryDownload.Action.Root");
            app.Finder.ById(action, "AdvancedFilters").Patterns.ExpandCollapse.Pattern.Expand();
        }
    }
}
