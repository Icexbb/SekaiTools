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

    public TranslatePage() : this(new EditorSessionStore()) { }

    internal TranslatePage(EditorSessionStore sessions)
    {
        _sessions = sessions;
        InitializeComponent();
        DataContext = new TranslatePageModel();
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
            _draftTimer.Stop();
            _draftTimer.Start();
        };
        Loaded += async (_, _) => await OfferRestoreDraftAsync();
        PreviewKeyDown += OnHistoryKeyDown;
        AddHandler(CommandManager.PreviewCanExecuteEvent, new CanExecuteRoutedEventHandler((_, e) =>
        {
            if (e.Command != ApplicationCommands.Undo && e.Command != ApplicationCommands.Redo) return;
            e.CanExecute = e.Command == ApplicationCommands.Undo ? ViewModel.CanUndo : ViewModel.CanRedo;
            e.Handled = true;
        }));
        AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler((_, e) =>
        {
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
        if (_sessions.SaveDraft(ViewModel)) return true;
        SnackbarService.Show("草稿保存失败", "无法写入本地恢复草稿，请保存翻译文件后再退出。", ControlAppearance.Danger,
            new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        return false;
    }

    private void OnHistoryKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            (Keyboard.Modifiers & ModifierKeys.Alt) != 0) return;
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
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
            ViewModel.RestoreDraft(draft.Script, draft.ScriptPath, draft.TranslationPath, draft.Values, draft.SavedValues);
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
        if (ViewModel.HasScript)
        {
            if (!await ConfirmReplaceCurrentContentAsync()) return;
            ViewModel.Clear();
            return;
        }

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
            SnackbarService.Show("成功", reference ? "参考翻译已载入" : "翻译已载入", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
        }
        catch (Exception exception)
        {
            SnackbarService.Show("错误", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
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
        SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
    }

    private async void LoadTranslationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasTranslation)
        {
            if (!await ConfirmReplaceCurrentContentAsync()) return;
            ViewModel.ClearTranslation();
            return;
        }

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

            if (ViewModel.IsTranslationApplicable(tData))
            {
                if (!await ConfirmReplaceCurrentContentAsync()) return;

                ViewModel.LoadTranslation(tData, filePath);
                Logger.Log($"翻译载入成功: 剧本={ViewModel.ScriptPath}, 翻译={filePath}, 对话={tData.Translations.Count}");
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                Logger.Log($"翻译不适用于剧本: 翻译={filePath}", LogLevel.Warning);
                SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译载入失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("错误", $"载入失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        }
    }

    private void LoadReviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasReferenceTranslation)
        {
            ViewModel.ClearReference();
            SnackbarService.Show("已清除", "参考翻译已清除", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
            return;
        }

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

            if (ViewModel.IsTranslationApplicable(tData))
            {
                ViewModel.LoadReferenceTranslation(tData, filePath);
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"参考翻译载入失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("错误", $"载入失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        }
    }


    private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasScript || !await ConfirmReplaceCurrentContentAsync()) return;

        ViewModel.Clear();
    }

    private void OpenDownloadPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Navigate(typeof(DownloadPage));
    }

    private void UpdateLoadButtons()
    {
        UpdateButton(LoadScriptButton, ViewModel.ScriptPath, "载入剧本", "清除剧本");
        UpdateButton(LoadTranslationButton, ViewModel.TranslationPath, "载入翻译文件", "清除翻译");
        UpdateButton(ReferenceTranslationButton, ViewModel.ReferenceTranslationPath, "载入参考翻译文件", "清除参考翻译");
        UpdateFilePath(TextBlockScriptFile, ViewModel.ScriptPath);
        UpdateFilePath(TextBlockTranslation, ViewModel.TranslationPath);
        UpdateFilePath(TextBlockTranslationReference, ViewModel.ReferenceTranslationPath);

        static void UpdateButton(Wpf.Ui.Controls.Button button, string path, string loadText, string clearText)
        {
            var loaded = !string.IsNullOrEmpty(path);
            button.Content = loaded ? clearText : loadText;
            button.Appearance = loaded ? ControlAppearance.Caution : ControlAppearance.Secondary;
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

    private async Task<bool> ConfirmReplaceCurrentContentAsync()
    {
        if (ViewModel.IsDirty)
            return await ConfirmUnsavedChangesAsync("替换当前内容？");

        var dialogService = ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "替换当前内容？",
            Content = "当前翻译内容将被清除，未保存的修改无法恢复。",
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

    public async Task<bool> SaveAsync()
    {
        if (ViewModel.IsEmpty)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return false;
        }

        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new SaveFileDialog(
            dialogService.GetDialogHostEx() ?? throw new InvalidOperationException(),
            ViewModel.ScriptPath, ViewModel.TranslationPath);
        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return false;
        var fileName = dialog.ViewModel.FileName;


        try
        {
            var content = ExportTranslation();
            var savedValues = ViewModel.ReadValues();
            if (!await ExportOverwriteConfirmation.ConfirmAsync(dialogService, dialog.ViewModel, token))
                return false;

            await File.WriteAllTextAsync(fileName, content, token);
            ViewModel.MarkSaved(savedValues, fileName);
            FlushDraft();
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译保存失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("错误", $"保存失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.Dismiss24), TimeSpan.FromSeconds(5));
            return false;
        }

        var snackService = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;
        snackService.Show("成功", "翻译文件已保存", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
        try
        {
            ShowFile(fileName);
        }
        catch (Exception ex)
        {
            Logger.Log($"打开保存位置失败: {ex.Message}", LogLevel.Warning);
        }

        return true;

        void ShowFile(string path)
        {
            var psi = new ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            Process.Start(psi);
        }

        string ExportTranslation()
        {
            return ViewModel.Export(dialog.UseV2);
        }
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
