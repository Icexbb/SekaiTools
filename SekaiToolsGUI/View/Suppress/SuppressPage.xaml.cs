using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.ViewModel.Suppress;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage<SuppressPageModel>
{
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

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        ViewModel.SourceVideo = result;
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "字幕文件|*.ass");
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
            var overwriteExisting = File.Exists(ViewModel.OutputPath);
            if (overwriteExisting && !await ConfirmOverwriteAsync()) return;

            await BeginSuppressAsync(overwriteExisting);
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
            ViewModel.Running = false;
            if (Debugger.IsAttached) throw;
        }
    }

    private async Task<bool> ConfirmOverwriteAsync()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "覆盖已有文件？",
                Content = $"输出文件已存在：\n{ViewModel.OutputPath}\n\n压制成功后将替换该文件。",
                PrimaryButtonText = "覆盖",
                CloseButtonText = "取消"
            }, CancellationToken.None);
        return result == ContentDialogResult.Primary;
    }


    private async void DisposeButton_OnClick(object sender, RoutedEventArgs e)
    {
        await CancelSuppressAsync();
    }

    private async void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        await CancelSuppressAsync();
        ViewModel.Reset();
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
