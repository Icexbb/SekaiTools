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
            model.AutoFormat = false;
            model.ExportUsesV2 = false;
            model.ActiveSequenceNumber = 2;
            store.SaveDraft(model);
            var draft = new EditorSessionStore(directory).ReadDraft() ?? throw new Exception("draft missing");
            draft.Script.OriginalEffectIndices = draft.OriginalEffectIndices;
            var restored = new TranslatePageModel();
            restored.RestoreDraft(draft.Script, draft.ScriptPath, draft.TranslationPath, draft.Values, draft.SavedValues,
                draft.UseV2, draft.AutoFormat, draft.ActiveSequenceNumber);
            Check(!restored.ExportUsesV2 && !restored.AutoFormat && restored.ActiveSequenceNumber == 2,
                "draft retains export format, formatting preference and reading position");
            Check(restored.IsDirty && restored.ReadValues().SequenceEqual(model.ReadValues()), "draft restores raw edits without source files");
            Check(restored.Export() == model.Export(), "v2 export and original effect indices survive recovery");
            File.WriteAllText(Path.Combine(directory, "translation-draft.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                draft.Script,
                draft.OriginalEffectIndices,
                draft.ScriptPath,
                draft.TranslationPath,
                draft.Values,
                draft.SavedValues
            }));
            var legacyDraft = store.ReadDraft()!;
            Check(legacyDraft.UseV2 && legacyDraft.AutoFormat && legacyDraft.ActiveSequenceNumber == 0, "older drafts use compatible default options");
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
        var voiceA = new LineDialogModel(SekaiToolsBase.Story.StoryEvent.DialogStoryEvent.FromData(script.TalkData[0]))
        {
            VoiceScenarioId = "probe",
            OriginalVoice = new Voice(1, "voice_a"),
            VoicePlaying = true
        };
        var voiceB = new LineDialogModel(SekaiToolsBase.Story.StoryEvent.DialogStoryEvent.FromData(script.TalkData[1]))
        {
            VoiceScenarioId = "probe",
            OriginalVoice = new Voice(1, "voice_b")
        };
        var actions = new SekaiToolsGUI.View.Translate.Components.TranslateLineActions { DataContext = voiceA };
        actions.Measure(new Size(100, 150));
        actions.Arrange(new Rect(0, 0, 100, 150));
        Pump();
        var voiceButton = Nodes(actions).OfType<Wpf.Ui.Controls.Button>().Single(button =>
            System.Windows.Automation.AutomationProperties.GetName(button) == "原始角色语音预览");
        Check(voiceButton.IsEnabled && ((Wpf.Ui.Controls.SymbolIcon)voiceButton.Content).Symbol == Wpf.Ui.Controls.SymbolRegular.Pause24,
            "voice button reflects playing model");
        LineDialogModel? requested = null;
        actions.AddHandler(SekaiToolsGUI.View.Translate.Components.TranslateLineActions.VoiceRequestedEvent,
            new RoutedEventHandler((_, e) => requested = e.OriginalSource as LineDialogModel));
        actions.DataContext = voiceB;
        Pump();
        voiceButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(ReferenceEquals(requested, voiceB) && ((Wpf.Ui.Controls.SymbolIcon)voiceButton.Content).Symbol == Wpf.Ui.Controls.SymbolRegular.Play24,
            "recycled voice control targets current row and resets icon");
        actions.DataContext = new LineDialogModel(SekaiToolsBase.Story.StoryEvent.DialogStoryEvent.FromData(script.TalkData[0]));
        Pump();
        Check(voiceButton.Visibility == Visibility.Collapsed, "voice button hides after recycling to row without voice");
        actions.DataContext = voiceB;
        Pump();
        Check(voiceButton.Visibility == Visibility.Visible, "voice button returns after recycling to row with voice");
        var constructor = typeof(TranslatePage).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(EditorSessionStore)], null)!;
        var page = (TranslatePage)constructor.Invoke([store]);
        page.ViewModel.LoadScript(script, "probe.json");
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 800,
            Left = -15000,
            Top = -15000,
            ShowActivated = false,
            ShowInTaskbar = false
        };
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
        var options = (WrapPanel)page.FindName("EditorOptionsPanel");
        Check(options.IsVisible && options.Children.Count == 4, "four editor options are directly visible");
        var optionCheck = options.Children.OfType<CheckBox>().First();
        optionCheck.IsChecked = false;
        Pump();
        Check(!page.ViewModel.ShowReference, "inline editor options binding updates project");
        Check(Nodes(page).OfType<TextBlock>().Any(block => block.Text.Contains("句尾缺少结束标点")), "validation reason is visible without hover");
        line.RequestAction("blank");
        Pump();
        Check(line.IsExplicitEmpty && page.ViewModel.ExplicitEmptyCount == 1, "explicit blank action updates progress");
        var beforeFilter = page.ViewModel.Export();
        page.ViewModel.FilterIndex = 1;
        Pump();
        Check(page.ViewModel.VisibleEvents.Length == 3, "untranslated filter excludes intentional blank");
        Check(page.ViewModel.Export() == beforeFilter, "filter does not alter export");
        page.ViewModel.SearchText = "not-present";
        Pump();
        Check(Nodes(page).OfType<TextBlock>().Any(block => block.IsVisible && block.Text.Contains("没有符合条件")), "empty search gives visible recovery guidance");
        page.ViewModel.SearchText = string.Empty;
        page.ViewModel.FilterIndex = 0;
        Pump();
        var saveDialog = new SekaiToolsGUI.View.Translate.Components.SaveFileDialog(new Wpf.Ui.Controls.ContentDialogHost(),
            "probe.json", translation: page.ViewModel);
        Check(((TextBlock)saveDialog.FindName("PreflightSummary")).Text.Contains("已标为留空"), "save dialog shows blank preflight");
        ((ComboBox)saveDialog.FindName("FormatSelector")).SelectedIndex = 1;
        Check(((TextBlock)saveDialog.FindName("PreflightSummary")).Text.Contains("旧 TXT 无法可靠保留"), "preflight follows selected format");
        saveDialog.ViewModel.ApplyTranslationTag("校对");
        Check(((TextBlock)saveDialog.FindName("TargetFileName")).Text == "【校对】probe.txt", "save dialog exposes complete tagged filename");
        page.ViewModel.AutoFormat = true;
        editor = Nodes(page).OfType<System.Windows.Controls.TextBox>().First(box => ReferenceEquals(box.DataContext, line) &&
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated");
        editor.Focus();
        Pump();
        Check(line.DeferFormatting, "focused editor defers formatting during input");
        editor.Text = "欸,你好!";
        Pump();
        Check(line.Content.Translated == "欸,你好!", "focused input remains unmodified");
        ((Wpf.Ui.Controls.TextBox)page.FindName("SearchBox")).Focus();
        Pump();
        Check(line.Content.Translated == "诶，你好！", "leaving editor applies formatting");
        line.Content.Translated = "一\n二\n三";
        Pump();
        var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "\n四"), false, DataFormats.UnicodeText);
        editor.Select(editor.Text.Length, 0);
        editor.RaiseEvent(paste);
        Pump();
        Check(paste.CommandCancelled && line.InputHint.Contains("最多三行"), "multiline paste is rejected with visible guidance");
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
        var location = page.ViewModel.Events.OfType<LineEffectModel>().First();
        location.ShowReference = true;
        location.Content.ReferenceAvailable = true;
        location.Content.Reference = "地点参考第一行\n地点参考第二行\n地点参考最后一行";
        var locationView = new SekaiToolsGUI.View.Translate.Components.TranslateLineEffect { LineEffectModel = location };
        window.Content = locationView;
        Pump();
        var referenceBox = Nodes(locationView).OfType<System.Windows.Controls.TextBox>().Single(box => box.IsReadOnly);
        Check(referenceBox.IsVisible && referenceBox.ActualHeight > 60, "location reference expands for multiline text and watermark");
        var lastCharacter = referenceBox.GetRectFromCharacterIndex(referenceBox.Text.Length - 1);
        Check(!lastCharacter.IsEmpty && lastCharacter.Bottom <= referenceBox.ActualHeight - referenceBox.Padding.Bottom + 1,
            "last line of location reference is not clipped");
        location.ShowReference = false;
        Pump();
        Check(!referenceBox.IsVisible, "location reference follows visibility switch");
        var discard = typeof(TranslatePage).GetMethod("DiscardDraftOnClose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Check((bool)discard.Invoke(page, null)!, "discard on exit removes recovery draft");
        Check(page.FlushDraft() && store.ReadDraft() == null, "close flush cannot recreate discarded draft");
        window.Close();
        Check(store.ReadDraft() == null, "unload cannot recreate discarded draft");
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
