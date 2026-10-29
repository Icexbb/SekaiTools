using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadTask : UserControl
{
    private bool _canEdit = true;
    private bool? _horizontalLayout;
    public DownloadTask(string scriptTag, string url, string sourceName = "", string? directory = null)
    {
        InitializeComponent();
        Url = url;
        ScriptTag = scriptTag;
        SourceName = sourceName;
        SetDirectory(directory ?? Model.Setting.Default.DownloadDirectory);
        TaskNameTextBlock.Text = string.Join(" · ", ScriptTag.Split("|"));
        SourceText.Text = sourceName;
        SizeChanged += (_, _) => UpdateLayoutMode();
    }
    public Guid Id { get; } = Guid.NewGuid();
    public string ScriptTag { get; }
    public string Url { get; }
    public string SourceName { get; }
    public string SavePath { get; private set; } = "";
    public DownloadStatus Status { get; private set; }
    public bool Downloaded => Status is DownloadStatus.Succeeded or DownloadStatus.Skipped;
    public event EventHandler? RemoveRequested;
    public event EventHandler? RetryRequested;
    public DownloadJob Job => new(Id, ScriptTag, Url, SavePath, SourceName);
    private void UpdateLayoutMode()
    {
        var horizontal = ActualWidth > 460;
        if (_horizontalLayout == horizontal) return;
        _horizontalLayout = horizontal;
        Control.Padding = new Thickness(horizontal ? 8 : 12);
        Grid.SetColumnSpan(TaskNameTextBlock, horizontal ? 1 : 2);
        Grid.SetColumnSpan(SourceText, horizontal ? 1 : 2);
        Grid.SetRow(StatusGrid, horizontal ? 0 : 3);
        Grid.SetColumn(StatusGrid, horizontal ? 1 : 0);
        Grid.SetColumnSpan(StatusGrid, horizontal ? 1 : 2);
        Grid.SetRowSpan(StatusGrid, horizontal ? 2 : 1);
        StatusGrid.Margin = horizontal ? new Thickness(12, 0, 0, 0) : new Thickness(0, 8, 0, 0);
    }

    public void SetDirectory(string directory)
    {
        SavePath = Path.Combine(directory, Path.GetFileName(new Uri(Url).LocalPath));
        ToolTip = $"{ScriptTag}\n来源：{SourceName}\n保存到：{SavePath}";
    }
    public void SetCanRemove(bool canRemove)
    {
        _canEdit = canRemove;
        RemoveButton.IsEnabled = canRemove;
        RetryButton.IsEnabled = canRemove;
    }
    private void RemoveButton_OnClick(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
    private void RetryButton_OnClick(object sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);
    public void ChangeStatus(DownloadStatus status, string? error = null)
    {
        Status = status;
        (StatusIcon.Symbol, StatusText.Text) = status switch
        {
            DownloadStatus.Downloading => (SymbolRegular.ArrowSync24, "下载中"),
            DownloadStatus.Succeeded => (SymbolRegular.DocumentCheckmark24, "下载完成"),
            DownloadStatus.Failed => (SymbolRegular.DocumentDismiss24, "下载失败"),
            DownloadStatus.Stopped => (SymbolRegular.Dismiss24, "已停止"),
            DownloadStatus.Skipped => (SymbolRegular.DocumentCheckmark24, "文件已存在，已跳过"),
            _ => (SymbolRegular.ArrowDownload24, "等待下载")
        };
        var brush = status switch
        {
            DownloadStatus.Succeeded => "SystemFillColorSuccessBrush",
            DownloadStatus.Failed => "SystemFillColorCriticalBrush",
            DownloadStatus.Downloading => "AccentTextFillColorPrimaryBrush",
            _ => "TextFillColorSecondaryBrush"
        };
        StatusIcon.SetResourceReference(ForegroundProperty, brush);
        StatusText.SetResourceReference(ForegroundProperty, brush);
        ErrorText.Text = error ?? "";
        ErrorText.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
        RetryButton.Visibility = status is DownloadStatus.Failed or DownloadStatus.Stopped ? Visibility.Visible : Visibility.Collapsed;
        RetryButton.IsEnabled = _canEdit;
    }
}
