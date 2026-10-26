using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using SekaiToolsBase.GameScript;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.ViewModel.Translate;
using SekaiToolsGUI.View.Translate;
using Wpf.Ui.Appearance;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var script = new GameScript
        {
            ScenarioId = "editing-probe",
            TalkData = [new("角色", "原文一", 0), new("角色", "原文二", 1)],
            SpecialEffectData = [new(8, "地点", "", 0, 0), new(8, "地点", "", 0, 0)],
            OriginalEffectIndices = [3, 7],
            Snippets = [new(1, 0, 0, 0, 0), new(1, 1, 0, 1, 0), new(6, 2, 0, 0, 0), new(6, 3, 0, 1, 0)]
        };
        var model = new TranslatePageModel();
        model.LoadScript(script, "missing-script.json");
        var dialogs = model.Events.OfType<LineDialogModel>().ToArray();
        var effects = model.Events.OfType<LineEffectModel>().ToArray();
        var original = dialogs[0].Content.Translated;
        dialogs[0].Content.Translated = "正文修改";
        Check(model.IsDirty && model.CanUndo, "body editing is dirty and undoable");
        model.Undo();
        Check(!model.IsDirty && dialogs[0].Content.Translated == original, "undo returns to clean baseline");
        model.Redo();
        Check(model.IsDirty && dialogs[0].Content.Translated == "正文修改", "redo restores body");
        model.MarkSaved();
        model.Undo();
        Check(model.IsDirty, "undo across save stays dirty");
        model.Redo();
        Check(!model.IsDirty, "redo to save clears dirty");
        dialogs[0].Character.Translated = "角色译名";
        Check(dialogs[1].Character.Translated == "角色译名", "speaker propagation");
        model.Undo();
        Check(dialogs.All(line => line.Character.Translated == "角色"), "one undo reverses all speaker propagation");
        model.Redo();
        effects[0].Content.Translated = "译文地点";
        Check(effects[1].Content.Translated == "译文地点", "effect propagation");
        model.Undo();
        Check(effects.All(line => line.Content.Translated != "译文地点"), "one undo reverses effect propagation");
        dialogs[1].Content.Translated = "新分支";
        Check(!model.CanRedo, "new edit discards redo branch");

        dialogs[0].Content.PreserveEmpty = true;
        dialogs[0].Content.Translated = string.Empty;
        var directory = Path.Combine(Path.GetTempPath(), "SekaiTools-EditingProbe-" + Guid.NewGuid());
        var store = new EditorSessionStore(directory);
        try
        {
            store.SaveLastModule("TranslatePage");
            Check(store.ReadLastModule() == "TranslatePage", "last module persists");
            var modules = new[] { typeof(SekaiToolsGUI.View.Subtitle.SubtitlePage), typeof(TranslatePage),
                typeof(SekaiToolsGUI.View.Download.DownloadPage), typeof(SekaiToolsGUI.View.Suppress.SuppressPage) };
            Check(store.ResolveLastModule(modules) == typeof(TranslatePage), "startup restores saved main module");
            store.RecordModule(typeof(SekaiToolsGUI.View.Setting.SettingPage), modules);
            Check(store.ResolveLastModule(modules) == typeof(TranslatePage), "settings do not overwrite last main module");
            store.SaveLastModule("unknown-page");
            Check(store.ResolveLastModule(modules) == modules[0], "invalid saved module falls back to first main module");
            store.SaveDraft(model);
            var draft = new EditorSessionStore(directory).ReadDraft() ?? throw new Exception("draft missing");
            draft.Script.OriginalEffectIndices = draft.OriginalEffectIndices;
            var restored = new TranslatePageModel();
            restored.RestoreDraft(draft.Script, draft.ScriptPath, draft.TranslationPath, draft.Values, draft.SavedValues);
            Check(restored.IsDirty && restored.ReadValues().SequenceEqual(model.ReadValues()), "draft restores raw edits without source files");
            Check(restored.Export() == model.Export(), "v2 export and original effect indices survive recovery");
            restored.Events.OfType<LineDialogModel>().First().Content.Translated = "恢复后编辑";
            restored.Undo();
            Check(restored.ReadValues().SequenceEqual(model.ReadValues()), "restored draft supports new undo history");
            restored.MarkSaved();
            store.SaveDraft(restored);
            Check(store.ReadDraft() == null, "saving removes draft");
            File.WriteAllText(Path.Combine(directory, "translation-draft.json"), "broken");
            Check(store.ReadDraft() == null, "corrupt draft is ignored");
            store.DeleteDraft();
            CheckUi(store, script);
        }
        finally
        {
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
        model.Clear();
        Check(!model.CanUndo && !model.CanRedo && !model.IsDirty, "clearing document resets history");
        model.LoadScript(script, "probe.json");
        for (var i = 0; i < 510; i++) model.Events.OfType<LineDialogModel>().First().Content.Translated = "修改" + i;
        for (var i = 0; i < 500; i++) model.Undo();
        Check(!model.CanUndo && model.CanRedo, "history is bounded at 500 steps");
        Console.WriteLine("PASS translation editing, linked undo, save baseline, branching, draft recovery, module persistence");
    }

    private static void CheckUi(EditorSessionStore store, GameScript script)
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        var constructor = typeof(TranslatePage).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(EditorSessionStore)], null)!;
        var page = (TranslatePage)constructor.Invoke([store]);
        page.ViewModel.LoadScript(script, "probe.json");
        var window = new Window { Content = page, Width = 1280, Height = 800,
            Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        Pump();
        var undo = Nodes(page).OfType<Wpf.Ui.Controls.Button>().Single(button => Equals(button.ToolTip, "撤销 (Ctrl+Z)"));
        var redo = Nodes(page).OfType<Wpf.Ui.Controls.Button>().Single(button => Equals(button.ToolTip, "重做 (Ctrl+Y / Ctrl+Shift+Z)"));
        Check(!undo.IsEnabled && !redo.IsEnabled, "history buttons initially disabled");
        var line = page.ViewModel.Events.OfType<LineDialogModel>().First();
        var initial = line.Content.Translated;
        line.Content.Translated = "界面编辑";
        Pump();
        Check(undo.IsEnabled && !redo.IsEnabled, "history button binding reacts to edit");
        undo.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Pump();
        Check(line.Content.Translated == initial && redo.IsEnabled, "undo button restores content");
        redo.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Pump();
        Check(line.Content.Translated == "界面编辑", "redo button restores content");
        var editor = Nodes(page).OfType<System.Windows.Controls.TextBox>().First(box =>
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated");
        Check(ApplicationCommands.Undo.CanExecute(null, editor), "textbox menu uses document undo availability");
        ApplicationCommands.Undo.Execute(null, editor);
        Pump();
        Check(line.Content.Translated == initial, "textbox undo command uses document history");
        ApplicationCommands.Redo.Execute(null, editor);
        Pump();
        Check(line.Content.Translated == "界面编辑", "textbox redo command uses document history");
        foreach (var theme in new[] { ApplicationTheme.Dark, ApplicationTheme.Light })
        {
            ApplicationThemeManager.Apply(theme);
            foreach (var width in new[] { 1280.0, 960.0 })
            {
                window.Width = width;
                Pump();
                Check(undo.ActualWidth > 0 && redo.ActualWidth > 0 && undo.IsVisible && redo.IsVisible,
                    $"history icons visible {theme} {width}");
                Check(redo.TransformToAncestor(page).Transform(new Point(redo.ActualWidth, 0)).X <= page.ActualWidth,
                    $"history buttons within page {theme} {width}");
            }
        }
        window.Content = null;
        Pump();
        Check(store.ReadDraft() != null, "leaving page flushes unsaved draft");
        window.Close();
        app.Shutdown();
    }

    private static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Nodes(VisualTreeHelper.GetChild(node, i))) yield return child;
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
