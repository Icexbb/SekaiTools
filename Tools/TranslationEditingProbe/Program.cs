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
                draft.UseV2, draft.AutoFormat, draft.ActiveSequenceNumber,
                draft.SyncMatchingContent, draft.SyncMatchingSpeakers, draft.LoadedTranslationValues);
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
        CheckTabNavigation(page);
        var undo = Nodes(page).OfType<Wpf.Ui.Controls.Button>().Single(button => Equals(button.ToolTip, "撤销 (Ctrl+Z)"));
        var redo = Nodes(page).OfType<Wpf.Ui.Controls.Button>().Single(button => Equals(button.ToolTip, "重做 (Ctrl+Y / Ctrl+Shift+Z)"));
        Check(!undo.IsEnabled && !redo.IsEnabled, "history buttons initially disabled");
        var playbackGate = typeof(TranslatePage).GetMethod("SetVoicePlaybackActive", BindingFlags.Instance | BindingFlags.NonPublic)!;
        playbackGate.Invoke(page, [true]);
        Pump();
        Check(page.ViewModel.Events.All(row => !row.VoicePlaybackAvailable) &&
            Nodes(page).OfType<Wpf.Ui.Controls.Button>().Where(button =>
                System.Windows.Automation.AutomationProperties.GetName(button) == "原始角色语音预览").All(button => !button.IsEnabled),
            "playback disables every row voice button");
        typeof(TranslatePage).GetMethod("StopVoice", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
        Pump();
        Check(page.ViewModel.Events.All(row => row.VoicePlaybackAvailable), "stopping playback restores every row voice button");
        voiceB.VoiceProgress = 0.5;
        Pump();
        var progressBorder = Nodes(actions).OfType<SekaiToolsGUI.View.Translate.Components.VoiceProgressBorder>().Single();
        Check(progressBorder.Progress == 0.5 && !progressBorder.IsHitTestVisible, "button perimeter binds playback progress without intercepting clicks");
        var border = new SekaiToolsGUI.View.Translate.Components.VoiceProgressBorder { Width = 34, Height = 34, Progress = 0.5 };
        border.Measure(new Size(34, 34));
        border.Arrange(new Rect(0, 0, 34, 34));
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(34, 34, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(border);
        var pixels = new byte[34 * 34 * 4];
        bitmap.CopyPixels(pixels, 34 * 4, 0);
        Check(pixels.Any(value => value != 0), "playback perimeter renders visible progress");
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
        var referenceBox = Nodes(locationView).OfType<System.Windows.Controls.TextBox>().Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == "参考译文");
        Check(referenceBox.IsVisible && referenceBox.ActualHeight < 32, "location reference uses only one text line of height");
        var firstCharacter = referenceBox.GetRectFromCharacterIndex(0);
        var endOfFirstLine = referenceBox.GetRectFromCharacterIndex(location.Content.Reference.IndexOf('\n') - 1, true);
        Check(!firstCharacter.IsEmpty && !endOfFirstLine.IsEmpty &&
            Math.Abs((firstCharacter.Left + endOfFirstLine.Right) / 2 - referenceBox.ActualWidth / 2) < 2,
            "location reference first line is centered like input text");
        var referenceHeight = referenceBox.ActualHeight;
        location.Content.Reference = new string('长', 1000) + "\n第二行\n第三行";
        Pump();
        Check(Math.Abs(referenceBox.ActualHeight - referenceHeight) < 1 &&
            Equals(referenceBox.ToolTip, location.Content.Reference), "long multiline location reference does not grow and full text remains in tooltip");
        location.ShowReference = false;
        Pump();
        Check(!referenceBox.IsVisible, "location reference follows visibility switch");
        var discard = typeof(TranslatePage).GetMethod("DiscardDraftOnClose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Check((bool)discard.Invoke(page, null)!, "discard on exit removes recovery draft");
        Check(page.FlushDraft() && store.ReadDraft() == null, "close flush cannot recreate discarded draft");
        CheckReferenceDifferences(window, script);
        CheckLoadedDifferences(window, script);
        CheckNewlineMarkers(window);
        CheckCloseDialog(window);
        window.Close();
        Check(store.ReadDraft() == null, "unload cannot recreate discarded draft");
        app.Shutdown();
    }

    private static void CheckTabNavigation(TranslatePage page)
    {
        var lines = page.ViewModel.Events;
        var first = Editor(lines[0]);
        var second = Editor(lines[1]);
        first.Focus();
        Check(Tab(first).Handled, "body Tab is handled by row navigation");
        Pump();
        Check(ReferenceEquals(Keyboard.FocusedElement, second), "body Tab skips row actions and names to next body");
        var name = Nodes(page).OfType<System.Windows.Controls.TextBox>().Single(box => ReferenceEquals(box.DataContext, lines[0]) &&
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Character.Translated");
        name.Focus();
        Tab(name);
        Pump();
        Check(ReferenceEquals(Keyboard.FocusedElement, second), "name Tab goes to next row body rather than current row body");
        Tab(second);
        Pump();
        Check(ReferenceEquals(Keyboard.FocusedElement, Editor(lines[2])), "Tab includes next visible scene text body");
        var last = Editor(lines[^1]);
        last.Focus();
        Check(Tab(last).Handled, "last body consumes Tab");
        Pump();
        Check(ReferenceEquals(Keyboard.FocusedElement, last), "last body retains focus without wrapping to toolbar");
        page.ViewModel.SearchText = "地点";
        Pump();
        var effect = Editor(lines[2]);
        effect.Focus();
        Tab(effect);
        Pump();
        Check(ReferenceEquals(Keyboard.FocusedElement, Editor(lines[3])), "Tab follows filtered visible body order");
        page.ViewModel.SearchText = string.Empty;
        Pump();
        var search = (Wpf.Ui.Controls.TextBox)page.FindName("SearchBox");
        search.Focus();
        Check(!Tab(search).Handled, "search field retains normal Tab handling");
        first = Editor(lines[0]);
        first.Focus();
        Pump();

        System.Windows.Controls.TextBox Editor(LineModel line) => Nodes(page).OfType<System.Windows.Controls.TextBox>().Single(box => ReferenceEquals(box.DataContext, line) &&
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated");
        static KeyEventArgs Tab(System.Windows.Controls.TextBox box)
        {
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box)!, Environment.TickCount, Key.Tab)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            box.RaiseEvent(key);
            return key;
        }
    }

    private static void CheckNewlineMarkers(Window window)
    {
        var box = new System.Windows.Controls.TextBox
        {
            Text = "第一行\r\n\r\n末行\n",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = 240,
            Height = 180,
            FontSize = 18,
            Padding = new Thickness(10, 2, 10, 2)
        };
        window.Content = new System.Windows.Documents.AdornerDecorator { Child = box };
        Pump();
        box.Focus();
        box.Select(2, 3);
        var text = box.Text;
        var start = box.SelectionStart;
        var length = box.SelectionLength;
        var caret = box.CaretIndex;
        var geometry = box.GetRectFromCharacterIndex(2);
        foreach (var theme in new[] { ApplicationTheme.Light, ApplicationTheme.Dark })
        {
            ApplicationThemeManager.Apply(theme);
            foreach (var style in new[] { 1, 2, 0 })
            {
                SekaiToolsGUI.Behaviors.NewlineMarker.SetStyle(box, style);
                Pump();
                var adorners = System.Windows.Documents.AdornerLayer.GetAdornerLayer(box).GetAdorners(box);
                Check(style == 0 ? adorners == null || adorners.Length == 0 :
                    adorners is { Length: 1 } && !adorners[0].IsHitTestVisible && !adorners[0].Focusable,
                    $"newline marker input transparency {theme}/{style}");
                if (style != 0)
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(Directory.GetCurrentDirectory(),
                        ".codex-build", "reference-diff", $"newline-{theme}-{style}.png"));
                    encoder.Save(stream);
                }
                Check(box.Text == text && box.SelectionStart == start && box.SelectionLength == length &&
                    box.CaretIndex == caret && box.GetRectFromCharacterIndex(2) == geometry,
                    "newline markers preserve text, selection, caret and layout");
            }
        }
        SekaiToolsGUI.Behaviors.NewlineMarker.SetStyle(box, 2);
        box.Text = new string('长', 100);
        Pump();
        Check(System.Windows.Documents.AdornerLayer.GetAdornerLayer(box).GetAdorners(box) == null,
            "automatic wrapping does not create newline markers");
        box.Text = "换行\n";
        Pump();
        Check(System.Windows.Documents.AdornerLayer.GetAdornerLayer(box).GetAdorners(box)?.Length == 1,
            "typing a newline recreates overlay");
        var setting = new SekaiToolsGUI.Model.Setting { TranslationNewlineMarker = 2 };
        Check(System.Text.Json.JsonSerializer.Deserialize<SekaiToolsGUI.Model.Setting>(setting.Dump())
            .TranslationNewlineMarker == 2, "newline marker setting persists");
        Check(SekaiToolsGUI.View.Setting.SettingCatalog.Load().SelectMany(section => section.Fields)
            .Any(field => field.Property == "TranslationNewlineMarker" && field.Options.Length == 3),
            "newline marker choices and settings icon are valid");
    }

    private static void CheckLoadedDifferences(Window window, GameScript script)
    {
        var model = new TranslatePageModel { AutoFormat = false };
        model.LoadScript(script, "loaded-probe.json");
        var data = new SekaiToolsBase.Story.Translation.TranslationData(null);
        data.Translations.AddRange([
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "今天一起休息吧！"),
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "第二句相同。"),
            new SekaiToolsBase.Story.Translation.EffectTranslate("学校屋顶"),
            new SekaiToolsBase.Story.Translation.EffectTranslate("学校屋顶")]);
        model.LoadTranslation(data, "loaded-probe.txt");
        var line = (LineDialogModel)model.Events[0];
        var effect = model.Events.OfType<LineEffectModel>().First();
        var view = new SekaiToolsGUI.View.Translate.Components.TranslateLineDialog { LineDialogModel = line };
        var effectView = new SekaiToolsGUI.View.Translate.Components.TranslateLineEffect { LineEffectModel = effect };
        var panel = new StackPanel { Margin = new Thickness(16) };
        var title = new TextBlock { Text = "黄色框：参考差异 · 蓝色框：相对载入译文的修改", Margin = new Thickness(4, 12, 4, 20) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(title);
        panel.Children.Add(view);
        panel.Children.Add(effectView);
        var background = new Border { Child = panel };
        background.SetResourceReference(Border.BackgroundProperty, "ApplicationBackgroundBrush");
        window.Content = background;
        Pump();
        var editor = Nodes(view).OfType<System.Windows.Controls.TextBox>().Single(box => box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated");
        Check(LoadedRects(editor) == 0, "newly loaded translation has no modification highlights");
        var loadedBox = Nodes(view).OfType<System.Windows.Controls.TextBox>().Single(box =>
            System.Windows.Automation.AutomationProperties.GetName(box) == "载入时译文");
        Check(!loadedBox.IsVisible, "unchanged row hides load-time comparison");
        line.Content.Translated = "今天一起练习吧！";
        effect.Content.Translated = "学校天台";
        Pump();
        Check(LoadedRects(editor) > 0, "editing loaded translation highlights without any reference");
        Check(loadedBox.IsVisible && loadedBox.Text == "今天一起休息吧！" && LoadedRects(loadedBox) > 0,
            "changed row shows original loaded text above editor with blue differences");
        Check(loadedBox.Padding == editor.Padding && loadedBox.Margin == editor.Margin,
            "load-time text and editor have identical text insets");
        Check(loadedBox.ActualHeight < 32, "single-line load-time text has no extra label row");
        var compactHeight = loadedBox.ActualHeight;
        line.Content.LoadedText = "第一行\n第二行\n第三行";
        Pump();
        Check(loadedBox.ActualHeight > compactHeight * 2 && loadedBox.ActualHeight < compactHeight + loadedBox.FontSize * 4,
            "load-time comparison grows automatically with actual text lines");
        line.Content.LoadedText = "今天一起休息吧！";
        Pump();
        Check(Math.Abs(loadedBox.ActualHeight - compactHeight) < 1, "comparison height shrinks back with single-line text");
        Check(loadedBox.TransformToAncestor(view).Transform(new Point()).Y < editor.TransformToAncestor(view).Transform(new Point()).Y,
            "load-time text is positioned above current editor");
        var loadedEffectBox = Nodes(effectView).OfType<System.Windows.Controls.TextBox>().Single(box =>
            System.Windows.Automation.AutomationProperties.GetName(box) == "载入时译文");
        Check(loadedEffectBox.IsVisible && loadedEffectBox.Text == "学校屋顶" && loadedEffectBox.MaxLines == 1 &&
            loadedEffectBox.TextAlignment == TextAlignment.Center, "location load-time comparison remains one centered line");
        data.Translations[0] = new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "今天一起散步吧！");
        data.Translations[2] = new SekaiToolsBase.Story.Translation.EffectTranslate("学校礼堂");
        data.Translations[3] = new SekaiToolsBase.Story.Translation.EffectTranslate("学校礼堂");
        model.LoadReferenceTranslation(data, "reference.txt");
        Pump();
        foreach (var theme in new[] { ApplicationTheme.Dark, ApplicationTheme.Light })
        {
            ApplicationThemeManager.Apply(theme);
            Pump();
            var adorner = Adorner(editor)!;
            var type = adorner.GetType();
            var referenceBrush = (Brush)adorner.GetValue((DependencyProperty)type.GetField("HighlightBrushProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!);
            var loadedBrush = (Brush)adorner.GetValue((DependencyProperty)type.GetField("LoadedBrushProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!);
            Check(referenceBrush.ToString() != loadedBrush.ToString() && LoadedRects(editor) > 0, $"loaded and reference differences have distinct colors {theme}");
            var referenceBox = Nodes(view).OfType<System.Windows.Controls.TextBox>().Single(box =>
                System.Windows.Automation.AutomationProperties.GetName(box) == "参考译文");
            Check(referenceBox.Padding == editor.Padding && referenceBox.Margin == editor.Margin &&
                Math.Abs(referenceBox.GetRectFromCharacterIndex(0).Left - editor.GetRectFromCharacterIndex(0).Left) < 1,
                $"reference and editor text align {theme}");
            foreach (var row in new FrameworkElement[] { view, effectView })
                foreach (var label in new[] { "参考译文", "载入时译文" })
                {
                    var mark = Nodes(row).OfType<TextBlock>().Single(block => block.Text == label);
                    Check(mark.IsVisible && !mark.IsHitTestVisible && mark.ActualHeight > 0,
                        $"source watermark visible without intercepting input {label} {theme}");
                    var parent = (FrameworkElement)VisualTreeHelper.GetParent(mark);
                    var bounds = mark.TransformToAncestor(parent).TransformBounds(new Rect(mark.RenderSize));
                    Check(bounds.Bottom <= parent.ActualHeight + 1 && bounds.Right <= parent.ActualWidth + 1,
                        $"source watermark stays within compact row {label} {theme}");
                }
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.GetFullPath($".codex-build/reference-diff/loaded-{theme}.png"));
            encoder.Save(stream);
        }
        line.ShowReference = false;
        model.ClearReference();
        Pump();
        Check(LoadedRects(editor) > 0, "loaded differences survive hiding and removing reference");
        model.MarkSaved();
        Check(line.Content.LoadedText == "今天一起休息吧！", "saving does not replace load-time baseline");
        line.Content.Translated += "继续修改";
        var sessions = new EditorSessionStore(Path.GetFullPath(".codex-build/reference-diff/loaded-draft"));
        Check(sessions.SaveDraft(model), "draft persists load-time baseline");
        var draft = sessions.ReadDraft()!;
        var restored = new TranslatePageModel();
        restored.RestoreDraft(draft.Script, draft.ScriptPath, draft.TranslationPath, draft.Values, draft.SavedValues,
            loadedTranslationValues: draft.LoadedTranslationValues);
        Check(((LineDialogModel)restored.Events[0]).Content.LoadedText == "今天一起休息吧！", "disk draft restores original loaded text after an intermediate save");
        line.Content.Translated = line.Content.LoadedText!;
        Pump();
        Check(LoadedRects(editor) == 0, "restoring loaded text clears modification highlight");
        Check(!loadedBox.IsVisible, "restoring loaded text hides load-time comparison");
        sessions.DeleteDraft();

        static System.Windows.Documents.Adorner? Adorner(System.Windows.Controls.TextBox box) =>
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(box)?.GetAdorners(box)?.SingleOrDefault(item => item.GetType().Name == "DifferenceAdorner");
        static int LoadedRects(System.Windows.Controls.TextBox box) => Adorner(box) is { } adorner
            ? ((IEnumerable<Rect>)adorner.GetType().GetField("_loadedRectangles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(adorner)!).Count() : 0;
    }

    private static void CheckCloseDialog(Window window)
    {
        var factory = typeof(TranslatePage).GetMethod("CreateCloseDialog", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var output = Path.GetFullPath(".codex-build/reference-diff");
        var host = new Wpf.Ui.Controls.ContentDialogHost();
        window.Content = host;
        foreach (var theme in new[] { ApplicationTheme.Dark, ApplicationTheme.Light })
        {
            ApplicationThemeManager.Apply(theme);
            foreach (var choice in new[] { "保存", "不保存", "取消" })
            {
                var dialog = (Wpf.Ui.Controls.ContentDialog)factory.Invoke(null, null)!;
                dialog.DialogHostEx = host;
                var result = dialog.ShowAsync(CancellationToken.None);
                Pump();
                var buttons = Nodes(dialog).OfType<Wpf.Ui.Controls.Button>().Where(button => button.IsVisible).ToArray();
                Check(buttons.Select(button => button.Content?.ToString()).Order().SequenceEqual(new[] { "不保存", "保存", "取消" }.Order()), $"only three native footer buttons {theme}");
                Check(dialog.Content is string && dialog.DefaultButton == Wpf.Ui.Controls.ContentDialogButton.Close, "close confirmation uses message-only body and cancel default");
                Check(buttons.All(button => button.TransformToAncestor(dialog).Transform(new Point()).Y > dialog.ActualHeight / 2), "all close actions are in dialog footer");
                if (choice == "取消")
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"close-dialog-{theme}.png"));
                    encoder.Save(stream);
                }
                var selected = buttons.Single(button => Equals(button.Content, choice));
                var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(selected);
                ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
                var frame = new DispatcherFrame();
                var deadline = DateTime.UtcNow.AddSeconds(2);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                timer.Tick += (_, _) => { if (result.IsCompleted || DateTime.UtcNow >= deadline) frame.Continue = false; };
                timer.Start();
                Dispatcher.PushFrame(frame);
                timer.Stop();
                var expected = choice == "保存" ? Wpf.Ui.Controls.ContentDialogResult.Primary : choice == "不保存" ? Wpf.Ui.Controls.ContentDialogResult.Secondary : Wpf.Ui.Controls.ContentDialogResult.None;
                Check(result.IsCompletedSuccessfully && result.Result == expected, $"footer {choice} returns correct result {theme}: {result.Status}/{(result.IsCompletedSuccessfully ? result.Result.ToString() : "pending")}");
            }
        }
        SynchronizationContext.SetSynchronizationContext(previousContext);
    }

    private static void CheckReferenceDifferences(Window window, GameScript script)
    {
        var model = new TranslatePageModel { AutoFormat = false };
        model.LoadScript(script, "reference-diff-probe.json");
        var line = model.Events.OfType<LineDialogModel>().First();
        var other = model.Events.OfType<LineDialogModel>().Last();
        var effect = model.Events.OfType<LineEffectModel>().First();
        const string current = "今天一起练习吧！\n我们明天见，再把这份心意传递给大家。";
        const string reference = "今天一起休息吧！\n我们明天见，再把这份心意传递给大家。";
        line.Content.Translated = current;
        other.Content.Translated = "这一句完全相同。";
        effect.Content.Translated = "学校屋顶";
        var data = new SekaiToolsBase.Story.Translation.TranslationData(null);
        data.Translations.AddRange([
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", reference),
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "这一句完全相同。"),
            new SekaiToolsBase.Story.Translation.EffectTranslate("学校天台"),
            new SekaiToolsBase.Story.Translation.EffectTranslate("学校天台")]);
        model.LoadReferenceTranslation(data, "reference-probe.txt");
        var dialogView = new SekaiToolsGUI.View.Translate.Components.TranslateLineDialog { LineDialogModel = line };
        var sameView = new SekaiToolsGUI.View.Translate.Components.TranslateLineDialog { LineDialogModel = other };
        var effectView = new SekaiToolsGUI.View.Translate.Components.TranslateLineEffect { LineEffectModel = effect };
        var panel = new StackPanel { Margin = new Thickness(16) };
        var heading = new TextBlock { Text = "参考译文差异 · 细框标出不同文字", FontSize = 20, Margin = new Thickness(4, 8, 4, 20) };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(heading);
        panel.Children.Add(dialogView);
        panel.Children.Add(sameView);
        panel.Children.Add(effectView);
        var background = new Border { Child = panel };
        background.SetResourceReference(Border.BackgroundProperty, "ApplicationBackgroundBrush");
        window.Content = background;
        window.Height = 650;
        Pump();
        var boxes = Nodes(dialogView).OfType<System.Windows.Controls.TextBox>().ToArray();
        var referenceBox = boxes.Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == "参考译文");
        var editor = boxes.Single(box => box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated");
        Check(Highlight(referenceBox) != null && Highlight(editor) != null, "reference load highlights both text sides");
        Check(!Nodes(sameView).OfType<System.Windows.Controls.TextBox>().Any(box => Highlight(box) != null), "identical row has no highlight");
        Check(Nodes(effectView).OfType<System.Windows.Controls.TextBox>().Count(box => Highlight(box) != null) == 2, "location text highlights both sides");
        editor.Select(1, 3);
        Pump();
        Check(editor.SelectionLength == 3 && Highlight(editor)!.IsHitTestVisible == false, "difference overlay preserves selection and input hit testing");
        editor.Select(0, 0);
        var output = Path.GetFullPath(".codex-build/reference-diff");
        Directory.CreateDirectory(output);
        foreach (var theme in new[] { ApplicationTheme.Dark, ApplicationTheme.Light })
        {
            ApplicationThemeManager.Apply(theme);
            foreach (var width in new[] { 1280d, 960d })
            {
                window.Width = width;
                Pump();
                var adorners = new[] { Highlight(referenceBox)!, Highlight(editor)! };
                Check(adorners.All(adorner => ((IEnumerable<Rect>)adorner.GetType().GetField("_rectangles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adorner)!).Any(rect => rect.Width > 0 && rect.Height > 0)), $"visible difference geometry {theme} {width}");
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"reference-{theme}-{width}.png"));
                encoder.Save(stream);
            }
        }
        line.RequestAction("reference");
        Pump();
        Check(line.Content.Translated == reference && Highlight(referenceBox) == null && Highlight(editor) == null, "adopting reference clears equal-text highlights");
        model.Undo();
        Pump();
        Check(line.Content.Translated == current && Highlight(referenceBox) != null && Highlight(editor) != null, "undo restores difference highlights");
        line.ShowReference = false;
        Pump();
        Check(Highlight(editor) == null, "hiding reference also hides current-text highlight");
        line.ShowReference = true;
        Pump();
        Check(Highlight(editor) != null, "showing reference restores current-text highlight");
        dialogView.LineDialogModel = other;
        Pump();
        Check(!Nodes(dialogView).OfType<System.Windows.Controls.TextBox>().Any(box => Highlight(box) != null), "recycling row clears obsolete highlights");
        dialogView.LineDialogModel = line;
        Pump();
        line.Content.Reference = new string('参', 90) + "参考";
        line.Content.Translated = new string('参', 90) + "当前";
        Pump();
        Check(referenceBox.LineCount > 1 && editor.LineCount > 1, "difference text wraps into multiple visual lines");
        var wrapped = (IEnumerable<Rect>)Highlight(referenceBox)!.GetType().GetField("_rectangles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Highlight(referenceBox))!;
        Check(wrapped.Any(rect => rect.Y > referenceBox.FontSize) && wrapped.All(rect => rect.Bottom <= referenceBox.ActualHeight - referenceBox.Padding.Bottom + 1), "wrapped differences follow final visual line without clipping");
        editor.MaxHeight = 45;
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Pump();
        editor.ScrollToEnd();
        Pump();
        var scrolled = (IEnumerable<Rect>)Highlight(editor)!.GetType().GetField("_rectangles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Highlight(editor))!;
        Check(scrolled.Any(rect => rect.Y >= 0 && rect.Y < editor.ActualHeight), "difference geometry follows editor scrolling");
        line.Content.Reference = "";
        Pump();
        Check(Highlight(referenceBox) != null, "empty reference retains insertion boundary marker");
        model.ClearReference();
        Pump();
        Check(!Nodes(panel).OfType<System.Windows.Controls.TextBox>().Any(box => Highlight(box) != null), "removing reference clears all difference overlays");

        static System.Windows.Documents.Adorner? Highlight(System.Windows.Controls.TextBox box) =>
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(box)?.GetAdorners(box)?.SingleOrDefault(adorner => adorner.GetType().Name == "DifferenceAdorner");
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
