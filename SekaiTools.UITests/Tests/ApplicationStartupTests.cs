using FlaUI.Core.AutomationElements;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class ApplicationStartupTests
{
    [DesktopFact]
    [Trait("Category", "Smoke")]
    public void UI_SMOKE_001_连续三轮真实启动导航截图并关闭()
    {
        for (var iteration = 1; iteration <= 3; iteration++)
        {
            using var app = new ApplicationFixture($"UI-SMOKE-001-{iteration}");
            app.Step("启动", "隔离翻译工作区与主导航可用", () =>
            {
                app.Start();
                Assert.Contains("Sekai Tools", app.Window!.Title);
                Assert.True(app.Finder.ById(app.Window, Catalog.LoadScript).IsEnabled);
                app.Finder.ById(app.Window, Catalog.Navigation);
                foreach (var id in new[] { Catalog.SubtitleNavigation, Catalog.TranslationNavigation,
                    Catalog.DownloadNavigation, Catalog.EncodingNavigation, Catalog.SettingsNavigation })
                    Assert.True(app.Finder.ById(app.Window, id).IsEnabled);
            });
            app.Step("导航到设置", "设置页面可见", () => app.Navigate(Catalog.SettingsNavigation, Catalog.Settings));
            app.Step("返回翻译", "翻译载入按钮可用", () => app.Navigate(Catalog.TranslationNavigation, Catalog.Translation));
            app.Step("截图", "保存真实窗口截图", () =>
            {
                var screenshot = ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.Empty");
                Assert.True(new FileInfo(screenshot).Length > 1000);
            });
            app.Step("关闭", "真实异步关闭完成，退出码为零", app.Close);
        }
    }

    [DesktopFact]
    [Trait("Category", "Smoke")]
    public void UI_INFRA_001_定位失败保留首个现场()
    {
        using var app = new ApplicationFixture("UI-INFRA-001");
        app.Step("启动", "隔离翻译工作区可用", app.Start);
        app.Step("诊断自检", "缺失控件产生超时，并保存截图、树和目标信息", () =>
        {
            var failure = Assert.Throws<TimeoutException>(() =>
                app.Finder.ById(app.Window!, "Diagnostics.IntentionallyMissing", TimeSpan.FromMilliseconds(400)));
            FailureDiagnostics.Save(app.Data, app.Window, app.Finder.TargetId, failure, "ExpectedFailure");
            Assert.True(File.Exists(Path.Combine(app.Data.Directory, "ExpectedFailure", "window.png")));
            var tree = File.ReadAllText(Path.Combine(app.Data.Directory, "ExpectedFailure", "uia-tree.txt"));
            Assert.Contains(Catalog.LoadScript, tree);
            Assert.Contains("Diagnostics.IntentionallyMissing",
                File.ReadAllText(Path.Combine(app.Data.Directory, "ExpectedFailure-diagnostic.json")));
        });
        app.Step("关闭", "自检后正常退出", app.Close);
    }
}
