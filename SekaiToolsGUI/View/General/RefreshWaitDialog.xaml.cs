using SekaiToolsGUI.ViewModel.General;
using SekaiDataFetch.List;
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
        ViewModel.ProgressText = $"已下载 {progress.CompletedFiles} / {progress.Files.Count} 个文件（{progress.Percentage:0}%）";
        ViewModel.FileDetails = string.Join(Environment.NewLine, progress.Files.Select(file =>
            $"{file.FileName} — {(file.IsDownloaded ? "已下载" : "正在下载")}"));
    }
}
