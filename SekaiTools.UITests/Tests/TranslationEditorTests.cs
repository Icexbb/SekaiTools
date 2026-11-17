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
public sealed class TranslationEditorTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_002_组合筛选编辑保持身份和导出顺序()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-002", "Functional");
        var scriptPath = Path.Combine(app.Data.Root, "multiple.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-multiple-story",
            TalkData = [new("甲", "原文甲", 0), new("乙", "原文乙", 1), new("丙", "原文丙", 2)],
            Snippets = [new() { Action = 1, ReferenceIndex = 0 }, new() { Action = 1, ReferenceIndex = 1 }, new() { Action = 1, ReferenceIndex = 2 }]
        }));
        app.Step("启动载入", "三条独立对话载入", () =>
        {
            app.Start();
            app.Finder.ById(app.Window!, Catalog.LoadScript).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))), "文件名编辑框").AsTextBox().Text = scriptPath;
            Keyboard.Type(VirtualKeyShort.RETURN);
            app.Finder.ById(app.Window!, "Translation.Dialog.3.TargetText");
        });
        app.Step("组合筛选编辑", "原始第二条在筛选中保留身份，译后退出未译列表", () =>
        {
            var status = app.Finder.ById(app.Window!, "Translation.StatusFilter").AsComboBox();
            status.Select(1);
            status.Collapse();
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "原文乙";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")) is null, "第一条被搜索隐藏");
            app.Finder.ById(app.Window!, "Translation.Dialog.2.TargetText").AsTextBox().Text = "第二译文。";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.2.TargetText")) is null, "已译条目退出未译筛选");
            status.Select(0);
            status.Collapse();
            Assert.Equal("第二译文。", app.Finder.ById(app.Window!, "Translation.Dialog.2.TargetText").AsTextBox().Text);
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "原文丙";
            app.Finder.ById(app.Window!, "Translation.Dialog.3.TargetText").AsTextBox().Text = "第三译文。";
        });
        app.Step("过滤列表导出", "输出全篇三条且保持原剧本顺序，未译沿用原文", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存按钮").AsButton().Invoke();
            var output = Path.ChangeExtension(scriptPath, ".txt");
            WaitHelper.Until(() => File.Exists(output), "导出完成");
            var parsed = new SekaiToolsBase.Story.Translation.TranslationData(output);
            Assert.Equal(new[] { "原文甲", "第二译文。", "第三译文。" }, parsed.ForScript(new GameScript(scriptPath)).Select(item => item.Body));
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "";
            Assert.Equal("第二译文。", app.Finder.ById(app.Window!, "Translation.Dialog.2.TargetText").AsTextBox().Text);
            Assert.Equal("第三译文。", app.Finder.ById(app.Window!, "Translation.Dialog.3.TargetText").AsTextBox().Text);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.Multiple");
        });
        app.Step("关闭", "已保存文档正常关闭", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_001_载入编辑并保存到隔离文件()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-001", "Functional");
        var scriptPath = Path.Combine(app.Data.Root, "sample.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-test-story",
            TalkData = [new("角色", "原文", 0)],
            Snippets = [new() { Action = 1 }]
        }));
        app.Step("启动", "翻译工作区就绪", app.Start);
        app.Step("载入剧本", "通过原生文件选择器载入独立样本", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            var fileName = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))), "原生文件名编辑框");
            fileName.AsTextBox().Text = scriptPath;
            Keyboard.Type(VirtualKeyShort.RETURN);
            app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText");
        });
        app.Step("编辑保存", "实际编辑后导出 TXT v2，磁盘内容含译文", () =>
        {
            app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text = "测试译文";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            var save = WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存选项主按钮");
            save.AsButton().Invoke();
            var output = Path.ChangeExtension(scriptPath, ".txt");
            WaitHelper.Until(() => File.Exists(output), "字幕翻译文件落盘");
            Assert.Contains("测试译文", File.ReadAllText(output));
            var parsed = new SekaiToolsBase.Story.Translation.TranslationData(output);
            Assert.True(parsed.IsV2);
            Assert.Equal("测试译文", Assert.Single(parsed.ForScript(new GameScript(scriptPath))).Body);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.Saved");
        });
        var originalOutput = Path.ChangeExtension(scriptPath, ".txt");
        var originalHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(originalOutput));
        app.Step("取消覆盖", "取消覆盖不修改原文件且保留当前编辑", () =>
        {
            app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text = "修订译文";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var confirmation = app.Finder.ById(app.Window!, "Export.OverwriteConfirmation");
            var cancel = WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("取消").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "取消覆盖按钮");
            cancel.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Export.OverwriteConfirmation")) is null, "覆盖确认关闭");
            Assert.Equal(originalHash, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(originalOutput)));
            Assert.Equal("修订译文", app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text);
        });
        app.Step("筛选后另存为", "隐藏条目仍完整导出到新文件，原文件不变", () =>
        {
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "不存在的搜索结果";
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")) is null, "列表筛选为空");
            app.Finder.ById(app.Window!, "Translation.SaveAsButton").AsButton().Invoke();
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("使用校对文件名标签")), "校对标签").AsButton().Invoke();
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "另存为保存按钮").AsButton().Invoke();
            var copy = Path.Combine(app.Data.Root, "【校对】sample.txt");
            WaitHelper.Until(() => File.Exists(copy), "另存为文件生成");
            var parsed = new SekaiToolsBase.Story.Translation.TranslationData(copy);
            Assert.Equal("修订译文", Assert.Single(parsed.ForScript(new GameScript(scriptPath))).Body);
            Assert.Equal(originalHash, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(originalOutput)));
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "";
            Assert.Equal("修订译文", app.Finder.ById(app.Window!, "Translation.Dialog.1.TargetText").AsTextBox().Text);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.SaveAs");
        });
        app.Step("关闭", "已保存文档正常退出", app.Close);
    }
}
