using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SekaiToolsAvalonia.View.Download.Components;

public partial class DownloadItem : UserControl
{
    private readonly Func<string> _urlProvider;

    public DownloadItem(string displayText, Func<string> urlProvider)
    {
        InitializeComponent();
        KeyText.Text = displayText;
        _urlProvider = urlProvider;
    }

    public string Url => _urlProvider();

    private void ButtonBase_OnClick(object? sender, RoutedEventArgs e)
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage)
            parent = (parent as Control)?.Parent;

        if (parent is DownloadPage downloadPage)
            downloadPage.AddTask(KeyText.Text!, Url);
    }
}