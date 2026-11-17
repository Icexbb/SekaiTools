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
        var script = JsonSerializer.Serialize(new SekaiToolsBase.GameScript.GameScript
        {
            ScenarioId = "fixture-scenario",
            TalkData = [new("测试角色", "下载正文", 0)],
            Snippets = [new() { Action = 1 }]
        });
        var assetMode = 0;
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.BeforeResponseAsync = async (request, token) =>
        {
            if (request.Contains("/assets/fixture-scenario.asset ") && Volatile.Read(ref assetMode) == 1)
            {
                heldRequest.TrySetResult();
                await releaseResponse.Task.WaitAsync(token);
            }
        };
        server.Response = request => request.Contains("/sources.json ") ? sources
            : request.Contains("/master/unitStories.json ") ? stories
            : request.Contains("/assets/fixture-scenario.asset ") ? Volatile.Read(ref assetMode) == 0 ? null : script
            : request.StartsWith("GET /master/", StringComparison.Ordinal) ? "[]" : null;
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
        app.Step("切换五类剧情", "其余分类正常刷新空数据，搜索状态保留，返回主线恢复已载入样本", () =>
        {
            app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox().Text = "NoMatchingFixture";
            foreach (var (category, resource) in new[] { ("Event", "eventStories"), ("Special", "specialStories"), ("Card", "cardEpisodes"), ("Action", "actionSets") })
            {
                var tab = app.Finder.ById(app.Window!, "StoryDownload.Category." + category).AsListBoxItem();
                tab.Select();
                Assert.True(tab.IsSelected);
                app.Finder.ById(app.Window!, "StoryDownload." + category + ".Root");
                Assert.Equal("NoMatchingFixture", app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox().Text);
                var refresh = app.Finder.ById(app.Window!, "StoryDownload.RefreshButton");
                WaitHelper.Until(() => refresh.IsEnabled, category + " 刷新就绪");
                refresh.AsButton().Invoke();
                WaitHelper.Until(() => server.Requests.Any(request => request.Contains("/master/" + resource + ".json ")), category + " 实际数据请求");
                WaitHelper.Until(() => refresh.IsEnabled, category + " 刷新结束");
                WaitHelper.Until(() => !app.Finder.ById(app.Window!, "StoryDownload.EmptyMessage").IsOffscreen, category + " 空结果提示");
                ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.Empty." + category);
            }
            app.Finder.ById(app.Window!, "StoryDownload.Category.Unit").AsListBoxItem().Select();
            app.Finder.ById(app.Window!, "StoryDownload.Unit.Root");
            app.Finder.ById(app.Window!, "StoryDownload.SearchBox").AsTextBox().Text = "";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("Fixture Episode")), "返回主线恢复样本");
        });
        app.Step("选择并下载", "通过队列实际获取本地剧本，文件位于隔离下载目录", () =>
        {
            app.Finder.ById(app.Window!, "StoryDownload.SelectResults").AsCheckBox().IsChecked = true;
            var add = app.Finder.ById(app.Window!, "StoryDownload.AddSelectedButton");
            WaitHelper.Until(() => add.IsEnabled, "加入已选按钮启用");
            app.Finder.ById(app.Window!, "StoryDownload.SelectResults").AsCheckBox().IsChecked = false;
            WaitHelper.Until(() => !add.IsEnabled, "取消全选清除选择");
            app.Finder.ById(app.Window!, "StoryDownload.SelectResults").AsCheckBox().IsChecked = true;
            WaitHelper.Until(() => add.IsEnabled, "重新全选后可加入队列");
            add.AsButton().Invoke();
            var start = app.Finder.ById(app.Window!, "StoryDownload.StartDownloadButton");
            WaitHelper.Until(() => start.IsEnabled, "队列可下载");
            start.AsButton().Invoke();
            var path = Path.Combine(app.Data.Paths.DataRoot, "Scripts", "fixture-scenario.asset");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("下载失败")), "503失败状态");
            Assert.False(File.Exists(path));
            if (Directory.Exists(Path.GetDirectoryName(path))) Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.Failed");
            var retry = app.Finder.ById(app.Window!, "StoryDownload.Task.RetryButton");
            WaitHelper.Until(() => retry.IsEnabled, "失败任务可重试");
            Volatile.Write(ref assetMode, 1);
            retry.AsButton().Invoke();
            WaitHelper.Until(() => heldRequest.Task.IsCompleted, "真实资产请求等待受控响应");
            WaitHelper.Until(() => start.IsEnabled, "停止下载按钮启用");
            start.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已停止")), "停止任务状态");
            Assert.False(File.Exists(path));
            if (Directory.Exists(Path.GetDirectoryName(path))) Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
            releaseResponse.TrySetResult();
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.Stopped");
            Volatile.Write(ref assetMode, 2);
            WaitHelper.Until(() => retry.IsEnabled, "停止任务可重试");
            retry.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("下载完成")), "任务完成状态");
            Assert.True(File.Exists(path));
            Assert.Equal(script, File.ReadAllText(path));
            var parsed = new SekaiToolsBase.GameScript.GameScript(path);
            Assert.Equal("fixture-scenario", parsed.ScenarioId);
            Assert.Equal("下载正文", Assert.Single(parsed.TalkData).Body);
            Assert.Contains(server.Requests, request => request.Contains("/assets/fixture-scenario.asset "));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Download.Completed");
            app.Finder.ById(app.Window!, "StoryDownload.ClearQueueButton").AsButton().Invoke();
            WaitHelper.Until(() => !start.IsEnabled, "清空队列后无任务可下载");
            Assert.Equal(script, File.ReadAllText(path));
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "正常退出", app.Close);
    }
}
