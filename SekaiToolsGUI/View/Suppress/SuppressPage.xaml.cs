using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.ViewModel.Suppress;
using SekaiToolsMedia;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage<SuppressPageModel>
{
    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".webm", ".wmv"
    };

    private bool _preparingResources;

    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();
        InitializeSuppressor();
    }

    public SuppressPageModel ViewModel => (SuppressPageModel)DataContext;

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    public async void OnNavigatedTo()
    {
        if (ViewModel.ResourcesReady || _preparingResources) return;

        _preparingResources = true;
        ViewModel.BeginResourcePreparation();
        try
        {
            if (!await ResourceManager.Instance.CheckResource(ResourceType.VapourSynth))
                await EnsureVapourSynthResourceAsync();
            if (!await ResourceManager.Instance.CheckResource(ResourceType.VapourSynth))
                throw new InvalidDataException("VapourSynth 运行环境校验失败");

            ViewModel.CompleteResourcePreparation();
        }
        catch (Exception e)
        {
            ViewModel.FailResourcePreparation();
            (Application.Current.MainWindow as MainWindow)?.OnCheckResourceFailed(e, OnNavigatedTo);
        }
        finally
        {
            _preparingResources = false;
        }
    }

    private static async Task EnsureVapourSynthResourceAsync()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在准备 VapourSynth 运行环境，请稍候……");
        using var source = new CancellationTokenSource();
        var dialogTask = dialogService.ShowAsync(dialog, source.Token);
        try
        {
            await ResourceManager.Instance.EnsureResource(ResourceType.VapourSynth);
        }
        finally
        {
            await source.CancelAsync();
            try
            {
                await dialogTask;
            }
            catch (OperationCanceledException)
            {
                // 下载完成或失败时关闭等待对话框。
            }
        }
    }

    private static string? SelectFile(string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;

        ViewModel.SetSourceVideos(dialog.FileNames);
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile("字幕文件|*.ass");
        if (result == null) return;

        ViewModel.SourceSubtitle = result;
    }

    private void ClearSubtitle_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SourceSubtitle = "";
    }

    private void SaveFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectSavePath();
        if (result == null) return;

        ViewModel.OutputPath = result;
    }

    private string? SelectSavePath()
    {
        var openFileDialog = new SaveFileDialog
        {
            Filter = "Mp4 文件|*.mp4",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.SourceVideo),
            DefaultExt = ".mp4",
            FileName = Path.ChangeExtension("[STVS]" + Path.GetFileName(ViewModel.SourceVideo), ".mp4")
        };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private async void StartSuppress_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var existingOutputs = ViewModel.QueueItems
                .Select(item => Path.GetFullPath(item.OutputPath))
                .Where(File.Exists)
                .ToArray();
            if (existingOutputs.Length > 0 && !await ConfirmOverwriteAsync(existingOutputs)) return;

            var pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            await BeginSuppressQueueAsync(existingOutputs.ToHashSet(pathComparer));
        }
        catch (OperationCanceledException)
        {
            // 用户主动停止。
        }
        catch (Exception exc)
        {
            Logger.Log($"视频压制失败: {exc}", LogLevel.Error);
            SnackService.Show("视频处理出错", exc.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.VideoClipOff24), TimeSpan.FromSeconds(6));
            if (ViewModel.TaskState != VideoSuppressionState.Failed)
                ViewModel.FailTask($"压制失败：{exc.Message}");
            if (Debugger.IsAttached) throw;
        }
    }

    private async Task<bool> ConfirmOverwriteAsync(IReadOnlyList<string> existingOutputs)
    {
        var outputSummary = existingOutputs.Count == 1
            ? existingOutputs[0]
            : string.Join("\n", existingOutputs.Take(5).Select(Path.GetFileName)) +
              (existingOutputs.Count > 5 ? $"\n等 {existingOutputs.Count} 个文件" : "");
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = existingOutputs.Count == 1 ? "覆盖已有文件？" : $"覆盖 {existingOutputs.Count} 个已有文件？",
                Content = $"以下输出文件已存在：\n{outputSummary}\n\n对应任务压制成功后将替换原文件。",
                PrimaryButtonText = existingOutputs.Count == 1 ? "覆盖" : "全部覆盖",
                CloseButtonText = "取消"
            }, CancellationToken.None);
        return result == ContentDialogResult.Primary;
    }

    private void ClearQueue_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearQueue();
    }

    private void RemoveQueueItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button { CommandParameter: VideoSuppressionQueueItem item })
            ViewModel.RemoveQueueItem(item);
    }

    private void SuppressPage_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ViewModel.HasNotStarted && GetDroppedVideoFiles(e.Data).Length > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void SuppressPage_OnDrop(object sender, DragEventArgs e)
    {
        if (!ViewModel.HasNotStarted) return;
        var files = GetDroppedVideoFiles(e.Data);
        if (files.Length > 0) ViewModel.AddSourceVideos(files);
    }

    private static string[] GetDroppedVideoFiles(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] files)
            return [];
        return files
            .Where(File.Exists)
            .Where(path => SupportedVideoExtensions.Contains(Path.GetExtension(path)))
            .ToArray();
    }


    private async void DisposeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsTaskActive)
        {
            await CancelSuppressAsync();
            return;
        }

        ViewModel.ReloadStatus();
        ClearTaskbarProgress();
    }

    private async void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        await CancelSuppressAsync();
        ViewModel.Reset();
        ClearTaskbarProgress();
    }

    private void ShowFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowFile(ViewModel.OutputPath);
        return;

        void ShowFile(string path)
        {
            var psi = new ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            Process.Start(psi);
        }
    }

    private void StatusTextChange_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender!;
        box.ScrollToEnd();
    }
}
