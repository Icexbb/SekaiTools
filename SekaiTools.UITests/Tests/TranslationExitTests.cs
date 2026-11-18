using System.Security.Cryptography;
using System.Text.Json;
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
public sealed class TranslationExitTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_009_退出取消不保存与保存()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-009", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var draft = Path.Combine(app.Data.Paths.DataRoot, "Data", "translation-draft.json");
        var output = Path.ChangeExtension(ui.ScriptPath, ".txt");
        app.Step("启动与草稿", "脏编辑实际自动写入隔离草稿", () =>
        {
            ui.Start(); ui.Body(1).Text = "未保存退出。";
            WaitHelper.Until(() => DraftContains(draft, "未保存退出。"), "自动草稿包含当前编辑");
        });
        app.Step("取消退出", "保留编辑和草稿，尚未生成导出文件", () =>
        {
            app.Window!.Close(); ui.Confirm("取消", "退出程序？");
            Assert.Equal("未保存退出。", ui.Body(1).Text);
            Assert.True(DraftContains(draft, "未保存退出。"));
            Assert.False(File.Exists(output));
        });
        app.Step("不保存退出", "进程正常退出且删除恢复草稿，重启为空工作区", () =>
        {
            app.Window!.Close(); ui.Confirm("不保存", "退出程序？"); app.WaitForExit();
            Assert.False(File.Exists(draft)); Assert.False(File.Exists(output));
            app.Restart();
            Assert.Equal("载入剧本", ui.Button(Catalog.LoadScript).Name);
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.TargetText")));
        });
        app.Step("保存后退出", "真实关闭流程打开保存选项，落盘后正常退出", () =>
        {
            ui.Load(Catalog.LoadScript, ui.ScriptPath);
            ui.Body(1).Text = "退出时保存。";
            app.Window!.Close(); ui.Confirm("保存", "退出程序？");
            SaveOptions();
            WaitHelper.Until(() => File.Exists(output) && new TranslationData(output).ForScript(ui.Script)[0].Body == "退出时保存。", "关闭流程已保存译文");
            app.WaitForExit();
            Assert.False(File.Exists(draft));
        });

        void SaveOptions()
        {
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "关闭保存选项").AsButton().Invoke();
        }
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_010_真实草稿副本跨进程恢复()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-010", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var draft = Path.Combine(app.Data.Paths.DataRoot, "Data", "translation-draft.json");
        var loaded = Path.Combine(app.Data.Root, "loaded.txt");
        File.WriteAllText(loaded, "基准名：原载入正文。\n基准名：第二基准正文。");
        var loadedHash = SHA256.HashData(File.ReadAllBytes(loaded));
        byte[] snapshot = [];
        app.Step("真实生成草稿", "载入基准、正文、独立译名和选项进入草稿", () =>
        {
            ui.Start(); ui.Load("Translation.LoadTranslationButton", loaded); ui.Confirm("继续", "替换当前内容？");
            WaitHelper.Until(() => ui.Body(1).Text == "原载入正文。", "当前TXT已载入");
            ui.Speaker(1).Text = "同步草稿名";
            ui.Option("SyncSpeakers", false); ui.Option("AutoFormat", false);
            ui.Speaker(1).Text = "独立草稿名";
            ui.Body(1).Text = "草稿新正文。";
            ui.Body(2).Focus();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            WaitHelper.Until(() => DraftContains(draft, "草稿新正文。"), "离页草稿包含正文");
            snapshot = File.ReadAllBytes(draft);
            using var data = JsonDocument.Parse(snapshot);
            Assert.False(data.RootElement.GetProperty("AutoFormat").GetBoolean());
            Assert.False(data.RootElement.GetProperty("SyncMatchingSpeakers").GetBoolean());
            Assert.Equal(2, data.RootElement.GetProperty("ActiveSequenceNumber").GetInt32());
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
        });
        app.Step("隔离恢复副本", "正常退出后仅在测试目录放回真实草稿副本，不模拟正式实例崩溃", () =>
        {
            app.Window!.Close(); ui.Confirm("不保存", "退出程序？"); app.WaitForExit();
            Assert.False(File.Exists(draft));
            File.WriteAllBytes(draft, snapshot);
            app.Restart();
            ui.Confirm("稍后", "重新打开上次未保存的翻译？");
            Assert.True(File.Exists(draft));
            Assert.Equal("载入剧本", ui.Button(Catalog.LoadScript).Name);
            app.Restart();
            ui.Confirm("重新打开", "重新打开上次未保存的翻译？");
        });
        app.Step("核对恢复状态", "值、加载快照、编辑选项及活动正文焦点恢复，源TXT不变", () =>
        {
            WaitHelper.Until(() => ui.Body(1).Text == "草稿新正文。", "草稿正文恢复");
            Assert.Equal("独立草稿名", ui.Speaker(1).Text);
            Assert.Equal("同步草稿名", ui.Speaker(2).Text);
            Assert.Equal("原载入正文。", app.Finder.ById(app.Window!, "Translation.Dialog.1.Loaded").AsTextBox().Text);
            WaitHelper.Until(() => ui.Body(2).Properties.HasKeyboardFocus.Value, "恢复活动条目焦点");
            ui.Button("Translation.OpenEditorOptions").Invoke();
            Assert.False(ui.Owned("Translation.Option.SyncSpeakers").AsCheckBox().IsChecked);
            var format = ui.Owned("Translation.Option.AutoFormat").AsCheckBox();
            Assert.False(format.IsChecked); format.Focus(); Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.Equal(loadedHash, SHA256.HashData(File.ReadAllBytes(loaded)));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Workspace.DraftRestored");
        });
        app.Step("不保存关闭恢复副本", "恢复数据可再次退出且不修改源TXT", () =>
        {
            app.Window!.Close(); ui.Confirm("不保存", "退出程序？"); app.WaitForExit();
            Assert.False(File.Exists(draft));
            Assert.Equal(loadedHash, SHA256.HashData(File.ReadAllBytes(loaded)));
        });
        app.Step("丢弃恢复副本", "恢复提示的丢弃分支删除草稿并保持空工作区", () =>
        {
            File.WriteAllBytes(draft, snapshot);
            app.Restart();
            ui.Confirm("丢弃草稿", "重新打开上次未保存的翻译？");
            WaitHelper.Until(() => !File.Exists(draft), "恢复副本已丢弃");
            Assert.Equal("载入剧本", ui.Button(Catalog.LoadScript).Name);
            Assert.Equal(loadedHash, SHA256.HashData(File.ReadAllBytes(loaded)));
            app.Close();
        });
    }

    private static bool DraftContains(string path, string text)
    {
        if (!File.Exists(path)) return false;
        using var data = JsonDocument.Parse(File.ReadAllText(path));
        return data.RootElement.GetProperty("Values").EnumerateArray().Any(value => value.GetProperty("Text").GetString() == text);
    }
}
