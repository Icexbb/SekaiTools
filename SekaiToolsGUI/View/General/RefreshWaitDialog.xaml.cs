using SekaiToolsInfrastructure.Resources;
using SekaiDataFetch.List;
using SekaiToolsGUI.ViewModel.General;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.General;

public partial class RefreshWaitDialog : ContentDialog
{
    public RefreshWaitDialog(string message = "")
    {
        InitializeComponent();
        DataContext = new RefreshWaitDialogModel();
        ViewModel.Message = message;
        UpdateLayout();
    }

    private RefreshWaitDialogModel ViewModel => (RefreshWaitDialogModel)DataContext;

    public void UpdateResourceProgress(ResourceDownloadProgress progress)
    {
        ViewModel.HasProgress = true;
        ViewModel.Percentage = progress.TotalBytes > 0
            ? Math.Min(100, progress.BytesReceived * 100.0 / progress.TotalBytes) : 0;
        ViewModel.ProgressText = $"{progress.FileName}：{progress.BytesReceived / 1024.0:N0} / {progress.TotalBytes / 1024.0:N0} KiB";
        ViewModel.FileDetails = $"第 {progress.Attempt} / 3 次尝试（多个文件并行下载）";
    }

    public void UpdateProgress(ListRefreshProgress progress)
    {
        ViewModel.HasProgress = true;
        ViewModel.Message = progress.Stage switch
        {
            ListRefreshStage.Downloading => "正在下载列表数据",
            ListRefreshStage.Saving => "正在保存下载数据",
            ListRefreshStage.Loading => "正在加载列表",
            ListRefreshStage.Completed => "列表刷新完成",
            _ => "正在刷新下载源数据"
        };
        ViewModel.Percentage = progress.Percentage;
        ViewModel.ProgressText =
            $"已下载 {progress.CompletedFiles} / {progress.Files.Count} 个文件（{progress.Percentage:0}%）";
        if (progress.UnavailableFiles > 0) ViewModel.ProgressText += $"，可选数据不可用 {progress.UnavailableFiles} 个";
        ViewModel.FileDetails = string.Join(Environment.NewLine, progress.Files.Select(file =>
            $"{file.FileName} — {(file.IsUnavailable ? "不可用（跳过）" : file.IsDownloaded ? "已下载" : "正在下载")}"));
    }
}