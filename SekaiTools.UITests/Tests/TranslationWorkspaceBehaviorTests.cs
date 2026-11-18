using System.Globalization;
using System.Security.Cryptography;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiToolsBase.Story.Translation;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationWorkspaceBehaviorTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_007_导入失败取消与清理保留正确工作区()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-007", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var reference = Path.Combine(app.Data.Root, "reference.txt");
        var invalid = Path.Combine(app.Data.Root, "mismatch.txt");
        var version2 = Path.Combine(app.Data.Root, "version2.txt");
        File.WriteAllText(reference, "参考名：参考甲。\n参考名：参考乙。");
        File.WriteAllText(invalid, "角色：只有一条。");
        File.WriteAllText(version2, TranslationFormatV2.Export(ui.Script,
            [new DialogTranslate("译名", "V2译文。"), new DialogTranslate("译名", "")]));
        var hashes = new[] { reference, invalid, version2 }.ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
        app.Step("启动", "载入剧本与参考，保留未保存编辑", () =>
        {
            ui.Start(); ui.Load("Translation.LoadReferenceButton", reference);
            app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference");
            ui.Body(1).Text = "保留编辑。";
        });
        app.Step("不匹配导入", "失败原因可读且当前编辑、参考不被覆盖", () =>
        {
            ui.Load("Translation.LoadTranslationButton", invalid);
            var error = app.Finder.ById(app.Window!, "Translation.ImportErrorDetails");
            Assert.False(error.IsOffscreen);
            Assert.False(string.IsNullOrWhiteSpace(error.Name));
            Assert.Equal("保留编辑。", ui.Body(1).Text);
            Assert.Equal("参考甲。", app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference").AsTextBox().Text);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Workspace.ImportMismatch");
            ui.Button("Translation.DismissImportError").Invoke();
        });
        app.Step("V2替换取消和确认", "取消保留当前编辑，确认后导入V2及明确空白，同时保留参考", () =>
        {
            ui.Load("Translation.LoadTranslationButton", version2); ui.Confirm("取消");
            Assert.Equal("保留编辑。", ui.Body(1).Text);
            ui.Load("Translation.LoadTranslationButton", version2); ui.Confirm("不保存");
            WaitHelper.Until(() => ui.Body(1).Text == "V2译文。", "V2正文载入");
            Assert.Equal("译名", ui.Speaker(1).Text);
            Assert.Equal("有意留空", app.Finder.ById(app.Window!, "Translation.Dialog.2.State").Name);
            Assert.Equal("参考甲。", app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference").AsTextBox().Text);
        });
        app.Step("译文清理与参考移除", "取消无变化，确认只清译文，移除参考保留剧本", () =>
        {
            ui.Clear("清除当前译文（保留剧本和参考）"); ui.Confirm("取消");
            Assert.Equal("V2译文。", ui.Body(1).Text);
            ui.Clear("清除当前译文（保留剧本和参考）"); ui.Confirm("继续");
            WaitHelper.Until(() => ui.Body(1).Text == "" && ui.Speaker(1).Text == "角色", "清除译文保留原剧本");
            Assert.Equal("参考甲。", app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference").AsTextBox().Text);
            ui.Clear("移除参考翻译");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.Reference")) is not { IsOffscreen: false }, "参考已移除");
            ui.Body(2);
        });
        app.Step("清空整个工作区", "取消保留剧本，确认恢复空状态，输入文件未改动", () =>
        {
            ui.Clear("清除整个工作区"); ui.Confirm("取消"); ui.Body(2);
            ui.Clear("清除整个工作区"); ui.Confirm("继续");
            WaitHelper.Until(() => ui.Button(Catalog.LoadScript).Name == "载入剧本", "工作区已清空");
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")));
            foreach (var (path, hash) in hashes) Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Workspace.Cleared");
        });
        app.Step("退出", "空工作区正常关闭", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_008_角色同步格式化焦点与文本缩放()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-008", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        app.Step("启动", "两个同名角色样本载入", ui.Start);
        app.Step("角色同步及整体撤销", "默认同步同名译名且不改正文，关闭开关后仅修改当前角色", () =>
        {
            ui.Speaker(1).Focus(); ui.Speaker(1).Text = "同步译名";
            ui.Button(Catalog.LoadScript).Focus();
            WaitHelper.Until(() => ui.Speaker(2).Text == "同步译名", "同名角色同步");
            Assert.Equal("", ui.Body(1).Text); Assert.Equal("", ui.Body(2).Text);
            ui.Shortcut(VirtualKeyShort.KEY_Z);
            WaitHelper.Until(() => ui.Speaker(1).Text == "角色" && ui.Speaker(2).Text == "角色", "一次撤销恢复两个译名");
            ui.Shortcut(VirtualKeyShort.KEY_Y);
            WaitHelper.Until(() => ui.Speaker(2).Text == "同步译名", "整体重做角色修改");
            ui.Option("SyncSpeakers", false);
            ui.Speaker(1).Text = "独立译名";
            Assert.Equal("同步译名", ui.Speaker(2).Text);
        });
        app.Step("格式化焦点时机", "输入过程中保留原写法，失焦才规范；关闭开关后失焦也不改写", () =>
        {
            ui.Body(1).Focus(); ui.Body(1).Text = "你好?!";
            Assert.Equal("你好?!", ui.Body(1).Text);
            ui.Button(Catalog.LoadScript).Focus();
            WaitHelper.Until(() => ui.Body(1).Text == "你好？！", "失焦规范半角标点");
            ui.Option("AutoFormat", false);
            ui.Body(1).Focus(); ui.Body(1).Text = "保留?!";
            ui.Button(Catalog.LoadScript).Focus();
            Assert.Equal("保留?!", ui.Body(1).Text);
        });
        app.Step("100到200文本缩放", "缩放改变实际文字字体，不改变正文或窗口尺寸，边界按钮正确禁用", () =>
        {
            var beforeWindow = app.Window!.BoundingRectangle;
            var font = FontSize();
            Assert.False(ui.Button("Translation.ZoomOut").IsEnabled);
            for (var index = 0; index < 10; index++) ui.Button("Translation.ZoomIn").Invoke();
            WaitHelper.Until(() => !ui.Button("Translation.ZoomIn").IsEnabled, "达到200%上限");
            Assert.InRange(FontSize(), font * 1.95, font * 2.05);
            Assert.Equal(beforeWindow, app.Window.BoundingRectangle);
            Assert.Equal("保留?!", ui.Body(1).Text);
            ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), "Workspace.Zoom200");
            for (var index = 0; index < 10; index++) ui.Button("Translation.ZoomOut").Invoke();
            WaitHelper.Until(() => !ui.Button("Translation.ZoomOut").IsEnabled, "恢复100%下限");
            Assert.InRange(FontSize(), font - 0.1, font + 0.1);
        });
        app.Step("保存结果", "磁盘保留独立/同步角色译名以及未格式化正文", () =>
        {
            ui.Shortcut(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存按钮").AsButton().Invoke();
            var output = Path.ChangeExtension(ui.ScriptPath, ".txt");
            WaitHelper.Until(() => File.Exists(output), "同步与格式化结果落盘");
            var values = new TranslationData(output).ForScript(ui.Script).Cast<DialogTranslate>().ToArray();
            Assert.Equal("独立译名", values[0].Chara); Assert.Equal("同步译名", values[1].Chara);
            Assert.Equal("保留?!", values[0].Body); Assert.Equal("原文乙。", values[1].Body);
        });
        app.Step("退出", "已保存工作区正常关闭", app.Close);
        double FontSize() => Convert.ToDouble(ui.Body(1).Patterns.Text.Pattern.DocumentRange.GetAttributeValue(app.Window!.Automation.TextAttributeLibrary.FontSize), CultureInfo.InvariantCulture);
    }
}
