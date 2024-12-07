using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadItem : UserControl
{
    public DownloadItem(string url, string key)
    {
        InitializeComponent();
        DataContext = this;
        Initialize(url, key);
        Margin = new Thickness(10, 5, 10, 5);
    }

    private string Url { get; set; } = "";
    private string Key { get; set; } = "";

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var parent = Parent;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

            (parent as DownloadPage)?.AddTask(Key, Url);
        });
    }
}

public partial class DownloadItem
{
    public void Initialize(string url, string key)
    {
        Url = url;
        Key = key;
        KeyText.Text = Key;
    }

    private static List<DownloadItem> RecycleContainer { get; } = [];

    public static void RecycleItem(DownloadItem item)
    {
        item.Visibility = Visibility.Collapsed;
        RecycleContainer.Add(item);
        (item.Parent as Panel)?.Children.Remove(item);
    }

    public static DownloadItem GetItem(string url, string key)
    {
        if (RecycleContainer.Count <= 0) return new DownloadItem(url, key);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.Visibility = Visibility.Visible;
        item.Initialize(url, key);
        return item;
    }
}