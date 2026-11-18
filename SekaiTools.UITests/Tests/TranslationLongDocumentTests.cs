using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiToolsBase.GameScript;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationLongDocumentTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_003_长篇滚动编辑撤销筛选与完整导出()
    {
        const int count = 300;
        using var app = new ApplicationFixture("UI-TRANSLATION-003", "Functional");
        var scriptPath = Path.Combine(app.Data.Root, "long-story.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-long-story",
            TalkData = Enumerable.Range(1, count).Select(index => new Talk("角色", $"固定长篇原文{index:D4}。", index - 1)).ToArray(),
            Snippets = Enumerable.Range(0, count).Select(index => new Snippet { Action = 1, ReferenceIndex = index }).ToArray()
        }));
        var metrics = new Dictionary<string, object> { ["Entries"] = count };
        app.Step("启动", "隔离翻译工作区就绪", app.Start);
        app.Step("载入长篇", "首条可编辑且只创建部分可见输入框", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))), "文件名输入框").AsTextBox().Text = scriptPath;
            var watch = Stopwatch.StartNew();
            Keyboard.Type(VirtualKeyShort.RETURN);
            app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText");
            metrics["LoadToFirstEditorMs"] = watch.Elapsed.TotalMilliseconds;
            var list = app.Finder.ById(app.Window!, "Translation.EventList");
            var realized = list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)).Length;
            Assert.InRange(realized, 1, count - 1);
            metrics["InitialRealizedEditors"] = realized;
        });
        app.Step("首尾滚动编辑", "离屏返回后编辑不丢失，末条可编辑且支持整体撤销重做", () =>
        {
            app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text = "首条译文。";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            var list = app.Finder.ById(app.Window!, "Translation.EventList");
            var scroll = list.Patterns.Scroll.Pattern;
            Assert.True(scroll.VerticallyScrollable.Value);
            var watch = Stopwatch.StartNew();
            scroll.SetScrollPercent(-1, 100);
            var last = app.Finder.ById(app.Window!, $"Translation.Dialog.{count}.TargetText").AsTextBox();
            metrics["ScrollToEndMs"] = watch.Elapsed.TotalMilliseconds;
            last.Focus();
            last.Text = "末条译文。";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_Z);
            WaitHelper.Until(() => last.Text == "", "撤销末条编辑");
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_Y);
            WaitHelper.Until(() => last.Text == "末条译文。", "重做末条编辑");
            watch.Restart();
            scroll.SetScrollPercent(-1, 0);
            WaitHelper.Until(() => app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text == "首条译文。", "首条编辑跨滚动保留");
            metrics["ScrollToStartMs"] = watch.Elapsed.TotalMilliseconds;
        });
        app.Step("筛选定位与键盘", "Ctrl+F聚焦搜索，末条身份保留且搜索编辑可清除", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_F);
            var search = app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox();
            Assert.True(search.Properties.HasKeyboardFocus.Value);
            var watch = Stopwatch.StartNew();
            search.Text = "固定长篇原文0300";
            Assert.Equal("末条译文。", app.Finder.ById(app.Window!, $"Translation.Dialog.{count}.TargetText").AsTextBox().Text);
            metrics["FilterToLastMs"] = watch.Elapsed.TotalMilliseconds;
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "LongDocument.Filtered");
        });
        app.Step("保存完整剧本", "筛选只影响显示，磁盘输出保持300条原始顺序", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存按钮").AsButton().Invoke();
            var output = Path.ChangeExtension(scriptPath, ".txt");
            WaitHelper.Until(() => File.Exists(output), "长篇导出完成");
            var parsed = new SekaiToolsBase.Story.Translation.TranslationData(output).ForScript(new GameScript(scriptPath)).Select(item => item.Body).ToArray();
            Assert.Equal(count, parsed.Length);
            Assert.Equal("首条译文。", parsed[0]);
            Assert.Equal("末条译文。", parsed[^1]);
            for (var index = 1; index < count - 1; index++) Assert.Equal($"固定长篇原文{index + 1:D4}。", parsed[index]);
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "";
            Assert.Equal("首条译文。", app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "LongDocument.Saved");
        });
        app.Data.WriteJson("translation-performance", metrics);
        app.Step("退出", "已保存长篇正常关闭", app.Close);
    }
}
