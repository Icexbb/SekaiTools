using System.Drawing;
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
public sealed class TranslationComparisonTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_005_参考与载入快照独立开关及三行输入()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-005", "Functional");
        var script = Path.Combine(app.Data.Root, "comparison.json");
        var loaded = Path.Combine(app.Data.Root, "loaded.txt");
        var reference = Path.Combine(app.Data.Root, "reference.txt");
        File.WriteAllText(script, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-comparison",
            TalkData = [new("角色", "原文第一条。", 0), new("角色", "原文第二条。", 1)],
            Snippets = [new() { Action = 1 }, new() { Action = 1, ReferenceIndex = 1 }]
        }));
        File.WriteAllText(loaded, "角色：载入译文甲。\n角色：载入译文乙。");
        File.WriteAllText(reference, "角色：参考译文甲。\n角色：参考译文乙。");
        app.Step("启动载入", "通过实际选择器载入剧本、当前译文和参考", () =>
        {
            app.Start();
            Load(Catalog.LoadScript, script);
            Edit(1);
            Load("Translation.LoadTranslationButton", loaded);
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("继续").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "确认替换当前译文").AsButton().Invoke();
            WaitHelper.Until(() => Edit(1).Text == "载入译文甲。", "当前译文载入");
            Load("Translation.LoadReferenceButton", reference);
            Assert.Equal("参考译文甲。", Field("Reference").Text);
            Assert.Equal("载入译文甲。", Edit(1).Text);
        });
        const string revised = "第一行。\n第二行。\n第三行。";
        app.Step("多行与Tab", "真实键盘接受三行、拒绝第四行，Tab定位下一条正文", () =>
        {
            Edit(1).Focus();
            Edit(1).Text = "第一行。";
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.END);
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type("第二行。");
            Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type("第三行。");
            Assert.Equal(revised, Normalize(Edit(1).Text));
            Keyboard.Type(VirtualKeyShort.RETURN);
            Assert.Equal(revised, Normalize(Edit(1).Text));
            Keyboard.Type(VirtualKeyShort.TAB);
            WaitHelper.Until(() => Edit(2).Properties.HasKeyboardFocus.Value, "Tab到下一条可见正文");
            Assert.Equal(revised, Normalize(Edit(1).Text));
            Assert.Equal("载入译文甲。", Field("Loaded").Text);
        });
        app.Step("双差异独立开关", "黄色参考与蓝色载入差异可独立关闭，参考显示不改变快照", () =>
        {
            Capture("Light.Both", true, true);
            SetOption("HighlightReference", false);
            Capture("Light.LoadedOnly", false, true);
            SetOption("HighlightModified", false);
            Capture("Light.Neither", false, false);
            SetOption("HighlightReference", true);
            Capture("Light.ReferenceOnly", true, false);
            SetOption("HighlightModified", true);
            SetOption("ShowReference", false);
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation.Dialog.1.Reference")) is not { IsOffscreen: false }, "参考隐藏");
            Assert.Equal("载入译文甲。", Field("Loaded").Text);
            Capture("Light.ReferenceHidden", false, true);
            SetOption("ShowReference", true);
            Assert.Equal("参考译文甲。", Field("Reference").Text);
        });
        app.Step("主题切换", "实际主题切换后参考和载入快照不变", () =>
        {
            foreach (var (label, name) in new[] { ("暗色", "Dark"), ("高对比度", "HighContrast") })
            {
                app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
                var theme = app.Finder.ById(app.Window!, "Settings.Field.CurrentApplicationTheme").AsComboBox();
                theme.Focus(); theme.Expand();
                WaitHelper.Until(() => theme.Items.FirstOrDefault(item => item.Text == label), "真实主题选项").Patterns.SelectionItem.Pattern.Select();
                theme.Collapse();
                app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
                Assert.Equal(revised, Normalize(Edit(1).Text));
                Assert.Equal("参考译文甲。", Field("Reference").Text);
                Assert.Equal("载入译文甲。", Field("Loaded").Text);
                Capture(name + ".Both", true, true, name != "HighContrast");
            }
        });
        app.Step("保存保持载入基准", "磁盘包含三行新译文，保存后仍显示原载入差异", () =>
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_S);
            var confirmation = app.Finder.ById(app.Window!, "Export.OverwriteConfirmation");
            WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("覆盖").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "确认覆盖").AsButton().Invoke();
            WaitHelper.Until(() => new SekaiToolsBase.Story.Translation.TranslationData(loaded).ForScript(new GameScript(script))[0].Body == revised, "三行译文落盘");
            Assert.Equal("载入译文甲。", Field("Loaded").Text);
            Assert.Equal("参考译文甲。", Field("Reference").Text);
            Capture("Saved.Comparison", true, true, false);
        });
        app.Step("退出", "正常关闭已保存工作区", app.Close);

        TextBox Edit(int index) => app.Finder.ById(app.Window!, $"Translation.Dialog.{index}.TargetText").AsTextBox();
        TextBox Field(string suffix) => app.Finder.ById(app.Window!, "Translation.Dialog.1." + suffix).AsTextBox();
        static string Normalize(string text) => text.Replace("\r\n", "\n");
        void Load(string id, string path)
        {
            app.Finder.ById(app.Window!, id).AsButton().Invoke();
            var dialog = app.OwnedFileDialog();
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))), "文件名输入框").AsTextBox().Text = path;
            Keyboard.Type(VirtualKeyShort.RETURN);
        }
        void SetOption(string key, bool value)
        {
            app.Finder.ById(app.Window!, "Translation.OpenEditorOptions").AsButton().Invoke();
            var option = WaitHelper.Until(() => app.Window!.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.Window.Properties.ProcessId.Value).And(cf.ByAutomationId("Translation.Option." + key))), "编辑选项：" + key).AsCheckBox();
            option.IsChecked = value; option.Focus(); Keyboard.Type(VirtualKeyShort.ESCAPE);
        }
        void Capture(string name, bool yellowExpected, bool blueExpected, bool checkColors = true)
        {
            app.Finder.ById(app.Window!, Catalog.LoadScript).Focus();
            Mouse.MoveTo((int)app.Window!.BoundingRectangle.Right - 30, (int)app.Window.BoundingRectangle.Bottom - 30);
            var screenshot = ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), name);
            if (!checkColors) return;
            using var bitmap = new Bitmap(screenshot);
            var bounds = Edit(1).BoundingRectangle;
            var left = (int)(bounds.Left - app.Window.BoundingRectangle.Left) + 4;
            var top = (int)(bounds.Top - app.Window.BoundingRectangle.Top) + 4;
            var yellow = 0; var blue = 0;
            for (var x = left; x < left + bounds.Width - 8; x++)
                for (var y = top; y < top + bounds.Height - 8; y++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.R > 100 && color.G > 40 && color.R > color.G + 20 && color.B < 60) yellow++;
                    if (color.B > 160 && color.G > 65 && color.G < 200 && color.R < 100 && color.B > color.G + 30) blue++;
                }
            Assert.Equal(yellowExpected, yellow > 3);
            Assert.Equal(blueExpected, blue > 3);
        }
    }
}
