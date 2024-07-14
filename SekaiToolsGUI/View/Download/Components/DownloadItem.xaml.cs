using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadItem : UserControl
{
    public string Url { get; set; }
    public string Key { get; set; }

    public DownloadItem(string url, string key)
    {
        Url = url;
        Key = key;
        InitializeComponent();
        DataContext = this;
    }

    private void Add()
    {
        Dispatcher.Invoke(() =>
        {
            var parent = Parent;
            while (parent != null && parent is not DownloadPage)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            (parent as DownloadPage)?.AddTask(Key, Url);
        });
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Add();
    }
}