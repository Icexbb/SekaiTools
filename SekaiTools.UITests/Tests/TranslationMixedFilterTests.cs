using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story.Translation;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationMixedFilterTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_011_混合类型组合筛选与跨类型定位()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-011", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var script = new GameScript
        {
            ScenarioId = "ui-mixed-filter",
            TalkData = [new("甲", "对话甲。", 0), new("乙", "对话乙。", 1)],
            SpecialEffectData = [new(8, "Cafe", "", 1, 0), new(8, "教室", "", 1, 0)],
            Snippets = [new() { Action = 1 }, new() { Action = 6 }, new() { Action = 1, ReferenceIndex = 1 }, new() { Action = 6, ReferenceIndex = 1 }]
        };
        var path = Path.Combine(app.Data.Root, "mixed.json");
        File.WriteAllText(path, JsonSerializer.Serialize(script));
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        app.Step("载入混合剧本", "对话和地点交错，原始编号保持剧本顺序", () =>
        {
            app.Start(); ui.Load(Catalog.LoadScript, path);
            foreach (var id in new[] { "Dialog.1", "Effect.2", "Dialog.3", "Effect.4" }) Editor(id);
        });
        app.Step("三重组合筛选", "地点、未译、大小写不敏感搜索共同生效，编辑后即时退出未译集合", () =>
        {
            Select("TypeFilter", 2); Select("StatusFilter", 1); Search().Text = "cAfE";
            WaitHelper.Until(() => !Exists("Dialog.1") && !Exists("Dialog.3") && !Exists("Effect.4"), "仅显示原始第二条地点");
            Editor("Effect.2").Focus();
            Editor("Effect.2").Text = "咖啡馆";
            WaitHelper.Until(() => !Exists("Effect.2"), "地点已译后退出未译筛选");
            Select("StatusFilter", 0);
            Assert.Equal("咖啡馆", Editor("Effect.2").Text);
            Search().Text = "咖啡馆";
            Assert.Equal("咖啡馆", Editor("Effect.2").Text);
            Select("TypeFilter", 1);
            WaitHelper.Until(() => !Exists("Effect.2") && !Exists("Dialog.1"), "对话类型与地点译文搜索交集为空");
        });
        app.Step("跨筛选定位", "下一未译解除阻挡的筛选、跨类型按剧本顺序定位并循环", () =>
        {
            ui.Button("Translation.NextUntranslated").Invoke(); Focused("Dialog.3");
            Assert.Equal("", Search().Text);
            Assert.Equal("全部类型", Combo("TypeFilter").SelectedItem!.Text);
            Assert.Equal("全部条目", Combo("StatusFilter").SelectedItem!.Text);
            ui.Button("Translation.NextUntranslated").Invoke(); Focused("Effect.4");
            ui.Button("Translation.NextUntranslated").Invoke(); Focused("Dialog.1");
            Editor("Dialog.1").Text = "甲译文。";
            ui.Button("Translation.NextUntranslated").Invoke(); Focused("Dialog.3");
            Editor("Dialog.3").Text = "乙译文。";
            ui.Button("Translation.NextUntranslated").Invoke(); Focused("Effect.4");
            Editor("Effect.4").Text = "教室译文";
        });
        app.Step("地点筛选导出全篇", "只显示地点时导出仍含四条并保持交错顺序，源JSON不变", () =>
        {
            Select("TypeFilter", 2);
            WaitHelper.Until(() => !Exists("Dialog.1") && !Exists("Dialog.3"), "仅地点");
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Mixed.LocationOnly");
            ui.Shortcut(VirtualKeyShort.KEY_S);
            var options = app.Finder.ById(app.Window!, "Translation.SaveOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))), "保存按钮").AsButton().Invoke();
            var output = Path.ChangeExtension(path, ".txt");
            WaitHelper.Until(() => File.Exists(output), "完整译文落盘");
            var parsed = new TranslationData(output);
            Assert.True(parsed.IsV2);
            Assert.Equal(new[] { "甲译文。", "咖啡馆", "乙译文。", "教室译文" }, parsed.ForScript(script).Select(item => item.Body));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
            app.Close();
        });

        TextBox Editor(string id) => app.Finder.ById(app.Window!, "Translation." + id + ".TargetText").AsTextBox();
        bool Exists(string id) => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Translation." + id + ".TargetText")) != null;
        TextBox Search() => app.Finder.ById(app.Window!, "Translation.SearchBox").AsTextBox();
        ComboBox Combo(string name) => app.Finder.ById(app.Window!, "Translation." + name).AsComboBox();
        void Select(string name, int index)
        {
            var combo = Combo(name);
            combo.Expand();
            var item = combo.Items[index];
            var label = item.Text;
            item.Patterns.SelectionItem.Pattern.Select();
            combo.Collapse();
            WaitHelper.Until(() => combo.SelectedItem?.Text == label, "筛选已选择：" + label);
        }
        void Focused(string id) => WaitHelper.Until(() => Editor(id).Properties.HasKeyboardFocus.Value, "定位正文：" + id);
    }
}
