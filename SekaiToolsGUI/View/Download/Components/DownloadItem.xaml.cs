using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadItem : UserControl
{
    private DownloadItem(Func<string> url, string key)
    {
        InitializeComponent();
        DataContext = this;
        Initialize(url, key);
        Margin = new Thickness(10, 5, 10, 5);
    }

    public Func<string> Url { get; set; } = () => "";

    private string Key { get; set; } = "";

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var parent = Parent;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);
            SourceList.Instance.SourceData = DownloadPageModel.Instance.CurrentSource.Data;

            var key = DownloadPageModel.Instance.CurrentSource.Data.SourceName + " - " + Key;
            var url = Url();
            (parent as DownloadPage)?.AddTask(key, url);
        });
    }
}

public partial class DownloadItem
{
    private static List<DownloadItem> RecycleContainer { get; } = [];

    private void Initialize(Func<string> url, string key)
    {
        Visibility = Visibility.Visible;
        Url = url;
        Key = key;
        KeyText.Text = Key;
    }

    public void Recycle()
    {
        Visibility = Visibility.Collapsed;
        RecycleContainer.Add(this);
        if (Parent is Panel parent)
        {
            parent.Children.Remove(this);
        }
    }

    public static DownloadItem GetItem(Func<string> url, string key)
    {
        if (RecycleContainer.Count <= 0) return new DownloadItem(url, key);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.Initialize(url, key);
        return item;
    }
}