using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.View.Suppress.Components;
using SekaiToolsGUI.ViewModel.Suppress;
using SekaiToolsMedia;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage<SuppressPageModel>
{
    private bool _preparingResources;

    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();

    }

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    public SuppressPageModel ViewModel => (SuppressPageModel)DataContext;

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

    private async void CreateTask_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ResourcesReady) return;

        try
        {
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService
                                ?? throw new InvalidOperationException("内容对话框服务不可用");
            var dialog = new SuppressionTaskDialog(
                dialogService.GetDialogHostEx() ?? throw new InvalidOperationException("内容对话框宿主不可用"), EnqueueSuppression);
            await dialogService.ShowAsync(dialog, CancellationToken.None);
        }
        catch (Exception exc)
        {
            Logger.Log($"创建压制任务失败: {exc}", LogLevel.Error);
            SnackService.Show("创建任务失败", exc.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.VideoClipOff24), TimeSpan.FromSeconds(6));
            if (Debugger.IsAttached) throw;
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var job in ViewModel.Jobs.Where(x => x.Progress.State is VideoSuppressionState.Completed
                     or VideoSuppressionState.Cancelled or VideoSuppressionState.Failed).ToList())
            ViewModel.Jobs.Remove(job);
        if (ViewModel.Jobs.Count == 0) ClearTaskbarProgress();
    }

}
