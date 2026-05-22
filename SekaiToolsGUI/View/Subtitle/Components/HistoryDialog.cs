using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public class HistoryDialog : ContentDialog
{
    private readonly ListBox _listBox;
    private readonly List<HistoryEntry> _entries;

    public HistoryEntry? SelectedEntry { get; private set; }

    public HistoryDialog(ContentDialogHost contentPresenter, List<HistoryEntry> entries) : base(contentPresenter)
    {
        _entries = entries;
        Title = "历史记录";
        PrimaryButtonText = "加载";
        CloseButtonText = "取消";

        _listBox = new ListBox
        {
            MinWidth = 520,
            MinHeight = 300,
            MaxHeight = 400,
            Margin = new Thickness(0, 8, 0, 0)
        };

        foreach (var entry in entries)
        {
            var videoName = Path.GetFileName(entry.State.VideoFilePath);
            _listBox.Items.Add($"{entry.Timestamp}    {videoName}");
        }

        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;

        Content = _listBox;
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary && _listBox.SelectedIndex >= 0)
            SelectedEntry = _entries[_listBox.SelectedIndex];
        base.OnButtonClick(button);
    }
}
