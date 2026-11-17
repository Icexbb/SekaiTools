using System.Text.Json;
using FlaUI.Core.AutomationElements;
using SekaiToolsBase.DataList;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class StoryDownloadTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DOWNLOAD_001_本地数据源刷新搜索及空结果恢复()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-DOWNLOAD-001", "Functional");
        var config = Path.Combine(app.Data.Root, "network-endpoints.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
        using var endpoints = JsonDocument.Parse(File.ReadAllText(config));
        var sources = endpoints.RootElement.GetProperty("defaultDataSources").GetRawText();
        var stories = JsonSerializer.Serialize(new[] { new UnitStory
        {
            Unit = "light_sound", Seq = 1, Chapters = [new UnitChapter
            {
                Id = 1, Unit = "light_sound", Title = "Fixture Chapter", AssetBundleName = "fixture",
                Episodes = [new UnitEpisode { Id = 1, Title = "Fixture Episode", ScenarioId = "fixture-scenario", EpisodeNoLabel = "1" }]
            }]
        }});
        server.Response = request => request.Contains("/sources.json ") ? sources
            : request.Contains("/master/unitStories.json ") ? stories : null;
        app.Step("启动与下载页", "正常获取本地数据源并加载页面", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.StoryDownload", "StoryDownload.Root");
            WaitHelper.Until(() => server.Requests.Any(request => request.Contains("/sources.json ")), "数据源请求完成");
        });
        app.Step("刷新列表", "通过真实刷新按钮获取主线样本", () =>
        {
            var refresh = app.Finder.ById(app.Window!, "StoryDownload.RefreshButton");
            WaitHelper.Until(() => refresh.IsEnabled, "刷新按钮就绪");
            refresh.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("Fixture Episode")), "样本章节条目出现");
            Assert.Contains(server.Requests, request => request.Contains("/master/unitStories.json "));
        });
        app.Step("搜索与恢复", "无匹配时显示空结果，清空搜索恢复样本", () =>
        {
            var search = app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox();
            search.Text = "NoMatchingFixture";
            WaitHelper.Until(() => !app.Finder.ById(app.Window!, "StoryDownload.EmptyMessage").IsOffscreen, "空结果提示显示");
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByName("Fixture Episode")));
            search.Text = "";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("Fixture Episode")), "清空搜索后条目恢复");
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.UnitStory");
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "正常退出", app.Close);
    }
}
