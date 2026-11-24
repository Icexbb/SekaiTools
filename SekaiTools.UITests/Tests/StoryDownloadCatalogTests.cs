using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using SekaiToolsBase.DataList;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using SekaiTools.UITests.Automation;
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
        sources[0]!["MemberStoryTemplate"] = "character/member/{abName}/{scenarioId}.asset";
        var alternate = sources[0]!.DeepClone();
        alternate["SourceName"] = "UI Alternate Fixture";
        alternate["SourceTemplate"] = $"http://127.0.0.1:{server.Port}/alternate/{{type}}.json";
        alternate["StorageBaseUrl"] = $"http://127.0.0.1:{server.Port}/alternate-assets/";
        alternate["MemberStoryTemplate"] = "mirror/cards/{abName}/{scenarioId}.json";
        sources.Add(alternate);
        var sourceResponse = sources.ToJsonString();
        var missingActivityMetadata = 0;
        var failAlternate = 1;
        var failPrimary = 0;
        var allowScript = 0;
        var failAlternateAction = 0;
        using var artwork = new System.Drawing.Bitmap(16, 9);
        using (var graphics = System.Drawing.Graphics.FromImage(artwork))
            graphics.Clear(System.Drawing.Color.SteelBlue);
        using var png = new MemoryStream();
        artwork.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var imageBytes = png.ToArray();
        const string artworkPath = "/assets/character/member/res005_no056/";
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
            ["cards"] = JsonSerializer.Serialize(new[] { new Card
            {
                Id = 3, CharacterId = 1, Prefix = "Fixture Card", AssetbundleName = "res005_no056", CardRarityType = "rarity_3"
            } }),
            ["cardEpisodes"] = JsonSerializer.Serialize(new[]
            {
                new CardEpisode { Id = 30, CardId = 3, Title = "Fixture First Part", ScenarioId = "card-fixture-1", AssetBundleName = "res005_no056", CardEpisodePartType = "first_part" },
                new CardEpisode { Id = 31, CardId = 3, Title = "Fixture Second Part", ScenarioId = "card-fixture-2", AssetBundleName = "res005_no056", CardEpisodePartType = "second_part" }
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
        server.BinaryResponse = request => request.Contains(artworkPath) && request.Contains(".png ") ? imageBytes : null;
        server.Response = request =>
        {
            if (request.Contains("/sources.json ")) return sourceResponse;
            var path = request.Split(' ')[1];
            if (request.StartsWith("HEAD ", StringComparison.Ordinal))
                return path.StartsWith("/alternate", StringComparison.Ordinal) ? "" : null;
            if (path == "/alternate-assets/mirror/cards/res005_no056/card-fixture-2.json")
                return """{"error":"not a script"}""";
            if (path == artworkPath + "card-fixture-2.asset" && Volatile.Read(ref allowScript) != 0)
                return """{"ScenarioId":"card-fixture-2","TalkData":[],"Snippets":[]}""";
            var resource = Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith("/alternate/", StringComparison.Ordinal))
            {
                if (Volatile.Read(ref failAlternate) != 0 ||
                    resource == "actionSets" && Volatile.Read(ref failAlternateAction) != 0) return null;
                return resource == "actionSets"
                    ? catalog[resource].Replace("action-fixture", "alternate-fixture", StringComparison.Ordinal)
                    : catalog.GetValueOrDefault(resource);
            }
            if (!path.StartsWith("/master/", StringComparison.Ordinal)) return null;
            if (Volatile.Read(ref failPrimary) != 0) return null;
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
                if (category == "Card") VerifyArtwork();
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
                    ScreenshotHelper.Capture(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.EventAligned");
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
                ScreenshotHelper.Capture(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download." + category);
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
            Volatile.Write(ref failPrimary, 1);
            Refresh();
            WaitForText("刷新失败");
            Assert.Contains(server.Requests, request => request.Contains("/alternate/actionSets.json "));
            Volatile.Write(ref failPrimary, 0);
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
        app.Step("自动选源和下载回退", "测量来源并隐藏选择器，卡图和剧本按各自模板回退", () =>
        {
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            var mode = app.Finder.ById(app.Window!, "Settings.Field.DownloadSourceMode").AsComboBox();
            mode.Select(1);
            mode.Collapse();
            using var savedSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(app.Data.Paths.DataRoot, "Data", "setting.json")));
            Assert.Equal(1, savedSettings.RootElement.GetProperty("DownloadSourceMode").GetInt32());
            app.Navigate(Catalog.DownloadNavigation, "StoryDownload.Root");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("StoryDownload.Source"))
                is not { IsOffscreen: false }, "自动模式隐藏来源选择器");
            WaitHelper.Until(() => server.Requests.Any(request => request.StartsWith("HEAD /alternate/cards.json", StringComparison.Ordinal)),
                "探测备用源响应");
            var actionCategory = app.Finder.ById(app.Window!, "StoryDownload.Category.Action").AsListBoxItem();
            WaitHelper.Until(() => actionCategory.IsEnabled, "自动测速完成");
            actionCategory.Select();
            app.Finder.ById(app.Window!, "StoryDownload.Action.Root");
            Volatile.Write(ref failAlternateAction, 1);
            var beforeRefresh = server.Requests.Count;
            Refresh();
            WaitForText("action-fixture");
            var catalogRequests = server.Requests.Skip(beforeRefresh).Where(request =>
                request.StartsWith("GET ", StringComparison.Ordinal) && request.Contains("/actionSets.json ")).ToArray();
            Assert.StartsWith("GET /alternate/", catalogRequests.First());
            Assert.StartsWith("GET /master/", catalogRequests.Last());
            app.Finder.ById(app.Window!, "StoryDownload.Category.Card").AsListBoxItem().Select();
            app.Finder.ById(app.Window!, "StoryDownload.Card.Root");
            Refresh();
            Assert.Contains(server.Requests, request => request.StartsWith("GET /alternate/cardEpisodes.json", StringComparison.Ordinal));
            WaitForText("# 3");

            app.Finder.ById(app.Window!, "StoryDownload.Card.3.PreviewArtwork").AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "StoryDownload.CardArtwork.Dialog");
            WaitForText("特训前卡图");
            Assert.Contains(server.Requests, request => request.Contains("/alternate-assets/mirror/cards/res005_no056/card_normal.png "));
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("关闭")), "关闭卡图预览").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("StoryDownload.CardArtwork.Dialog"))
                is not { IsOffscreen: false }, "卡图预览已关闭");

            app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox().Text = "card-fixture-2";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("1 项结果 · 已选 0")), "只剩后篇剧本");
            app.Finder.ById(app.Window!, "StoryDownload.SelectResults").AsCheckBox().IsChecked = true;
            app.Finder.ById(app.Window!, "StoryDownload.AddSelectedButton").AsButton().Invoke();
            app.Finder.ById(app.Window!, "StoryDownload.StartDownloadButton").AsButton().Invoke();
            WaitForText("下载失败");
            const string preferred = "GET /alternate-assets/mirror/cards/res005_no056/card-fixture-2.json ";
            var backup = "GET " + artworkPath + "card-fixture-2.asset ";
            var requests = server.Requests.ToArray();
            var preferredIndex = Array.FindIndex(requests, request => request.StartsWith(preferred, StringComparison.Ordinal));
            var backupIndex = Array.FindIndex(requests, request => request.StartsWith(backup, StringComparison.Ordinal));
            Assert.True(preferredIndex >= 0 && backupIndex > preferredIndex);
            Volatile.Write(ref allowScript, 1);
            app.Finder.ById(app.Window!, "StoryDownload.Task.RetryButton").AsButton().Invoke();
            WaitForText("下载完成");
            var saved = Path.Combine(app.Data.Paths.DataRoot, "Scripts", "card-fixture-2.json");
            Assert.Equal("card-fixture-2", new SekaiToolsBase.GameScript.GameScript(saved).ScenarioId);

            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            mode = app.Finder.ById(app.Window!, "Settings.Field.DownloadSourceMode").AsComboBox();
            mode.Select(0);
            mode.Collapse();
            app.Navigate(Catalog.DownloadNavigation, "StoryDownload.Root");
            var manualPicker = app.Finder.ById(app.Window!, "StoryDownload.Source");
            Assert.False(manualPicker.IsOffscreen);
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

        void VerifyArtwork()
        {
            var preview = app.Finder.ById(app.Window!, "StoryDownload.Card.3.PreviewArtwork").AsButton();
            Assert.DoesNotContain(server.Requests, request => request.Contains(artworkPath));
            for (var visit = 0; visit < 2; visit++)
            {
                preview.Invoke();
                var dialog = app.Finder.ById(app.Window!, "StoryDownload.CardArtwork.Dialog");
                WaitForText("特训前卡图");
                app.Finder.ById(dialog, "StoryDownload.CardArtwork.Trained").AsButton().Invoke();
                WaitForText("特训后卡图");
                ScreenshotHelper.Capture(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.CardArtwork");
                WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("关闭")), "关闭卡图预览").AsButton().Invoke();
                WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("StoryDownload.CardArtwork.Dialog"))
                    is not { IsOffscreen: false }, "卡图预览已关闭");
            }
            Assert.Single(server.Requests, request => request.Contains(artworkPath + "card_normal.png "));
            Assert.Single(server.Requests, request => request.Contains(artworkPath + "card_after_training.png "));
            var cache = Path.Combine(app.Data.Paths.DataRoot, "Data", "CardArtworkCache");
            Assert.Equal(2, Directory.GetFiles(cache, "*.png").Length);
        }

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
