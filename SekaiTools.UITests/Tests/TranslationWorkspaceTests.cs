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
public sealed class TranslationWorkspaceTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_004_紧凑筛选与编辑选项实际绑定()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-004", "Functional");
        var scriptPath = Path.Combine(app.Data.Root, "workspace.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-workspace",
            SpecialEffectData = [new(8, "相同场景原文。", "", 1, 0), new(8, "相同场景原文。", "", 1, 0)],
            Snippets = [new() { Action = 6 }, new() { Action = 6, ReferenceIndex = 1 }]
        }));
        app.Step("启动载入", "两条同原文样本就绪", () =>
        {
            app.Start();
            app.Finder.ById(app.Window!, Catalog.LoadScript).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))), "文件名输入框").AsTextBox().Text = scriptPath;
            Keyboard.Type(VirtualKeyShort.RETURN);
            app.Finder.ById(app.Window!, "Translation.Effect.2.TargetText");
        });
        app.Step("编辑选项实际绑定", "关闭同步后独立编辑，重新开启不立即覆盖，后续编辑同步且可整体撤销", () =>
        {
            OpenOptions();
            Option("SyncContent").IsChecked = false;
            Option("ShowReference").IsChecked = false;
            Option("ShowReference").Focus();
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            WaitHelper.Until(() => FindOption("SyncContent") is null, "Esc关闭编辑选项");
            Edit(1).Focus();
            Edit(1).Text = "独立译文。";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            Assert.Equal("", Edit(2).Text);
            OpenOptions();
            Assert.False(Option("SyncContent").IsChecked);
            Assert.False(Option("ShowReference").IsChecked);
            Option("SyncContent").IsChecked = true;
            Option("ShowReference").IsChecked = true;
            Option("ShowReference").Focus();
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.Equal("", Edit(2).Text);
            Edit(1).Focus();
            Edit(1).Text = "同步译文。";
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            WaitHelper.Until(() => Edit(2).Text == "同步译文。", "实际模型同步第二条");
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_Z);
            WaitHelper.Until(() => Edit(1).Text == "独立译文。" && Edit(2).Text == "", "一次撤销恢复整个同步修改");
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_Y);
            WaitHelper.Until(() => Edit(1).Text == "同步译文。" && Edit(2).Text == "同步译文。", "同步修改整体重做");
        });
        app.Step("主题与尺寸", "紧凑搜索不受挤压，选项浮层完整可见且关闭后不占列表空间", () =>
        {
            var desktop = app.Window!.Automation.GetDesktop().BoundingRectangle;
            foreach (var (label, name) in new[] { ("亮色", "Light"), ("暗色", "Dark"), ("高对比度", "HighContrast") })
            {
                app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
                var theme = app.Finder.ById(app.Window!, "Settings.Field.CurrentApplicationTheme").AsComboBox();
                theme.Focus(); theme.Expand();
                WaitHelper.Until(() => theme.Items.FirstOrDefault(item => item.Text == label), "主题选项就绪").Patterns.SelectionItem.Pattern.Select();
                theme.Collapse();
                app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
                foreach (var (width, height) in new[] { (960, 600), (1280, 960) })
                {
                    if (desktop.Width < width + 24 || desktop.Height < height + 24) throw new InvalidOperationException("翻译布局验证桌面尺寸不足。");
                    app.Window!.Patterns.Transform.Pattern.Resize(width, height);
                    app.Window.Patterns.Transform.Pattern.Move(desktop.Left + (desktop.Width - width) / 2, desktop.Top + (desktop.Height - height) / 2);
                    WaitHelper.Until(() => Math.Abs(app.Window.BoundingRectangle.Width - width) <= 1, "实际窗口尺寸");
                    var list = app.Finder.ById(app.Window, "Translation.EventList").BoundingRectangle;
                    Assert.True(list.Height >= 200, "最小窗口仍应保留至少200像素列表高度。");
                    var search = app.Finder.ById(app.Window, "Translation.SearchBox").BoundingRectangle;
                    Assert.True(search.Width >= (width == 960 ? 650 : 500), "搜索应保留合理的输入宽度。");
                    foreach (var id in new[] { "Translation.TypeFilter", "Translation.StatusFilter", "Translation.NextUntranslated", "Translation.NextWarning", "Translation.OpenEditorOptions" })
                    {
                        var element = app.Finder.ById(app.Window, id);
                        Assert.False(element.IsOffscreen);
                        Assert.InRange(element.BoundingRectangle.Right, app.Window.BoundingRectangle.Left, app.Window.BoundingRectangle.Right);
                        Assert.True(element.BoundingRectangle.Bottom <= list.Top);
                    }
                    OpenOptions();
                    foreach (var option in new[] { "ShowReference", "HighlightReference", "HighlightModified", "SyncContent", "SyncSpeakers", "AutoFormat" })
                    {
                        var item = Option(option);
                        Assert.False(item.IsOffscreen);
                        Assert.InRange(item.BoundingRectangle.Bottom, app.Window.BoundingRectangle.Top, app.Window.BoundingRectangle.Bottom);
                        Assert.InRange(item.BoundingRectangle.Right, app.Window.BoundingRectangle.Left, app.Window.BoundingRectangle.Right);
                    }
                    ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), $"{name}.{width}.Options");
                    Option("AutoFormat").Focus();
                    Keyboard.Type(VirtualKeyShort.ESCAPE);
                    WaitHelper.Until(() => FindOption("AutoFormat") is null, "浮层关闭");
                    Assert.Equal(list, app.Finder.ById(app.Window, "Translation.EventList").BoundingRectangle);
                    Assert.Equal("同步译文。", Edit(1).Text);
                    ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), $"{name}.{width}.Workspace");
                }
            }
        });
        app.Step("保存退出", "正常保存全部译文并关闭", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存按钮").AsButton().Invoke();
            WaitHelper.Until(() => File.Exists(Path.ChangeExtension(scriptPath, ".txt")), "译文落盘");
            app.Close();
        });

        TextBox Edit(int index) => app.Finder.ById(app.Window!, $"Translation.Effect.{index}.TargetText").AsTextBox();
        AutomationElement? FindOption(string key) => app.Window!.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.Window.Properties.ProcessId.Value).And(cf.ByAutomationId("Translation.Option." + key)));
        CheckBox Option(string key) => WaitHelper.Until(() => FindOption(key), "编辑选项：" + key).AsCheckBox();
        void OpenOptions()
        {
            app.Finder.ById(app.Window!, "Translation.OpenEditorOptions").AsButton().Invoke();
            Option("SyncContent");
        }
    }
}
