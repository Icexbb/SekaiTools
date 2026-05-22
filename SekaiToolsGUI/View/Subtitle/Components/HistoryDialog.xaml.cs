using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class HistoryDialog : ContentDialog
{
    private readonly List<HistoryEntry> _entries;

    public HistoryEntry? SelectedEntry { get; private set; }

    public HistoryDialog(ContentDialogHost contentPresenter, List<HistoryEntry> entries) : base(contentPresenter)
    {
        InitializeComponent();
        _entries = entries;


        foreach (var entry in entries)
        {
            var videoName = Path.GetFileName(entry.State.VideoFilePath);
            var index = entries.IndexOf(entry);
            var button = new Button()
            {
                Content = $"{index + 1}: {entry.Timestamp}    {videoName}"
            };

            button.Click += (_, _) =>
            {
                SelectedEntry = entry;
                Hide(ContentDialogResult.Primary);
            };
            StackPanel.Children.Add(button);
        }
    }
}