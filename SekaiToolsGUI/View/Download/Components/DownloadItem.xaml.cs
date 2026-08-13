using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadItem : UserControl
{
    private Func<string> Url { get; set; } = () => "";

    private string TitleString { get; set; } = "";
    private string IndexString { get; set; } = "";

    private string TaskName { get; set; } = "";


    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var parent = Parent;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);
            SourceList.Instance.SourceData = DownloadPageModel.Instance.CurrentSource;

            var key = (DownloadPageModel.Instance.CurrentSource.SourceName + "|" + TaskName)
                .Trim();
            var url = Url();
            (parent as DownloadPage)?.AddTask(key, url);
        });
    }
}

public partial class DownloadItem
{
    private static List<DownloadItem> RecycleContainer { get; } = [];

    private void Initialize(Func<string> url, string title, string index)
    {
        Visibility = Visibility.Visible;
        Url = url;
        IndexString = index ?? "";
        IndexInfoBadge.Value = IndexString;
        IndexInfoBadge.Visibility = IndexString.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        TitleString = title;
        TitleTextBlock.Text = TitleString;
    }

    public void Recycle()
    {
        Visibility = Visibility.Collapsed;
        RecycleContainer.Add(this);
        if (Parent is Panel parent) parent.Children.Remove(this);
    }

    private DownloadItem(Func<string> url, string title, string index, string taskName)
    {
        InitializeComponent();
        DataContext = this;
        TaskName = taskName;
        Initialize(url, title, index);
    }

    public static DownloadItem GetItem(Func<string> url, string title, string index, string taskName)
    {
        if (RecycleContainer.Count <= 0) return new DownloadItem(url, title, index, taskName);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.Initialize(url, title, index);
        item.TaskName = taskName;
        return item;
    }
}