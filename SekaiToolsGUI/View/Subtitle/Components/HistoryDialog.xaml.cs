using System.IO;
using System.Windows;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class HistoryDialog : ContentDialog
{
    public HistoryDialog(ContentDialogHost contentPresenter, List<HistoryEntry> entries) : base(contentPresenter)
    {
        InitializeComponent();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var videoName = Path.GetFileName(entry.State.VideoFilePath);
            var button = new Button
            {
                Content = $"{entry.Timestamp}    {videoName}\n{GetStatusText(entry.State.StopReason)}",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 8, 12, 8),
                ToolTip = $"视频：{entry.State.VideoFilePath}\n剧本：{entry.State.ScriptFilePath}\n翻译：{entry.State.TranslateFilePath}"
            };

            button.Click += (_, _) =>
            {
                SelectedEntry = entry;
                Hide(ContentDialogResult.Primary);
            };
            HistoryItemsPanel.Children.Add(button);
        }
    }

    public HistoryEntry? SelectedEntry { get; private set; }

    private static string GetStatusText(ProcessStopReason reason)
    {
        return reason switch
        {
            ProcessStopReason.Completed => "已完成",
            ProcessStopReason.Canceled => "已取消",
            ProcessStopReason.EndOfStream => "部分完成",
            ProcessStopReason.ReadFailed => "读取失败",
            ProcessStopReason.ExceptionThreshold => "处理异常",
            ProcessStopReason.CaptureError => "视频读取错误",
            ProcessStopReason.UnexpectedError => "处理失败",
            _ => "状态未知"
        };
    }
}
