using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsInfrastructure.Persistence;
using SekaiToolsCore;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class HistoryDialog : ContentDialog
{
    public HistoryDialog(ContentDialogHost contentPresenter, IReadOnlyList<HistoryCatalogEntry> completedEntries,
        IReadOnlyList<HistoryCatalogEntry> unfinishedEntries) : base(contentPresenter)
    {
        InitializeComponent();

        if (unfinishedEntries.Count > 0)
        {
            AddHeader("未完成任务（作为历史结果查看）");
            foreach (var unfinishedEntry in unfinishedEntries)
            {
                var (row, button) = CreateEntryRow(unfinishedEntry, "未完成");
                button.Click += (_, _) =>
                {
                    SelectedUnfinishedEntry = unfinishedEntry;
                    Hide(ContentDialogResult.Primary);
                };
                HistoryItemsPanel.Children.Add(row);
            }
        }

        if (completedEntries.Count > 0)
        {
            AddHeader("已完成历史");
            foreach (var entry in completedEntries)
            {
                var (row, button) = CreateEntryRow(entry, entry.Timestamp);
                button.Click += (_, _) =>
                {
                    SelectedEntry = entry;
                    Hide(ContentDialogResult.Primary);
                };
                HistoryItemsPanel.Children.Add(row);
            }
        }
    }

    public HistoryCatalogEntry? SelectedEntry { get; private set; }

    public HistoryCatalogEntry? SelectedUnfinishedEntry { get; private set; }

    private void AddHeader(string text)
    {
        HistoryItemsPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 8)
        });
    }

    private static (Card Row, Button LoadButton) CreateEntryRow(HistoryCatalogEntry entry, string prefix)
    {
        var summary = entry.Summary;
        var videoName = Path.GetFileName(summary.VideoFilePath);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new System.Windows.Controls.TextBlock
        {
            Text = $"{videoName}\n{prefix} · {GetStatusText(summary.StopReason)}",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        var button = new Button
        {
            Content = "加载",
            Icon = new SymbolIcon(SymbolRegular.FolderOpen24),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"加载 {videoName}"
        };
        System.Windows.Automation.AutomationProperties.SetName(button, $"加载 {videoName}");
        Grid.SetColumn(button, 1);
        grid.Children.Add(text);
        grid.Children.Add(button);
        var card = new Card
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12, 8, 12, 8),
            ToolTip =
                $"视频：{summary.VideoFilePath}\n剧本：{summary.ScriptFilePath}\n翻译：{summary.TranslateFilePath}"
        };
        return (card, button);
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
