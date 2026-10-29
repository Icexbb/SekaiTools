using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.ViewModel.Translate;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsConfiguration;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using SaveFileDialog = SekaiToolsGUI.View.Translate.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage : UserControl
{
    private readonly EditorSessionStore _sessions;
    private readonly DispatcherTimer _draftTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool _draftChecked;
    private bool _restoringDraft;
    private bool _isLoadingScript;
    private string? _dragTranslationPath;
    private Story? _dragStory;
    private TranslationData? _dragTranslation;
    private string? _dragTranslationError;
    private CancellationTokenSource? _flashbackLookup;
    private readonly FlashbackEventService _flashbackEvents = new();
    private LineModel? _activeLine;
    private bool _isSaving;
    private string? _acceptedPreflight;

    public TranslatePage() : this(new EditorSessionStore()) { }

    internal TranslatePage(EditorSessionStore sessions)
    {
        _sessions = sessions;
        InitializeComponent();
        DataContext = new TranslatePageModel();
        ViewModel.NavigationRequested += (_, line) => NavigateToLine(line);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TranslatePageModel.ScriptPath)
                or nameof(TranslatePageModel.TranslationPath)
                or nameof(TranslatePageModel.ReferenceTranslationPath))
                UpdateLoadButtons();
            if (e.PropertyName == nameof(TranslatePageModel.ScriptPath))
                UpdateFlashbackOrigins();
        };
        UpdateLoadButtons();
        _draftTimer.Tick += (_, _) => FlushDraft();
        ViewModel.ContentChanged += (_, _) =>
        {
            if (_restoringDraft) return;
            ViewModel.MarkDraftPending();
            _draftTimer.Stop();
            _draftTimer.Start();
        };
        Loaded += async (_, _) => await OfferRestoreDraftAsync();
        PreviewKeyDown += OnHistoryKeyDown;
        AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler((_, _) =>
        {
            ViewModel.BreakEditGroup();
            Dispatcher.InvokeAsync(ViewModel.BreakEditGroup, DispatcherPriority.Background);
        }));
        AddHandler(CommandManager.PreviewCanExecuteEvent, new CanExecuteRoutedEventHandler((_, e) =>
        {
            if (SearchBox.IsKeyboardFocusWithin) return;
            if (e.Command != ApplicationCommands.Undo && e.Command != ApplicationCommands.Redo) return;
            e.CanExecute = e.Command == ApplicationCommands.Undo ? ViewModel.CanUndo : ViewModel.CanRedo;
            e.Handled = true;
        }));
        AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler((_, e) =>
        {
            if (SearchBox.IsKeyboardFocusWithin) return;
            if (e.Command != ApplicationCommands.Undo && e.Command != ApplicationCommands.Redo) return;
            if (e.Command == ApplicationCommands.Undo) ViewModel.Undo();
            else ViewModel.Redo();
            e.Handled = true;
        }));
    }

    public bool FlushDraft()
    {
        _draftTimer.Stop();
        if (_restoringDraft || !_draftChecked && !ViewModel.HasScript) return true;
        var saved = _sessions.SaveDraft(ViewModel);
        ViewModel.MarkDraftResult(saved);
        if (saved) return true;
        SnackbarService.Show("草稿保存失败", "无法写入本地恢复草稿，请保存翻译文件后再退出。", ControlAppearance.Danger,
            new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        return false;
    }

    private async void OnHistoryKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            (Keyboard.Modifiers & ModifierKeys.Alt) != 0) return;
        if (SearchBox.IsKeyboardFocusWithin && e.Key is Key.Z or Key.Y) return;
        if (e.Key == Key.S)
        {
            e.Handled = true;
            await SaveAsync((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            NavigateNext(false);
        }
        else if (e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            ViewModel.Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y || e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            ViewModel.Redo();
            e.Handled = true;
        }
    }

    private void UndoButton_OnClick(object sender, RoutedEventArgs e) => ViewModel.Undo();
    private void RedoButton_OnClick(object sender, RoutedEventArgs e) => ViewModel.Redo();

    private void TranslatePanel_OnActivity(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(TranslatePanel, source) is System.Windows.Controls.ListViewItem { DataContext: LineModel line })
            _activeLine = line;
        if (_activeLine != null) ViewModel.ActiveSequenceNumber = _activeLine.SequenceNumber;
        if (e.RoutedEvent == Keyboard.GotKeyboardFocusEvent && e.OriginalSource is System.Windows.Controls.TextBox box && !box.IsReadOnly)
        {
            var path = box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path;
            if (_activeLine is { } active && path == "Content.Translated") ViewModel.BeginTextEdit(active.Body);
            else if (_activeLine is LineDialogModel dialog && path == "Character.Translated") ViewModel.BeginTextEdit(dialog.Character);
        }
    }

    private void TranslatePanel_OnLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel.EndTextEdit();

    private void RefreshFilter_OnClick(object sender, RoutedEventArgs e) => ViewModel.RefreshVisibleEvents();
    private void NextUntranslated_OnClick(object sender, RoutedEventArgs e) => NavigateNext(false);
    private void NextWarning_OnClick(object sender, RoutedEventArgs e) => NavigateNext(true);
    private void NavigateNext(bool warnings)
    {
        var next = ViewModel.FindNext(_activeLine, warnings);
        if (next == null)
        {
            SnackbarService.Show("已检查", warnings ? "没有需要处理的提示。" : "没有未译条目。", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            return;
        }
        NavigateToLine(next);
    }

    private void NavigateToLine(LineModel next)
    {
        _activeLine = next;
        ViewModel.ActiveSequenceNumber = next.SequenceNumber;
        TranslatePanel.ScrollIntoView(next);
        TranslatePanel.SelectedItem = next;
        Dispatcher.InvokeAsync(() =>
        {
            if (TranslatePanel.ItemContainerGenerator.ContainerFromItem(next) is not DependencyObject item) return;
            FindEditor(item)?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private static System.Windows.Controls.TextBox? FindEditor(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.TextBox box &&
                box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.ParentBinding.Path?.Path == "Content.Translated") return box;
            if (FindEditor(child) is { } editor) return editor;
        }
        return null;
    }

    private async Task OfferRestoreDraftAsync()
    {
        if (_draftChecked) return;
        _draftChecked = true;
        var draft = _sessions.ReadDraft();
        if (draft == null || ViewModel.HasScript) return;
        // Loaded may precede the main window's dialog-host initialization.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var result = await ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService
            .ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
            {
                Title = "重新打开上次未保存的翻译？",
                Content = $"上次编辑的翻译尚未保存。是否恢复？\n{draft.ScriptPath}",
                PrimaryButtonText = "重新打开",
                SecondaryButtonText = "丢弃草稿",
                CloseButtonText = "稍后"
            }, CancellationToken.None);
        if (result == ContentDialogResult.Secondary) { _sessions.DeleteDraft(); return; }
        if (result != ContentDialogResult.Primary) { _draftChecked = false; return; }
        _restoringDraft = true;
        try
        {
            draft.Script.OriginalEffectIndices = draft.OriginalEffectIndices;
            ViewModel.RestoreDraft(draft.Script, draft.ScriptPath, draft.TranslationPath, draft.Values, draft.SavedValues, draft.UseV2,
                draft.AutoFormat, draft.ActiveSequenceNumber, draft.SyncMatchingContent, draft.SyncMatchingSpeakers);
            if (ViewModel.ActiveSequenceNumber > 0) NavigateToLine(ViewModel.Events[ViewModel.ActiveSequenceNumber - 1]);
        }
        catch (Exception exception)
        {
            _draftChecked = false;
            SnackbarService.Show("恢复失败", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        }
        finally { _restoringDraft = false; }
    }

    public TranslatePageModel ViewModel => (TranslatePageModel)DataContext;

    private async void UpdateFlashbackOrigins()
    {
        _flashbackLookup?.Cancel();
        var sources = ViewModel.FlashbackSources;
        if (!ViewModel.HasScript || sources.Length == 0) return;
        using var lookup = new CancellationTokenSource();
        _flashbackLookup = lookup;
        var downloads = DownloadPageModel.Instance;
        var downloadSources = new[] { downloads.CurrentSource }.Concat(downloads.SourceData).ToArray();
        Fetcher.Instance.SetProxy(SettingPageModel.Instance.GetProxy());
        try
        {
            var origins = await Task.Run(() => _flashbackEvents.ResolveAsync(sources, downloadSources, lookup.Token,
                async token =>
                {
                    var json = await Fetcher.Instance.FetchOnceAsync(NetworkEndpoints.Current.Resources.SourceListUrl, token);
                    return System.Text.Json.JsonSerializer.Deserialize<SourceData[]>(json)
                        ?? throw new InvalidDataException("数据源列表为空");
                }), lookup.Token);
            if (!lookup.IsCancellationRequested) ViewModel.ApplyFlashbackOrigins(origins);
        }
        catch (OperationCanceledException) when (lookup.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.Log($"闪回活动来源解析失败: {exception.Message}", LogLevel.Warning);
        }
        finally
        {
            if (ReferenceEquals(_flashbackLookup, lookup)) _flashbackLookup = null;
        }
    }


    private static ISnackbarService SnackbarService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    private async void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "剧本文件|*.json;*.asset"
        };

        if (openFileDialog.ShowDialog() != true) return;

        await LoadScriptFileAsync(openFileDialog.FileName);
    }

    private void Page_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var path = GetDroppedFilePath(e);
        var script = IsScriptPath(path);
        var translation = !script && GetDragTranslation(path) != null;
        var allowed = !_isLoadingScript && (script || translation);
        e.Effects = allowed
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
        var showError = !script && path != null && Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase);
        DropOverlay.Visibility = allowed || showError ? Visibility.Visible : Visibility.Collapsed;
        DropErrorRegion.Visibility = !allowed && showError ? Visibility.Visible : Visibility.Collapsed;
        ReferenceDropRegion.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
        TranslationDropRegion.Visibility = Visibility.Collapsed;
        DropErrorText.Text = _isLoadingScript ? "正在载入文件，请稍后重试。" : _dragTranslationError;
        if (!allowed) return;

        ReferenceDropText.Text = script ? "松开以重新载入剧本" : "松开以载入参考翻译";
        Grid.SetRowSpan(ReferenceDropRegion, script ? 2 : 1);
        TranslationDropRegion.Visibility = script ? Visibility.Collapsed : Visibility.Visible;
        var upper = e.GetPosition(this).Y < ActualHeight / 2;
        ReferenceDropRegion.Background = script || upper ? new SolidColorBrush(Color.FromArgb(160, 0, 100, 180)) : Brushes.Transparent;
        TranslationDropRegion.Background = !upper ? new SolidColorBrush(Color.FromArgb(160, 0, 100, 180)) : Brushes.Transparent;
    }

    private async void Page_OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        e.Handled = true;
        e.Effects = DragDropEffects.None;
        var path = GetDroppedFilePath(e);
        var reference = e.GetPosition(this).Y < ActualHeight / 2;
        DropOverlay.Visibility = Visibility.Collapsed;
        if (_isLoadingScript || path == null) return;

        if (IsScriptPath(path))
        {
            ResetDragState();
            e.Effects = DragDropEffects.Copy;
            await LoadScriptFileAsync(path);
            return;
        }

        // 松开时重新读取，避免拖动期间文件被修改后绕过校验。
        ResetDragState();
        var translation = GetDragTranslation(path);
        ResetDragState();
        if (translation == null) return;
        e.Effects = DragDropEffects.Copy;
        _isLoadingScript = true;
        try
        {
            if (reference)
                ViewModel.LoadReferenceTranslation(translation, path);
            else
            {
                if (!await ConfirmReplaceCurrentContentAsync()) return;
                ViewModel.LoadTranslation(translation, path);
            }
            ImportErrorPanel.Visibility = Visibility.Collapsed;
            SnackbarService.Show("成功", reference ? "参考翻译已载入" : "翻译已载入", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
        }
        catch (Exception exception)
        {
            ShowImportError(exception.Message);
        }
        finally
        {
            _isLoadingScript = false;
        }
    }

    private void Page_OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        ResetDragState();
    }

    private void Page_OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResetDragState();
        FlushDraft();
    }

    private void ResetDragState()
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        _dragTranslationPath = null;
        _dragStory = null;
        _dragTranslation = null;
        _dragTranslationError = null;
    }

    private TranslationData? GetDragTranslation(string? path)
    {
        if (path == null || !Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!ViewModel.HasScript)
        {
            _dragTranslationError = "请先载入剧本文件，再拖入翻译文件。";
            return null;
        }
        if (_dragTranslationPath == path && ReferenceEquals(_dragStory, ViewModel.Story)) return _dragTranslation;
        _dragTranslationPath = path;
        _dragStory = ViewModel.Story;
        _dragTranslation = null;
        _dragTranslationError = null;
        try
        {
            var translation = new TranslationData(path);
            _dragTranslationError = translation.IsEmpty()
                ? "翻译文件为空，或只包含空行和注释，没有可载入的翻译条目。"
                : ViewModel.GetTranslationMismatchReason(translation);
            if (_dragTranslationError == null)
                _dragTranslation = translation;
        }
        catch (Exception exception)
        {
            _dragTranslationError = $"翻译文件读取或解析失败：{exception.Message}";
        }
        return _dragTranslation;
    }

    private static string? GetDroppedFilePath(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths) return null;

        var path = paths[0];
        return File.Exists(path) ? path : null;
    }

    private static bool IsScriptPath(string? path) => path != null &&
        (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
         || Path.GetExtension(path).Equals(".asset", StringComparison.OrdinalIgnoreCase));

    private async Task LoadScriptFileAsync(string path)
    {
        if (_isLoadingScript) return;
        _isLoadingScript = true;
        try
        {
            await LoadScriptCoreAsync(path);
        }
        finally
        {
            _isLoadingScript = false;
        }
    }

    private async Task LoadScriptCoreAsync(string path)
    {
        GameScript script;
        try
        {
            script = new GameScript(path);
            // 先验证能够转换，再更新当前页面状态。
            _ = new Story(script, new TranslationData(null));
        }
        catch (Exception exception)
        {
            SnackbarService.Show("错误", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        if (!_draftChecked)
        {
            await OfferRestoreDraftAsync();
            if (!_draftChecked) return;
        }
        if (!ViewModel.IsEmpty && !await ConfirmReplaceCurrentContentAsync()) return;

        ViewModel.LoadScript(script, path);
        ImportErrorPanel.Visibility = Visibility.Collapsed;
        SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
    }

    private async void LoadTranslationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasScript)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt"
        };

        if (openFileDialog.ShowDialog() != true) return;
        var filePath = openFileDialog.FileName;

        try
        {
            var tData = new TranslationData(filePath);
            if (tData.IsEmpty()) { ShowImportError("翻译文件没有可载入的条目。"); return; }

            if (ViewModel.IsTranslationApplicable(tData))
            {
                if (!await ConfirmReplaceCurrentContentAsync()) return;

                ViewModel.LoadTranslation(tData, filePath);
                ImportErrorPanel.Visibility = Visibility.Collapsed;
                Logger.Log($"翻译载入成功: 剧本={ViewModel.ScriptPath}, 翻译={filePath}, 对话={tData.Translations.Count}");
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                Logger.Log($"翻译不适用于剧本: 翻译={filePath}", LogLevel.Warning);
                ShowImportError(ViewModel.GetTranslationMismatchReason(tData) ?? "翻译数据不适用于此剧本");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译载入失败: {ex.Message}", LogLevel.Error);
            ShowImportError($"载入失败: {ex.Message}");
        }
    }

    private void LoadReviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasScript)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt"
        };

        if (openFileDialog.ShowDialog() != true) return;
        var filePath = openFileDialog.FileName;

        try
        {
            var tData = new TranslationData(filePath);
            if (tData.IsEmpty()) { ShowImportError("参考翻译文件没有可载入的条目。"); return; }

            if (ViewModel.IsTranslationApplicable(tData))
            {
                ViewModel.LoadReferenceTranslation(tData, filePath);
                ImportErrorPanel.Visibility = Visibility.Collapsed;
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                ShowImportError(ViewModel.GetTranslationMismatchReason(tData) ?? "翻译数据不适用于此剧本");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"参考翻译载入失败: {ex.Message}", LogLevel.Error);
            ShowImportError($"载入失败: {ex.Message}");
        }
    }


    private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasScript || !await ConfirmReplaceCurrentContentAsync("清除整个翻译工作区？", $"将移除剧本、参考及全部 {ViewModel.Events.Length} 条编辑内容。")) return;

        ViewModel.Clear();
    }

    private void OpenClearMenu_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.DataContext = ViewModel;
            menu.IsOpen = true;
        }
    }

    private async void ClearTranslation_OnClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmReplaceCurrentContentAsync("清除当前译文？", $"将清除 {ViewModel.Events.Length} 条译文，保留剧本和参考翻译。"))
            ViewModel.ClearTranslation();
    }
    private void ClearReference_OnClick(object sender, RoutedEventArgs e) => ViewModel.ClearReference();
    private void ShowImportError(string detail)
    {
        ImportErrorText.Text = detail + "\n请确认文件属于当前剧本，并根据条目编号、数量或类型修正后重新载入。";
        ImportErrorPanel.Visibility = Visibility.Visible;
    }
    private void DismissImportError_OnClick(object sender, RoutedEventArgs e) => ImportErrorPanel.Visibility = Visibility.Collapsed;
    private void CopyImportError_OnClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ImportErrorText.Text); }
        catch (Exception exception) { Logger.Log($"复制导入错误失败: {exception.Message}", LogLevel.Warning); }
    }

    private void OpenDownloadPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Navigate(typeof(DownloadPage));
    }

    private void UpdateLoadButtons()
    {
        UpdateButton(LoadScriptButton, ViewModel.ScriptPath, "载入剧本", "更换剧本");
        UpdateButton(LoadTranslationButton, ViewModel.TranslationPath, "载入翻译文件", "更换翻译文件");
        UpdateButton(ReferenceTranslationButton, ViewModel.ReferenceTranslationPath, "载入参考翻译文件", "更换参考翻译");
        UpdateFilePath(TextBlockScriptFile, ViewModel.ScriptPath);
        UpdateFilePath(TextBlockTranslation, ViewModel.TranslationPath);
        UpdateFilePath(TextBlockTranslationReference, ViewModel.ReferenceTranslationPath);

        static void UpdateButton(Wpf.Ui.Controls.Button button, string path, string loadText, string clearText)
        {
            var loaded = !string.IsNullOrEmpty(path);
            button.Content = loaded ? clearText : loadText;
            button.Appearance = ControlAppearance.Secondary;
            button.ToolTip = loaded ? path : null;
        }
    }

    private static void UpdateFilePath(System.Windows.Controls.TextBlock textBlock, string path)
    {
        textBlock.ToolTip = string.IsNullOrEmpty(path) ? null : path;
        textBlock.Text = CompactPath(path);

        string CompactPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || Fits(fullPath)) return fullPath;

            var root = Path.GetPathRoot(fullPath) ?? string.Empty;
            // 从前向后省略目录，优先保留完整文件名及末尾目录。
            for (var i = root.Length; i < fullPath.Length; i++)
            {
                if (fullPath[i] is not ('\\' or '/')) continue;
                var candidate = root + ".." + fullPath[i..];
                if (Fits(candidate)) return candidate;
            }

            // 文件名本身过长时仍从中间省略，保留路径开头和扩展名。
            for (var keep = fullPath.Length - 1; keep > 0; keep--)
            {
                var prefixLength = Math.Min(root.Length, keep / 2);
                var candidate = fullPath[..prefixLength] + ".." + fullPath[^(keep - prefixLength)..];
                if (Fits(candidate)) return candidate;
            }

            return "..";
        }

        bool Fits(string text)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, textBlock.FlowDirection,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
            return formatted.WidthIncludingTrailingWhitespace <= textBlock.MaxWidth;
        }
    }

    private async Task<bool> ConfirmReplaceCurrentContentAsync(string title = "替换当前内容？", string? message = null)
    {
        if (ViewModel.IsDirty)
            return await ConfirmUnsavedChangesAsync(title);

        var dialogService = ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = title,
            Content = message ?? $"将替换当前 {ViewModel.Events.Length} 条翻译内容。已保存的文件不会被删除。",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        return result == ContentDialogResult.Primary;
    }

    public Task<bool> ConfirmCloseAsync() => ConfirmUnsavedChangesAsync("退出程序？", keepDraft: true);

    private async Task<bool> ConfirmUnsavedChangesAsync(string title, bool keepDraft = false)
    {
        if (!ViewModel.IsDirty) return true;

        var dialogService = ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = title,
            Content = "当前翻译有未保存的修改。",
            PrimaryButtonText = "保存",
            SecondaryButtonText = keepDraft ? "保留草稿并退出" : "不保存",
            CloseButtonText = "取消"
        }, CancellationToken.None);

        return result switch
        {
            ContentDialogResult.Primary => await SaveAsync(),
            ContentDialogResult.Secondary => DiscardChanges(),
            _ => false
        };

        bool DiscardChanges()
        {
            return !keepDraft || FlushDraft();
        }
    }

    public async Task<bool> SaveAsync(bool saveAs = false)
    {
        if (_isSaving || ViewModel.IsEmpty) return false;
        _isSaving = true;
        try
        {
            if (Keyboard.FocusedElement is FrameworkElement { DataContext: LineDialogModel focused }) focused.CompleteTextEdit();
            return await SaveCoreAsync(saveAs);
        }
        finally
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox { IsReadOnly: false, DataContext: LineDialogModel focused }) focused.DeferFormatting = true;
            _isSaving = false;
        }
    }

    private async Task<bool> SaveCoreAsync(bool saveAs)
    {
        var dialogService = ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
        SekaiToolsGUI.ViewModel.Subtitle.SaveFileDialogModel target;
        var useV2 = ViewModel.ExportUsesV2;
        var optionsShown = saveAs || !ViewModel.HasTranslation;
        if (optionsShown)
        {
            var dialog = new SaveFileDialog(dialogService.GetDialogHostEx() ?? throw new InvalidOperationException(),
                ViewModel.ScriptPath, ViewModel.TranslationPath, ViewModel);
            if (await dialogService.ShowAsync(dialog, CancellationToken.None) != ContentDialogResult.Primary) return false;
            target = dialog.ViewModel;
            useV2 = dialog.UseV2;
        }
        else target = new SekaiToolsGUI.ViewModel.Subtitle.SaveFileDialogModel { FileName = ViewModel.TranslationPath };

        var preflight = target.FileName + "\n" + useV2 + "\n" + ViewModel.GetExportSummary(useV2);
        if (!optionsShown && preflight != _acceptedPreflight &&
            (ViewModel.UntranslatedCount > 0 || ViewModel.ExplicitEmptyCount > 0 || ViewModel.WarningCount > 0))
        {
            var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
            {
                Title = "保存当前翻译进度？",
                Content = ViewModel.GetExportSummary(useV2) + "\n\n保存到：" + target.FileName,
                PrimaryButtonText = "保存", CloseButtonText = "取消"
            }, CancellationToken.None);
            if (result != ContentDialogResult.Primary) return false;
        }
        try
        {
            var content = ViewModel.Export(useV2);
            var values = ViewModel.ReadValues();
            if (!await ExportOverwriteConfirmation.ConfirmAsync(dialogService, target, CancellationToken.None)) return false;
            await File.WriteAllTextAsync(target.FileName, content);
            ViewModel.ExportUsesV2 = useV2;
            ViewModel.MarkSaved(values, target.FileName);
            _acceptedPreflight = preflight;
            FlushDraft();
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译保存失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("保存失败", ex.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.Dismiss24), TimeSpan.FromSeconds(5));
            return false;
        }
        SnackbarService.Show("保存成功", "翻译文件已保存，可点击“打开保存位置”查看。", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
        return true;
    }

    private async void SaveAsButton_OnClick(object sender, RoutedEventArgs e) => await SaveAsync(true);
    private void OpenSavedLocation_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasTranslation) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{ViewModel.TranslationPath}\"" });
        }
        catch (Exception ex) { Logger.Log($"打开保存位置失败: {ex.Message}", LogLevel.Warning); }
    }

    private void SpecialCharButton_OnClick(object sender, RoutedEventArgs e)
    {
        SpecialCharPopover.Open();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        await SaveAsync();
    }

    private void SpecialCharacters_OnCustomCharacterAdding(object? sender, EventArgs e)
    {
        SpecialCharPopover.Close();
    }
}
