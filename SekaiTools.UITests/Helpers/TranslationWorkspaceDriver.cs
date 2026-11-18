using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiToolsBase.GameScript;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;

namespace SekaiTools.UITests.Helpers;

internal sealed class TranslationWorkspaceDriver(ApplicationFixture app)
{
    public GameScript Script { get; } = new()
    {
        ScenarioId = "ui-workspace-behavior",
        TalkData = [new("角色", "原文甲。", 0), new("角色", "原文乙。", 1)],
        Snippets = [new() { Action = 1 }, new() { Action = 1, ReferenceIndex = 1 }]
    };
    public string ScriptPath => Path.Combine(app.Data.Root, "behavior.json");
    public void Start()
    {
        File.WriteAllText(ScriptPath, JsonSerializer.Serialize(Script));
        app.Start(); Load(Catalog.LoadScript, ScriptPath); Body(2);
    }
    public TextBox Body(int index) => app.Finder.ById(app.Window!, $"Translation.Dialog.{index}.TargetText").AsTextBox();
    public TextBox Speaker(int index) => app.Finder.ById(app.Window!, $"Translation.Dialog.{index}.Speaker").AsTextBox();
    public Button Button(string id) => app.Finder.ById(app.Window!, id).AsButton();
    public void Load(string id, string path)
    {
        Button(id).Invoke();
        var dialog = app.OwnedFileDialog();
        WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "文件名输入框").AsTextBox().Text = path;
        Keyboard.Type(VirtualKeyShort.RETURN);
    }
    public void Confirm(string text, string title)
    {
        var dialog = WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window).And(cf.ByName(title))), "当前对话框：" + title);
        WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(text))), "确认操作：" + text).AsButton().Invoke();
    }
    public void Clear(string label)
    {
        Button("Translation.ClearButton").Invoke();
        Owned("", label, ControlType.MenuItem).AsMenuItem().Invoke();
    }
    public AutomationElement Owned(string id, string? name = null, ControlType? type = null) => WaitHelper.Until(() =>
        app.Window!.Automation.GetDesktop().FindFirstDescendant(cf =>
        {
            ConditionBase condition = cf.ByProcessId(app.Window.Properties.ProcessId.Value);
            if (id.Length > 0) condition = condition.And(cf.ByAutomationId(id));
            if (name != null) condition = condition.And(cf.ByName(name));
            if (type != null) condition = condition.And(cf.ByControlType(type.Value));
            return condition;
        }), "自有控件：" + (name ?? id));
    public void Option(string key, bool value)
    {
        Button("Translation.OpenEditorOptions").Invoke();
        var option = Owned("Translation.Option." + key).AsCheckBox();
        option.IsChecked = value; option.Focus(); Keyboard.Type(VirtualKeyShort.ESCAPE);
    }
    public void Shortcut(VirtualKeyShort key)
    {
        Button(Catalog.LoadScript).Focus();
        using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(key);
    }
}
