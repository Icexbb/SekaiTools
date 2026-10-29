using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.Source;
using SekaiDataFetch;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadItem : UserControl
{
    private Func<string> Url { get; set; } = () => "";

    private string TitleString { get; set; } = "";
    private string IndexString { get; set; } = "";

    private string TaskName { get; set; } = "";
    private string SearchMetadata { get; set; } = "";
    internal DownloadCandidate Candidate => new(DownloadPageModel.Instance.CurrentSource.SourceName + "|" + TaskName,
        Url(), SearchMetadata + " " + TitleString + " " + IndexString);
    internal void ApplyVisibility()
    {
        if (string.IsNullOrEmpty(TaskName)) return;
        Visibility = Candidate.Matches(StoryBrowserState.GetQuery(this)) ? Visibility.Visible : Visibility.Collapsed;
        Choice.RefreshState();
    }


    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var parent = Parent;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);


            (parent as DownloadPage)?.AddTask(Candidate.Tag, Candidate.Url);
        });
    }
}

public partial class DownloadItem
{
    private DownloadItem(Func<string> url, string title, string index, string taskName, string searchMetadata)
    {
        InitializeComponent();
        DataContext = this;
        TaskName = taskName;
        SearchMetadata = searchMetadata;
        Choice.CandidateFactory = () => Candidate;
        Loaded += (_, _) => { ApplyVisibility(); ReflowParent(); };
        IsVisibleChanged += (_, _) => ReflowParent();
        Initialize(url, title, index);
    }

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
        ApplyVisibility();
    }

    private static readonly DependencyProperty MaximumColumnsProperty = DependencyProperty.RegisterAttached(
        "MaximumColumns", typeof(int), typeof(DownloadItem), new PropertyMetadata(0));

    private void ReflowParent()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new System.Action(() =>
        {
            if (Parent is not Grid grid || grid.ColumnDefinitions.Count == 0) return;
            if ((int)grid.GetValue(MaximumColumnsProperty) == 0)
            {
                grid.SetValue(MaximumColumnsProperty, grid.ColumnDefinitions.Count);
                grid.SizeChanged += (_, _) => ReflowGrid(grid);
            }
            ReflowGrid(grid);
        }));
    }

    private static void ReflowGrid(Grid grid)
    {
        var maximum = (int)grid.GetValue(MaximumColumnsProperty);
        if (maximum == 0 || grid.ActualWidth <= 0) return;
        var columns = Math.Clamp((int)(grid.ActualWidth / 220), 1, maximum);
        if (grid.ColumnDefinitions.Count != columns)
        {
            grid.ColumnDefinitions.Clear();
            for (var index = 0; index < columns; index++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var visible = grid.Children.OfType<DownloadItem>().Where(item => item.Visibility == Visibility.Visible).ToArray();
        var rows = (visible.Length + columns - 1) / columns;
        if (grid.RowDefinitions.Count != rows)
        {
            grid.RowDefinitions.Clear();
            for (var index = 0; index < rows; index++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var index = 0; index < visible.Length; index++)
        {
            Grid.SetRow(visible[index], index / columns);
            Grid.SetColumn(visible[index], index % columns);
        }
    }

    public void Recycle()
    {
        Visibility = Visibility.Collapsed;
        RecycleContainer.Add(this);
        if (Parent is Panel parent) parent.Children.Remove(this);
    }

    public static DownloadItem GetItem(Func<string> url, string title, string index, string taskName, string searchMetadata = "")
    {
        if (RecycleContainer.Count <= 0) return new DownloadItem(url, title, index, taskName, searchMetadata);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.TaskName = taskName;
        item.SearchMetadata = searchMetadata;
        item.Initialize(url, title, index);
        return item;
    }
}