using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiToolsBase.GameScript;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationRowActionTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_006_条目操作保留空白语义与参考正文()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-006", "Functional");
        var script = Path.Combine(app.Data.Root, "actions.json");
        var reference = Path.Combine(app.Data.Root, "reference.txt");
        File.WriteAllText(script, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-actions",
            TalkData = [new("角色", "原文甲。", 0), new("角色", "原文乙。", 1), new("角色", "原文丙。", 2)],
            Snippets = [new() { Action = 1 }, new() { Action = 1, ReferenceIndex = 1 }, new() { Action = 1, ReferenceIndex = 2 }]
        }));
        File.WriteAllText(reference, "参考译名：参考正文甲。\n参考译名：参考正文乙。\n参考译名：参考正文丙。");
        var referenceHash = SHA256.HashData(File.ReadAllBytes(reference));
        app.Step("载入与空参考", "无参考时采用按钮禁用", () =>
        {
            app.Start(); Load(Catalog.LoadScript, script);
            Edit(3);
            OpenActions(1);
            Assert.False(Menu("采用参考译文（仅此条正文）").IsEnabled);
            var button = app.Finder.ById(app.Window!, "Translation.Row.1.Actions").BoundingRectangle;
            var item = Menu("查看上下文（显示全部条目）").BoundingRectangle;
            Assert.InRange(item.Left, button.Left - 4, button.Right + 4);
            Assert.InRange(item.Top, button.Bottom - 2, button.Bottom + 32);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Actions.NoReference");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
        });
        app.Step("参考与上下文", "采用仅改变当前正文，角色译名及其他条目不变，上下文清除搜索", () =>
        {
            Load("Translation.LoadReferenceButton", reference);
            app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference");
            Choose(1, "采用参考译文（仅此条正文）");
            WaitHelper.Until(() => Edit(1).Text == "参考正文甲。", "采用参考正文");
            Assert.Equal("", Edit(2).Text);
            Assert.Equal("角色", app.Finder.ById(app.Window!, "Translation.Dialog.1.Speaker").AsTextBox().Text);
            app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text = "原文甲";
            Choose(1, "查看上下文（显示全部条目）");
            WaitHelper.Until(() => app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox().Text == "", "上下文恢复搜索");
            Edit(3);
        });
        app.Step("条目状态与撤销", "有意留空不算未译，撤销恢复空白标记，未译与原文可区分", () =>
        {
            Choose(1, "有意留空（导出空白）");
            WaitHelper.Until(() => State(1) == "有意留空", "行内显示留空状态");
            Filter(1);
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")) is null, "有意留空退出未译列表");
            Edit(2);
            Filter(0);
            Choose(1, "使用原文");
            WaitHelper.Until(() => Edit(1).Text == "原文甲。", "原文已采用");
            Assert.Equal("沿用原文", State(1));
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_Z);
            WaitHelper.Until(() => Edit(1).Text == "", "撤销原文操作");
            Filter(1);
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")) is null, "撤销同时恢复留空标记");
            Filter(0);
            Choose(1, "标为未译（导出沿用原文）");
            WaitHelper.Until(() => State(1) == "未译 · 导出沿用原文", "行内显示未译及导出后果");
            Filter(1);
            Edit(1);
            Filter(0);
            Choose(1, "有意留空（导出空白）");
            Choose(3, "使用原文");
            Filter(1);
            Edit(2);
        });
        app.Step("筛选中保存", "全篇V2保留留空、未译回退原文和显式原文，参考文件未变", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "保存按钮").AsButton().Invoke();
            var output = Path.ChangeExtension(script, ".txt");
            WaitHelper.Until(() => File.Exists(output), "条目操作结果落盘");
            var data = new SekaiToolsBase.Story.Translation.TranslationData(output);
            Assert.True(data.IsV2);
            Assert.Equal(new[] { "", "原文乙。", "原文丙。" }, data.ForScript(new GameScript(script)).Select(line => line.Body));
            Assert.Equal(referenceHash, SHA256.HashData(File.ReadAllBytes(reference)));
            Filter(0);
            OpenActions(1);
            Menu("查看上下文（显示全部条目）");
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Actions.Saved");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
        });
        app.Step("退出", "已保存工作区正常关闭", app.Close);

        TextBox Edit(int index) => app.Finder.ById(app.Window!, $"Translation.Dialog.{index}.TargetText").AsTextBox();
        string State(int index) => app.Finder.ById(app.Window!, $"Translation.Dialog.{index}.State").Name;
        void OpenActions(int index) => app.Finder.ById(app.Window!, $"Translation.Row.{index}.Actions").AsButton().Invoke();
        AutomationElement Menu(string text) => WaitHelper.Until(() => app.Window!.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.Window.Properties.ProcessId.Value).And(cf.ByControlType(ControlType.MenuItem)).And(cf.ByName(text))), "条目操作菜单：" + text);
        void Choose(int index, string text) { OpenActions(index); Menu(text).AsMenuItem().Invoke(); }
        void Filter(int index)
        {
            var filter = app.Finder.ById(app.Window!, "Translation.StatusFilter").AsComboBox();
            filter.Select(index); filter.Collapse();
        }
        void Load(string id, string path)
        {
            app.Finder.ById(app.Window!, id).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "文件名输入框").AsTextBox().Text = path;
            Keyboard.Type(VirtualKeyShort.RETURN);
        }
    }
}
