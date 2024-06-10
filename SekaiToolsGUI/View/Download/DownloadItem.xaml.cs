using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Input;

namespace SekaiToolsGUI.View.Download.Tabs.UnitStory;

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