using System.IO;
using System.Windows;
using SekaiToolsCore;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class HistoryDialog : ContentDialog
{
    public HistoryDialog(ContentDialogHost contentPresenter, IReadOnlyList<HistoryEntry> completedEntries,
        HistoryEntry? unfinishedEntry) : base(contentPresenter)
    {
        InitializeComponent();

        if (unfinishedEntry != null)
        {
            AddHeader("未完成任务（可继续恢复）");
            var button = CreateEntryButton(unfinishedEntry, "最近一次未完成");
            button.Click += (_, _) =>
            {
                SelectedUnfinishedEntry = unfinishedEntry;
                Hide(ContentDialogResult.Primary);
            };
            HistoryItemsPanel.Children.Add(button);
        }

        if (completedEntries.Count > 0)
        {
            AddHeader("已完成历史");
            foreach (var entry in completedEntries)
            {
                var button = CreateEntryButton(entry, entry.Timestamp);
                button.Click += (_, _) =>
                {
                    SelectedEntry = entry;
                    Hide(ContentDialogResult.Primary);
                };
                HistoryItemsPanel.Children.Add(button);
            }
        }
    }

    public HistoryEntry? SelectedEntry { get; private set; }

    public HistoryEntry? SelectedUnfinishedEntry { get; private set; }

    private void AddHeader(string text)
    {
        HistoryItemsPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 8)
        });
    }

    private static Button CreateEntryButton(HistoryEntry entry, string prefix)
    {
        var videoName = Path.GetFileName(entry.State.VideoFilePath);
        return new Button
        {
            Content = $"{prefix}    {videoName}\n{GetStatusText(entry.State.StopReason)}",
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12, 8, 12, 8),
            ToolTip =
                $"视频：{entry.State.VideoFilePath}\n剧本：{entry.State.ScriptFilePath}\n翻译：{entry.State.TranslateFilePath}"
        };
    }

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
